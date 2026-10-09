using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Finance;

/// <summary>Règlements, virements internes, comptes, caisse, banque et prévisions (§17-§19, R-21 à R-30).</summary>
public class ServiceTresorerie(ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant, INumerotation numerotation,
    MoteurTresorerie tresorerie, Soldes soldes, Parametres parametres)
{
    // ---------- Comptes ----------

    public async Task<IReadOnlyList<CompteFinancierDto>> ComptesAsync(CancellationToken ct)
    {
        var soldesComptes = await db.MouvementsFinanciers.AsNoTracking().GroupBy(m => m.CompteId).Select(g => new { g.Key, S = g.Sum(m => m.Montant) })
            .ToDictionaryAsync(x => x.Key, x => x.S, ct);
        return (await db.ComptesFinanciers.AsNoTracking().OrderBy(c => c.Type).ThenBy(c => c.Code).ToListAsync(ct))
            .Select(c => new CompteFinancierDto(c.Id, c.Code, c.Libelle, c.Type, c.Banque, c.Rib, c.Actif, soldesComptes.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<CompteFinancierDto> EnregistrerCompteAsync(Guid? id, EnregistrementCompteDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Code, "code");
        Garde.Requis(dto.Libelle, "libelle");
        CompteFinancier c;
        if (id is null) { c = new CompteFinancier { Type = dto.Type }; db.ComptesFinanciers.Add(c); }
        else
        {
            c = Garde.Trouve(await db.ComptesFinanciers.FindAsync([id.Value], ct), "Compte");
            if (c.Type != dto.Type && await db.MouvementsFinanciers.AnyAsync(m => m.CompteId == c.Id, ct))
                throw new RegleMetierException("Le type d'un compte qui a des mouvements ne peut pas être modifié.");
            c.Type = dto.Type;
            if (!dto.Actif && c.Actif && await tresorerie.SoldeAsync(c.Id, null, ct) != 0)
                throw new RegleMetierException("Un compte dont le solde n'est pas nul ne peut pas être désactivé : virez d'abord le solde.");
        }
        c.Code = dto.Code.Trim().ToUpperInvariant(); c.Libelle = dto.Libelle; c.Banque = dto.Banque; c.Rib = dto.Rib; c.Actif = dto.Actif;
        await db.SaveChangesAsync(ct);
        return (await ComptesAsync(ct)).First(x => x.Id == c.Id);
    }

    /// <summary>Solde de départ d'un compte (reprise) : uniquement sur un compte sans mouvement.</summary>
    public async Task<CompteFinancierDto> SoldeInitialAsync(SoldeInitialCompteDto dto, CancellationToken ct)
    {
        var m = Garde.Motif(dto.Motif, "un solde initial");
        await db.VerrouillerAsync("comptes_financiers", dto.CompteId, ct);
        if (await db.MouvementsFinanciers.AnyAsync(x => x.CompteId == dto.CompteId, ct))
            throw new RegleMetierException("Ce compte a déjà des mouvements : le solde initial ne peut plus être saisi. Utilisez une opération motivée.");
        await tresorerie.EcrireAsync(dto.CompteId, dto.Montant, NatureMouvementFinancier.SoldeInitial, $"Solde initial — {m}", dto.Date, null, ct);
        audit.Enregistrer("SOLDE_INITIAL_COMPTE", "CompteFinancier", dto.CompteId, null, m, null, new { dto.Montant, dto.Date });
        await db.SaveChangesAsync(ct);
        return (await ComptesAsync(ct)).First(x => x.Id == dto.CompteId);
    }

    public async Task<Page<MouvementFinancierDto>> MouvementsAsync(Guid? compteId, DateOnly? du, DateOnly? au, int page, int taille, CancellationToken ct)
    {
        taille = Math.Clamp(taille, 1, 500);
        var q = db.MouvementsFinanciers.AsNoTracking().Where(m => (compteId == null || m.CompteId == compteId) && (du == null || m.DateOperation >= du) && (au == null || m.DateOperation <= au));
        var total = await q.CountAsync(ct);
        var liste = await q.OrderByDescending(m => m.DateOperation).ThenByDescending(m => m.Id).Skip((Math.Max(page, 1) - 1) * taille).Take(taille).ToListAsync(ct);
        return new Page<MouvementFinancierDto>(await VersDtoAsync(liste, compteId.HasValue, ct), total, page, taille);
    }

    private async Task<List<MouvementFinancierDto>> VersDtoAsync(List<MouvementFinancier> liste, bool soldeCourant, CancellationToken ct)
    {
        var comptes = await db.ComptesFinanciers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var res = new List<MouvementFinancierDto>();
        foreach (var m in liste)
        {
            decimal? solde = soldeCourant
                ? await db.MouvementsFinanciers.Where(x => x.CompteId == m.CompteId && (x.DateOperation < m.DateOperation || (x.DateOperation == m.DateOperation && x.Id <= m.Id))).SumAsync(x => x.Montant, ct)
                : null;
            res.Add(new MouvementFinancierDto(m.Id, m.CompteId, comptes.GetValueOrDefault(m.CompteId, "?"), m.DateOperation, m.Horodatage, m.Montant, m.Nature, m.Libelle,
                m.DocumentRef, m.MouvementAnnuleId, solde));
        }
        return res;
    }

    /// <summary>Opération directe motivée (frais bancaires, agios, divers) ; jamais un encaissement de vente, qui passe par son document.</summary>
    public async Task<MouvementFinancierDto> OperationAsync(Guid compteId, OperationCompteDto dto, CancellationToken ct)
    {
        Garde.Positif(dto.Montant, "montant");
        Garde.Requis(dto.Libelle, "libelle");
        if (dto.Nature is not (NatureMouvementFinancier.FraisBancaires or NatureMouvementFinancier.Autre))
            throw new ValidationException("Une opération directe est de nature « Frais bancaires » ou « Autre » ; les ventes, règlements et virements passent par leur document.");
        var compte = Garde.Trouve(await db.ComptesFinanciers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == compteId, ct), "Compte");
        await db.VerrouillerAsync("comptes_financiers", compteId, ct);
        if (compte.Type == TypeCompteFinancier.Caisse)
        {
            if (!await db.SessionsCaisse.AnyAsync(s => s.CompteId == compteId && s.Statut == StatutSessionCaisse.Ouverte, ct))
                throw new RegleMetierException("Ouvrez une session de caisse avant toute opération de caisse.");
            if (!dto.Entree && await tresorerie.SoldeAsync(compteId, null, ct) < dto.Montant)
                throw new RegleMetierException("Solde de caisse insuffisant pour cette sortie.");
        }
        var mvt = await tresorerie.EcrireAsync(compteId, dto.Entree ? dto.Montant : -dto.Montant, dto.Nature, dto.Libelle, null, m => m.DocumentRef = dto.Reference, ct);
        audit.Enregistrer("OPERATION_COMPTE", "CompteFinancier", compteId, compte.Code, dto.Libelle, null, new { dto.Montant, dto.Entree, dto.Nature });
        await db.SaveChangesAsync(ct);
        return (await VersDtoAsync([mvt], false, ct))[0];
    }

    // ---------- Règlements (R-21) ----------

    public async Task<IReadOnlyList<ReglementDto>> ReglementsAsync(SensReglement? sens, Guid? clientId, Guid? fournisseurId, DateOnly? du, DateOnly? au, CancellationToken ct)
    {
        var ids = await db.Reglements.AsNoTracking()
            .Where(r => (sens == null || r.Sens == sens) && (clientId == null || r.ClientId == clientId) && (fournisseurId == null || r.FournisseurId == fournisseurId)
                        && (du == null || r.Date >= du) && (au == null || r.Date <= au))
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.CreeLe).Take(500).Select(r => r.Id).ToListAsync(ct);
        var res = new List<ReglementDto>();
        foreach (var id in ids) res.Add(await ReglementAsync(id, ct));
        return res;
    }

    public async Task<ReglementDto> ReglementAsync(Guid id, CancellationToken ct)
    {
        var r = Garde.Trouve(await db.Reglements.AsNoTracking().Include(x => x.Affectations).FirstOrDefaultAsync(x => x.Id == id, ct), "Règlement");
        var compte = await db.ComptesFinanciers.AsNoTracking().Where(c => c.Id == r.CompteId).Select(c => c.Code).FirstAsync(ct);
        string? tiers = r.ClientId is { } c ? await db.Clients.Where(x => x.Id == c).Select(x => x.Nom).FirstOrDefaultAsync(ct)
            : r.FournisseurId is { } f ? await db.Fournisseurs.Where(x => x.Id == f).Select(x => x.Nom).FirstOrDefaultAsync(ct) : r.Beneficiaire;
        var fids = r.Affectations.Where(a => a.FactureId.HasValue).Select(a => a.FactureId!.Value).ToList();
        var cids = r.Affectations.Where(a => a.ChargeId.HasValue).Select(a => a.ChargeId!.Value).ToList();
        var pieces = await db.Factures.AsNoTracking().Where(x => fids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.NumeroTiers ?? x.Numero, ct);
        foreach (var ch in await db.Charges.AsNoTracking().Where(x => cids.Contains(x.Id)).ToListAsync(ct)) pieces[ch.Id] = ch.Numero;
        var affecte = r.Affectations.Sum(a => a.Montant);
        return new ReglementDto(r.Id, r.Numero, r.Sens, r.ClientId ?? r.FournisseurId, tiers, r.Mode, r.CompteId, compte, r.Date, r.Montant, affecte, r.Montant - affecte,
            r.Reference, r.Statut, r.Affectations.Select(a => new AffectationDto(a.FactureId, a.ChargeId, pieces.GetValueOrDefault((a.FactureId ?? a.ChargeId)!.Value, "?"), a.Montant)).ToList(),
            r.MotifAnnulation);
    }

    public async Task<ReglementDto> CreerReglementAsync(CreationReglementDto dto, CancellationToken ct)
    {
        Garde.Positif(dto.Montant, "montant");
        if (dto.Mode == ModeReglement.Credit) throw new ValidationException("Le crédit n'est pas un mode de règlement.");
        var encaissement = dto.Sens == SensReglement.Encaissement;
        if (encaissement && dto.FournisseurId is not null || !encaissement && dto.ClientId is not null && dto.Affectations.Count > 0)
            throw new ValidationException(encaissement ? "Un encaissement concerne un client." : "Un décaissement affecté concerne un fournisseur ou une charge.");
        if (dto.ClientId is null && dto.FournisseurId is null && string.IsNullOrWhiteSpace(dto.Beneficiaire))
            throw new ValidationException("Précisez le tiers (client, fournisseur ou bénéficiaire).");
        if (dto.ClientId is { } cid) Garde.Trouve(await db.Clients.FindAsync([cid], ct), "Client");
        if (dto.FournisseurId is { } fid) Garde.Trouve(await db.Fournisseurs.FindAsync([fid], ct), "Fournisseur");
        if (dto.Affectations.Any(a => a.Montant <= 0 || (a.FactureId is null) == (a.ChargeId is null)))
            throw new ValidationException("Chaque affectation vise une facture ou une charge, pour un montant positif.");
        if (dto.Affectations.Sum(a => a.Montant) > dto.Montant)
            throw new RegleMetierException("Le total affecté dépasse le montant du règlement.");
        if (dto.Affectations.GroupBy(a => a.FactureId ?? a.ChargeId).Any(g => g.Count() > 1))
            throw new ValidationException("Une même pièce ne peut figurer qu'une fois dans les affectations.");

        // Verrouillage des pièces pour éviter un sur-règlement concurrent.
        foreach (var a in dto.Affectations.OrderBy(a => a.FactureId ?? a.ChargeId))
        {
            if (a.FactureId is { } factureId)
            {
                await db.VerrouillerAsync("factures", factureId, ct);
                var f = Garde.Trouve(await db.Factures.AsNoTracking().FirstOrDefaultAsync(x => x.Id == factureId, ct), "Facture");
                if (f.Statut != StatutDocument.Valide || f.Type != TypeFacture.Facture) throw new RegleMetierException($"La pièce {f.Numero} n'est pas une facture validée.");
                if (encaissement ? f.Sens != SensFacture.Client || f.ClientId != dto.ClientId : f.Sens != SensFacture.Fournisseur || f.FournisseurId != dto.FournisseurId)
                    throw new RegleMetierException($"La facture {f.Numero} ne concerne pas ce tiers.");
                var s = await soldes.FactureAsync(factureId, ct);
                if (a.Montant > s.ResteDu) throw new RegleMetierException($"Facture {f.NumeroTiers ?? f.Numero} : affectation {a.Montant:N2} supérieure au reste dû {s.ResteDu:N2}.", "SUR_REGLEMENT");
            }
            else
            {
                if (encaissement) throw new ValidationException("Un encaissement ne s'affecte pas à une charge.");
                await db.VerrouillerAsync("charges", a.ChargeId!.Value, ct);
                var ch = Garde.Trouve(await db.Charges.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.ChargeId, ct), "Charge");
                if (ch.Statut != StatutDocument.Valide) throw new RegleMetierException($"La charge {ch.Numero} n'est pas valide.");
                if (dto.FournisseurId is not null && ch.FournisseurId != dto.FournisseurId) throw new RegleMetierException($"La charge {ch.Numero} ne concerne pas ce fournisseur.");
                var s = (await soldes.ChargesAsync([ch.Id], ct))[ch.Id];
                if (a.Montant > s.ResteDu) throw new RegleMetierException($"Charge {ch.Numero} : affectation {a.Montant:N2} supérieure au reste dû {s.ResteDu:N2}.", "SUR_REGLEMENT");
            }
        }

        var compte = await tresorerie.CompteAsync(dto.CompteId, dto.Mode, ct);
        if (!encaissement)
        {
            await db.VerrouillerAsync("comptes_financiers", compte.Id, ct);
            if (compte.Type == TypeCompteFinancier.Caisse && await tresorerie.SoldeAsync(compte.Id, null, ct) < dto.Montant)
                throw new RegleMetierException($"Solde de la caisse {compte.Code} insuffisant pour ce paiement.");
        }
        var r = new Reglement
        {
            Numero = await numerotation.ProchainAsync(encaissement ? "REGLEMENT_ENCAISSEMENT" : "REGLEMENT_DECAISSEMENT", ct), Sens = dto.Sens,
            ClientId = dto.ClientId, FournisseurId = dto.FournisseurId, Beneficiaire = dto.Beneficiaire, Mode = dto.Mode, CompteId = compte.Id, Date = dto.Date,
            Montant = dto.Montant, Reference = dto.Reference, ValideLe = horloge.Maintenant, ValidePar = courant.Id
        };
        foreach (var a in dto.Affectations) r.Affectations.Add(new AffectationReglement { ReglementId = r.Id, FactureId = a.FactureId, ChargeId = a.ChargeId, Montant = a.Montant });
        db.Reglements.Add(r);
        await db.SaveChangesAsync(ct);
        var nature = encaissement ? NatureMouvementFinancier.EncaissementClient
            : dto.Affectations.Count > 0 && dto.Affectations.All(a => a.ChargeId.HasValue) || dto.FournisseurId is null ? NatureMouvementFinancier.PaiementCharge
            : NatureMouvementFinancier.PaiementFournisseur;
        await tresorerie.EcrireAsync(compte.Id, encaissement ? dto.Montant : -dto.Montant, nature, $"{r.Numero} — {dto.Mode} {dto.Reference}".Trim(), dto.Date,
            m => { m.ReglementId = r.Id; m.DocumentRef = r.Numero; }, ct);
        audit.Enregistrer("CREATION_REGLEMENT", "Reglement", r.Id, r.Numero, null, null, new { r.Sens, r.Montant, r.Mode, Affectations = dto.Affectations });
        await db.SaveChangesAsync(ct);
        return await ReglementAsync(r.Id, ct);
    }

    /// <summary>Annulation par contre-passation : le règlement reste visible, ses affectations cessent de compter.</summary>
    public async Task<ReglementDto> AnnulerReglementAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'un règlement");
        await db.VerrouillerAsync("reglements", id, ct);
        var r = Garde.Trouve(await db.Reglements.FirstOrDefaultAsync(x => x.Id == id, ct), "Règlement");
        await db.Entry(r).ReloadAsync(ct);
        if (r.Statut == StatutReglement.Annule) throw new ConflitException("Règlement déjà annulé.");
        foreach (var mvt in await db.MouvementsFinanciers.Where(x => x.ReglementId == id && x.Nature != NatureMouvementFinancier.ContrePassation).ToListAsync(ct))
            await tresorerie.ContrePasserAsync(mvt, m, ct);
        r.Statut = StatutReglement.Annule; r.AnnuleLe = horloge.Maintenant; r.AnnulePar = courant.Id; r.MotifAnnulation = m;
        audit.Enregistrer("ANNULATION_REGLEMENT", "Reglement", r.Id, r.Numero, m, new { Statut = "Valide", r.Montant }, new { Statut = "Annule" });
        await db.SaveChangesAsync(ct);
        return await ReglementAsync(id, ct);
    }

    // ---------- Virements internes (R-28) ----------

    public async Task<IReadOnlyList<TransfertDto>> TransfertsAsync(CancellationToken ct)
    {
        var comptes = await db.ComptesFinanciers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        return (await db.TransfertsInternes.AsNoTracking().OrderByDescending(t => t.Date).Take(300).ToListAsync(ct))
            .Select(t => new TransfertDto(t.Id, t.Numero, t.CompteSourceId, comptes[t.CompteSourceId], t.CompteDestinationId, comptes[t.CompteDestinationId],
                t.Montant, t.Commission, t.Montant - t.Commission, t.Date, t.Libelle, t.AnnuleLe.HasValue)).ToList();
    }

    /// <summary>Virement entre comptes (dépôt de caisse, remise TPE…). La commission éventuelle est une charge de frais bancaires.</summary>
    public async Task<TransfertDto> TransfererAsync(CreationTransfertDto dto, CancellationToken ct)
    {
        Garde.Positif(dto.Montant, "montant");
        Garde.PositifOuNul(dto.Commission, "commission");
        Garde.Requis(dto.Libelle, "libelle");
        if (dto.Commission >= dto.Montant) throw new ValidationException("La commission doit être inférieure au montant transféré.");
        if (dto.CompteSourceId == dto.CompteDestinationId) throw new ValidationException("Les comptes source et destination doivent être différents.");
        foreach (var id in new[] { dto.CompteSourceId, dto.CompteDestinationId }.OrderBy(x => x)) await db.VerrouillerAsync("comptes_financiers", id, ct);
        var source = Garde.Trouve(await db.ComptesFinanciers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CompteSourceId, ct), "Compte source");
        Garde.Trouve(await db.ComptesFinanciers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CompteDestinationId, ct), "Compte destination");
        var solde = await tresorerie.SoldeAsync(source.Id, null, ct);
        if (solde < dto.Montant) throw new RegleMetierException($"Solde du compte {source.Code} insuffisant ({solde:N2} DH).");
        var t = new TransfertInterne
        {
            Numero = await numerotation.ProchainAsync("TRANSFERT", ct), CompteSourceId = dto.CompteSourceId, CompteDestinationId = dto.CompteDestinationId,
            Montant = dto.Montant, Commission = dto.Commission, Date = dto.Date, Libelle = dto.Libelle, ValideLe = horloge.Maintenant, ValidePar = courant.Id
        };
        db.TransfertsInternes.Add(t);
        await db.SaveChangesAsync(ct);
        var net = dto.Montant - dto.Commission;
        await tresorerie.EcrireAsync(dto.CompteSourceId, -net, NatureMouvementFinancier.TransfertInterne, $"{t.Numero} — {dto.Libelle}", dto.Date, m => { m.TransfertId = t.Id; m.DocumentRef = t.Numero; }, ct);
        await tresorerie.EcrireAsync(dto.CompteDestinationId, net, NatureMouvementFinancier.TransfertInterne, $"{t.Numero} — {dto.Libelle}", dto.Date, m => { m.TransfertId = t.Id; m.DocumentRef = t.Numero; }, ct);
        if (dto.Commission > 0)
            await tresorerie.EcrireAsync(dto.CompteSourceId, -dto.Commission, NatureMouvementFinancier.FraisBancaires, $"{t.Numero} — commission", dto.Date, m => { m.TransfertId = t.Id; m.DocumentRef = t.Numero; }, ct);
        audit.Enregistrer("TRANSFERT_INTERNE", "TransfertInterne", t.Id, t.Numero, null, null, new { dto.Montant, dto.Commission, Source = source.Code });
        await db.SaveChangesAsync(ct);
        return (await TransfertsAsync(ct)).First(x => x.Id == t.Id);
    }

    public async Task<TransfertDto> AnnulerTransfertAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'un virement");
        await db.VerrouillerAsync("transferts_internes", id, ct);
        var t = Garde.Trouve(await db.TransfertsInternes.FirstOrDefaultAsync(x => x.Id == id, ct), "Virement");
        await db.Entry(t).ReloadAsync(ct);
        if (t.AnnuleLe.HasValue) throw new ConflitException("Virement déjà annulé.");
        if (await db.LignesReleveBancaire.AnyAsync(l => l.MouvementId != null && db.MouvementsFinanciers.Any(x => x.Id == l.MouvementId && x.TransfertId == id), ct))
            throw new RegleMetierException("Ce virement est pointé dans un rapprochement bancaire : il ne peut plus être annulé.");
        foreach (var mvt in await db.MouvementsFinanciers.Where(x => x.TransfertId == id && x.Nature != NatureMouvementFinancier.ContrePassation).ToListAsync(ct))
            await tresorerie.ContrePasserAsync(mvt, m, ct);
        t.AnnuleLe = horloge.Maintenant; t.AnnulePar = courant.Id; t.MotifAnnulation = m;
        audit.Enregistrer("ANNULATION_TRANSFERT", "TransfertInterne", t.Id, t.Numero, m);
        await db.SaveChangesAsync(ct);
        return (await TransfertsAsync(ct)).First(x => x.Id == id);
    }

    // ---------- Caisse (R-27) ----------

    public async Task<SessionCaisseDto> SessionAsync(Guid id, CancellationToken ct)
    {
        var s = Garde.Trouve(await db.SessionsCaisse.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct), "Session de caisse");
        var compte = await db.ComptesFinanciers.AsNoTracking().FirstAsync(c => c.Id == s.CompteId, ct);
        var mvts = await db.MouvementsFinanciers.AsNoTracking().Where(m => m.SessionCaisseId == id).OrderBy(m => m.Id).ToListAsync(ct);
        var hors = mvts.Where(m => m.Nature != NatureMouvementFinancier.EcartCaisse).ToList();
        var enc = s.Encaissements ?? hors.Where(m => m.Montant > 0).Sum(m => m.Montant);
        var dec = s.Decaissements ?? -hors.Where(m => m.Montant < 0).Sum(m => m.Montant);
        var theo = s.SoldeTheorique ?? CalculsFinance.SoldeTheoriqueCaisse(s.SoldeOuverture, enc, dec);
        var alertes = new List<string>();
        var seuil = await parametres.DecimalAsync(Parametres.SeuilEcartCaisse, 50m, ct);
        if (s.Ecart is { } e && Math.Abs(e) > seuil) alertes.Add($"Écart de caisse de {e:N2} DH supérieur au seuil de {seuil:N2} DH : à expliquer.");
        if (s.NombreReouvertures > 0) alertes.Add($"Session rouverte {s.NombreReouvertures} fois.");
        return new SessionCaisseDto(s.Id, s.Numero, s.CompteId, compte.Code, s.Ouverture, s.SoldeOuverture, s.Cloture, enc, dec, theo, s.MontantCompte, s.Ecart,
            s.Statut, s.Commentaire, s.NombreReouvertures, await VersDtoAsync(mvts, false, ct), alertes);
    }

    public async Task<IReadOnlyList<SessionCaisseDto>> SessionsAsync(Guid? compteId, StatutSessionCaisse? statut, CancellationToken ct)
    {
        var ids = await db.SessionsCaisse.AsNoTracking().Where(s => (compteId == null || s.CompteId == compteId) && (statut == null || s.Statut == statut))
            .OrderByDescending(s => s.Ouverture).Take(100).Select(s => s.Id).ToListAsync(ct);
        var res = new List<SessionCaisseDto>();
        foreach (var id in ids) res.Add(await SessionAsync(id, ct));
        return res;
    }

    public async Task<SessionCaisseDto> OuvrirCaisseAsync(OuvertureCaisseDto dto, CancellationToken ct)
    {
        var compte = dto.CompteId is { } cid
            ? Garde.Trouve(await db.ComptesFinanciers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct), "Compte")
            : await tresorerie.CompteParDefautAsync(TypeCompteFinancier.Caisse, ct);
        if (compte.Type != TypeCompteFinancier.Caisse) throw new ValidationException("Une session s'ouvre sur un compte de type caisse.");
        await db.VerrouillerAsync("comptes_financiers", compte.Id, ct);
        if (await db.SessionsCaisse.AnyAsync(s => s.CompteId == compte.Id && s.Statut == StatutSessionCaisse.Ouverte, ct))
            throw new ConflitException($"Une session est déjà ouverte sur la caisse {compte.Code}.");
        var solde = await tresorerie.SoldeAsync(compte.Id, null, ct);
        var s = new SessionCaisse
        {
            Numero = await numerotation.ProchainAsync("SESSION_CAISSE", ct), CompteId = compte.Id, Ouverture = horloge.Maintenant, SoldeOuverture = solde, Commentaire = dto.Commentaire
        };
        if (dto.SoldeOuvertureCompte is { } compte0 && compte0 != solde)
            s.Commentaire = $"{dto.Commentaire} [Fond compté à l'ouverture {compte0:N2} DH ≠ solde théorique {solde:N2} DH]".Trim();
        db.SessionsCaisse.Add(s);
        await db.SaveChangesAsync(ct);
        return await SessionAsync(s.Id, ct);
    }

    public async Task<SessionCaisseDto> OperationCaisseAsync(Guid sessionId, OperationCaisseDto dto, CancellationToken ct)
    {
        var s = Garde.Trouve(await db.SessionsCaisse.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sessionId, ct), "Session de caisse");
        if (s.Statut != StatutSessionCaisse.Ouverte) throw new RegleMetierException("La session de caisse n'est pas ouverte.");
        await OperationAsync(s.CompteId, new OperationCompteDto(dto.Montant, dto.Entree, NatureMouvementFinancier.Autre, dto.Libelle, dto.Reference), ct);
        return await SessionAsync(sessionId, ct);
    }

    public async Task<SessionCaisseDto> CloturerCaisseAsync(Guid id, ClotureCaisseDto dto, CancellationToken ct)
    {
        Garde.PositifOuNul(dto.MontantCompte, "montantCompte");
        await db.VerrouillerAsync("sessions_caisse", id, ct);
        var s = Garde.Trouve(await db.SessionsCaisse.FirstOrDefaultAsync(x => x.Id == id, ct), "Session de caisse");
        await db.Entry(s).ReloadAsync(ct);
        if (s.Statut != StatutSessionCaisse.Ouverte) throw new ConflitException("La session n'est pas ouverte.");
        var mvts = await db.MouvementsFinanciers.Where(m => m.SessionCaisseId == id && m.Nature != NatureMouvementFinancier.EcartCaisse).Select(m => m.Montant).ToListAsync(ct);
        s.Encaissements = mvts.Where(m => m > 0).Sum();
        s.Decaissements = -mvts.Where(m => m < 0).Sum();
        s.SoldeTheorique = CalculsFinance.SoldeTheoriqueCaisse(s.SoldeOuverture, s.Encaissements.Value, s.Decaissements.Value);
        s.MontantCompte = dto.MontantCompte;
        s.Ecart = CalculsFinance.EcartCaisse(dto.MontantCompte, s.SoldeTheorique.Value);
        s.Cloture = horloge.Maintenant;
        s.CloturePar = courant.Id;
        s.Statut = StatutSessionCaisse.Cloturee;
        if (!string.IsNullOrWhiteSpace(dto.Commentaire)) s.Commentaire = dto.Commentaire;
        await db.SaveChangesAsync(ct);
        return await SessionAsync(id, ct);
    }

    /// <summary>Validation : l'écart est constaté par un mouvement « Écart de caisse » pour que le solde suive le comptage. Aucun écart n'est qualifié de vol.</summary>
    public async Task<SessionCaisseDto> ValiderCaisseAsync(Guid id, MotifDto? dto, CancellationToken ct)
    {
        await db.VerrouillerAsync("sessions_caisse", id, ct);
        var s = Garde.Trouve(await db.SessionsCaisse.FirstOrDefaultAsync(x => x.Id == id, ct), "Session de caisse");
        await db.Entry(s).ReloadAsync(ct);
        if (s.Statut != StatutSessionCaisse.Cloturee) throw new ConflitException("Seule une session clôturée peut être validée.");
        var seuil = await parametres.DecimalAsync(Parametres.SeuilEcartCaisse, 50m, ct);
        var ecart = s.Ecart!.Value;
        if (Math.Abs(ecart) > seuil && string.IsNullOrWhiteSpace(dto?.Motif ?? s.Commentaire))
            throw new RegleMetierException($"Écart de {ecart:N2} DH supérieur au seuil de {seuil:N2} DH : une explication est obligatoire pour valider.", "EXPLICATION_ECART");
        if (!string.IsNullOrWhiteSpace(dto?.Motif)) s.Commentaire = dto.Motif;
        if (ecart != 0)
            await tresorerie.EcrireAsync(s.CompteId, ecart, NatureMouvementFinancier.EcartCaisse, $"Écart de caisse {s.Numero}", horloge.JourneeDe(s.Cloture!.Value),
                m => { m.SessionCaisseId = s.Id; m.DocumentRef = s.Numero; }, ct);
        s.Statut = StatutSessionCaisse.Validee;
        s.ValideLe = horloge.Maintenant;
        s.ValidePar = courant.Id;
        audit.Enregistrer("VALIDATION_CAISSE", "SessionCaisse", s.Id, s.Numero, s.Commentaire, null, new { s.SoldeTheorique, s.MontantCompte, s.Ecart });
        await db.SaveChangesAsync(ct);
        return await SessionAsync(id, ct);
    }

    public async Task<SessionCaisseDto> RouvrirCaisseAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "la réouverture d'une caisse");
        await db.VerrouillerAsync("sessions_caisse", id, ct);
        var s = Garde.Trouve(await db.SessionsCaisse.FirstOrDefaultAsync(x => x.Id == id, ct), "Session de caisse");
        await db.Entry(s).ReloadAsync(ct);
        if (s.Statut == StatutSessionCaisse.Ouverte) throw new ConflitException("La session est déjà ouverte.");
        if (await db.SessionsCaisse.AnyAsync(x => x.CompteId == s.CompteId && x.Statut == StatutSessionCaisse.Ouverte, ct))
            throw new ConflitException("Une autre session est ouverte sur cette caisse.");
        if (await db.SessionsCaisse.AnyAsync(x => x.CompteId == s.CompteId && x.Ouverture > s.Ouverture, ct))
            throw new RegleMetierException("Seule la dernière session de la caisse peut être rouverte.");
        var avant = new { Statut = s.Statut.ToString(), s.Ecart, s.MontantCompte };
        if (s.Statut == StatutSessionCaisse.Validee)
            foreach (var mvt in await db.MouvementsFinanciers.Where(x => x.SessionCaisseId == id && x.Nature == NatureMouvementFinancier.EcartCaisse
                                                                   && !db.MouvementsFinanciers.Any(y => y.MouvementAnnuleId == x.Id)).ToListAsync(ct))
                await tresorerie.ContrePasserAsync(mvt, m, ct);
        s.Statut = StatutSessionCaisse.Ouverte;
        s.Cloture = null; s.Encaissements = null; s.Decaissements = null; s.SoldeTheorique = null; s.MontantCompte = null; s.Ecart = null;
        s.ValideLe = null; s.ValidePar = null;
        s.NombreReouvertures++;
        audit.Enregistrer("REOUVERTURE_CAISSE", "SessionCaisse", s.Id, s.Numero, m, avant, new { Statut = "Ouverte" });
        await db.SaveChangesAsync(ct);
        // La contre-passation de l'écart, écrite avant la réouverture, n'est pas rattachée à la session : elle ne fausse pas son solde théorique.
        return await SessionAsync(id, ct);
    }

    // ---------- Banque ----------

    public async Task<RapprochementDto> CreerRapprochementAsync(CreationRapprochementDto dto, CancellationToken ct)
    {
        var compte = Garde.Trouve(await db.ComptesFinanciers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == dto.CompteId, ct), "Compte");
        if (compte.Type != TypeCompteFinancier.Banque) throw new ValidationException("Le rapprochement concerne un compte bancaire.");
        var r = new RapprochementBancaire { CompteId = compte.Id, DateReleve = dto.DateReleve, SoldeReleve = dto.SoldeReleve };
        foreach (var l in dto.Lignes)
        {
            if (l.Montant == 0) throw new ValidationException("Une ligne de relevé ne peut pas être nulle.");
            r.Lignes.Add(new LigneReleveBancaire { RapprochementId = r.Id, Date = l.Date, Libelle = l.Libelle, Montant = l.Montant });
        }
        db.RapprochementsBancaires.Add(r);
        await db.SaveChangesAsync(ct);
        // Pointage automatique sûr : un seul mouvement de même montant à ±3 jours.
        var libres = await MouvementsNonPointesAsync(compte.Id, dto.DateReleve.AddDays(3), ct);
        foreach (var l in r.Lignes)
        {
            var candidats = libres.Where(m => m.Montant == l.Montant && Math.Abs(m.DateOperation.DayNumber - l.Date.DayNumber) <= 3).ToList();
            if (candidats.Count == 1 && r.Lignes.Count(x => x.Montant == l.Montant) == 1) { l.MouvementId = candidats[0].Id; libres.Remove(candidats[0]); }
        }
        await db.SaveChangesAsync(ct);
        return await RapprochementAsync(r.Id, ct);
    }

    private async Task<List<MouvementFinancier>> MouvementsNonPointesAsync(Guid compteId, DateOnly jusqua, CancellationToken ct)
    {
        var mvts = await db.MouvementsFinanciers.AsNoTracking()
            .Where(m => m.CompteId == compteId && m.DateOperation <= jusqua && m.Nature != NatureMouvementFinancier.SoldeInitial
                        && !db.LignesReleveBancaire.Any(l => l.MouvementId == m.Id)).OrderBy(m => m.DateOperation).ToListAsync(ct);
        // Un mouvement et sa contre-passation se neutralisent : ni l'un ni l'autre n'est à pointer.
        var annules = mvts.Where(m => m.MouvementAnnuleId.HasValue).Select(m => m.MouvementAnnuleId!.Value).ToHashSet();
        return mvts.Where(m => !annules.Contains(m.Id) && !(m.MouvementAnnuleId.HasValue && mvts.Any(o => o.Id == m.MouvementAnnuleId))).ToList();
    }

    public async Task<RapprochementDto> RapprochementAsync(Guid id, CancellationToken ct)
    {
        var r = Garde.Trouve(await db.RapprochementsBancaires.AsNoTracking().Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id, ct), "Rapprochement");
        var compte = await db.ComptesFinanciers.AsNoTracking().FirstAsync(c => c.Id == r.CompteId, ct);
        var soldeSsi = await tresorerie.SoldeAsync(r.CompteId, r.DateReleve, ct);
        var libres = await MouvementsNonPointesAsync(r.CompteId, r.DateReleve, ct);
        var lignesLibres = r.Lignes.Where(l => l.MouvementId is null).Sum(l => l.Montant);
        var mvtsLibres = libres.Sum(m => m.Montant);
        // Solde relevé = solde SSI − mouvements SSI non encore passés en banque + opérations bancaires non saisies dans SSI.
        var ecart = r.SoldeReleve - (soldeSsi - mvtsLibres + lignesLibres);
        var lignes = r.Lignes.OrderBy(l => l.Date).Select(l => new LigneRapprochementDto(l.Id, l.Date, l.Libelle, l.Montant, l.MouvementId,
            l.MouvementId is null ? libres.Where(m => m.Montant == l.Montant && Math.Abs(m.DateOperation.DayNumber - l.Date.DayNumber) <= 7).Select(m => m.Id).ToList() : [])).ToList();
        return new RapprochementDto(r.Id, r.CompteId, compte.Code, r.DateReleve, r.SoldeReleve, soldeSsi, lignesLibres, mvtsLibres, ecart, r.Valide, lignes,
            await VersDtoAsync(libres, false, ct));
    }

    public async Task<RapprochementDto> PointerAsync(Guid id, PointageDto dto, CancellationToken ct)
    {
        await db.VerrouillerAsync("rapprochements_bancaires", id, ct);
        var r = Garde.Trouve(await db.RapprochementsBancaires.Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id, ct), "Rapprochement");
        if (r.Valide) throw new RegleMetierException("Rapprochement validé : il ne peut plus être modifié.");
        var l = r.Lignes.FirstOrDefault(x => x.Id == dto.LigneId) ?? throw new NonTrouveException("Ligne de relevé introuvable.");
        if (dto.MouvementId is long mid)
        {
            var m = Garde.Trouve(await db.MouvementsFinanciers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mid, ct), "Mouvement");
            if (m.CompteId != r.CompteId) throw new ValidationException("Le mouvement appartient à un autre compte.");
            if (m.Montant != l.Montant) throw new RegleMetierException($"Montants différents : relevé {l.Montant:N2}, SSI {m.Montant:N2}.");
            if (await db.LignesReleveBancaire.AnyAsync(x => x.MouvementId == mid && x.Id != l.Id, ct)) throw new ConflitException("Ce mouvement est déjà pointé.");
        }
        l.MouvementId = dto.MouvementId;
        await db.SaveChangesAsync(ct);
        return await RapprochementAsync(id, ct);
    }

    public async Task<RapprochementDto> ValiderRapprochementAsync(Guid id, CancellationToken ct)
    {
        var dto = await RapprochementAsync(id, ct);
        if (dto.EcartInexplique != 0) throw new RegleMetierException($"Écart inexpliqué de {dto.EcartInexplique:N2} DH : pointez ou saisissez les opérations manquantes avant de valider.");
        if (dto.Lignes.Any(l => l.MouvementId is null))
            throw new RegleMetierException("Des lignes du relevé ne sont pas pointées : saisissez les opérations correspondantes (frais, agios…) puis pointez-les.");
        var r = await db.RapprochementsBancaires.FirstAsync(x => x.Id == id, ct);
        r.Valide = true;
        audit.Enregistrer("VALIDATION_RAPPROCHEMENT", "RapprochementBancaire", r.Id, dto.Compte, null, null, new { r.DateReleve, r.SoldeReleve, dto.SoldeSsi });
        await db.SaveChangesAsync(ct);
        return await RapprochementAsync(id, ct);
    }

    // ---------- Créances, dettes, situation (R-22 à R-30) ----------

    public async Task<IReadOnlyList<EcheanceDto>> CreancesAsync(CancellationToken ct)
    {
        var fs = await db.Factures.AsNoTracking().Include(f => f.Client)
            .Where(f => f.Sens == SensFacture.Client && f.Type == TypeFacture.Facture && f.Statut == StatutDocument.Valide).ToListAsync(ct);
        var s = await soldes.FacturesAsync(fs.Select(f => f.Id).ToList(), ct);
        return fs.Where(f => s[f.Id].ResteDu > 0).OrderBy(f => f.Echeance)
            .Select(f => new EcheanceDto(f.Id, f.Numero, f.Client!.Nom, f.ClientId, f.Date, f.Echeance, f.TotalTtc, s[f.Id].ResteDu, soldes.JoursRetard(f.Echeance, s[f.Id].ResteDu), $"Facture {f.Origine}"))
            .ToList();
    }

    public async Task<IReadOnlyList<EcheanceDto>> DettesAsync(CancellationToken ct)
    {
        var fs = await db.Factures.AsNoTracking().Include(f => f.Fournisseur)
            .Where(f => f.Sens == SensFacture.Fournisseur && f.Type == TypeFacture.Facture && f.Statut == StatutDocument.Valide).ToListAsync(ct);
        var s = await soldes.FacturesAsync(fs.Select(f => f.Id).ToList(), ct);
        var res = fs.Where(f => s[f.Id].ResteDu > 0)
            .Select(f => new EcheanceDto(f.Id, f.NumeroTiers ?? f.Numero, f.Fournisseur!.Nom, f.FournisseurId, f.Date, f.Echeance, f.TotalTtc, s[f.Id].ResteDu,
                soldes.JoursRetard(f.Echeance, s[f.Id].ResteDu), "Facture fournisseur")).ToList();
        var charges = await db.Charges.AsNoTracking().Where(c => c.Statut == StatutDocument.Valide).ToListAsync(ct);
        var sc = await soldes.ChargesAsync(charges.Select(c => c.Id).ToList(), ct);
        var noms = await db.Fournisseurs.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Nom, ct);
        res.AddRange(charges.Where(c => sc[c.Id].ResteDu > 0).Select(c => new EcheanceDto(c.Id, c.Numero,
            c.FournisseurId is { } fid ? noms.GetValueOrDefault(fid, "?") : c.Beneficiaire ?? "—", c.FournisseurId, c.Date, c.Echeance, c.MontantTtc, sc[c.Id].ResteDu,
            soldes.JoursRetard(c.Echeance, sc[c.Id].ResteDu), "Charge")));
        return res.OrderBy(e => e.Echeance).ToList();
    }

    public async Task<SituationTresorerieDto> SituationAsync(CancellationToken ct)
    {
        var comptes = await ComptesAsync(ct);
        var disponible = comptes.Where(c => c.Type is TypeCompteFinancier.Caisse or TypeCompteFinancier.Banque).Sum(c => c.Solde);
        var transit = comptes.Where(c => c.Type is TypeCompteFinancier.AttenteCarte or TypeCompteFinancier.ChequesAEncaisser).Sum(c => c.Solde);
        var creances = await CreancesAsync(ct);
        var dettes = await DettesAsync(ct);
        var bons = await db.ReglementsPoste.Where(r => r.Mode == ModeReglement.Credit && r.FactureId == null).SumAsync(r => r.Montant, ct);
        var taux = await parametres.DecimalAsync(Parametres.TauxRecouvrement, 1m, ct);
        var aujourdhui = horloge.Aujourdhui;
        var echeances = creances.Select(c => new EcheanceTresorerie(c.Echeance, c.ResteDu, true, c.Piece))
            .Concat(dettes.Select(d => new EcheanceTresorerie(d.Echeance, d.ResteDu, false, d.Piece))).ToList();
        var previsions = new[] { 7, 30, 90 }.Select(h => CalculsFinance.Prevision(aujourdhui, h, disponible, transit, echeances, taux))
            .Select(p => new PrevisionTresorerieDto(p.Horizon, p.DisponibleInitial, p.FondsEnTransit, p.EncaissementsAttendus, p.DecaissementsPrevus, p.SoldePrevu,
                p.CreancesEchuesNonIncluses, p.Hypotheses)).ToList();
        return new SituationTresorerieDto(aujourdhui, comptes, disponible, transit, creances.Sum(c => c.ResteDu), bons,
            dettes.Where(d => d.Type != "Charge").Sum(d => d.ResteDu), dettes.Where(d => d.Type == "Charge").Sum(d => d.ResteDu), previsions);
    }
}
