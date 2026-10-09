using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Securite;
using Ssi.Application.Stocks;
using Ssi.Contracts;
using Ssi.Domain.Calculs;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Application.Station;

/// <summary>
/// Postes de travail, relevés d'index, règlements de poste, clôture et validation (§9, §10).
/// Vente par index (H-01) : les relevés validés sont la seule source du CA carburant station ;
/// les règlements de poste ne font que ventiler ce montant et ne créent jamais de vente.
/// </summary>
public class ServicePostes(
    ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant, INumerotation numerotation,
    Tarifs tarifs, Fiscalite fiscalite, Parametres parametres, MoteurStock stock, MoteurTresorerie tresorerie)
{
    public const string TypeDocument = "POSTE";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- Lecture ----------

    public async Task<Page<PosteResumeDto>> PostesAsync(DateOnly? du, DateOnly? au, StatutPoste? statut, int page, int taille, CancellationToken ct)
    {
        var q = db.Postes.AsNoTracking().AsQueryable();
        if (du.HasValue) q = q.Where(p => p.JourneeExploitation >= du);
        if (au.HasValue) q = q.Where(p => p.JourneeExploitation <= au);
        if (statut.HasValue) q = q.Where(p => p.Statut == statut.Value);
        var total = await q.CountAsync(ct);
        taille = Math.Clamp(taille, 1, 200);
        var postes = await q.OrderByDescending(p => p.Debut).Skip((Math.Max(page, 1) - 1) * taille).Take(taille).ToListAsync(ct);
        var noms = await NomsEmployesAsync(postes.Where(p => p.ResponsableId.HasValue).Select(p => p.ResponsableId!.Value), ct);
        return new Page<PosteResumeDto>(postes.Select(p => Resume(p, noms)).ToList(), total, page, taille);
    }

    private static PosteResumeDto Resume(PosteTravail p, IReadOnlyDictionary<Guid, string> noms) => new(
        p.Id, p.Numero, p.Debut, p.Fin, p.JourneeExploitation, p.Statut,
        p.ResponsableId.HasValue ? noms.GetValueOrDefault(p.ResponsableId.Value) : null, p.MontantTheoriqueTtc, p.TotalReglements, p.Ecart);

    private async Task<Dictionary<Guid, string>> NomsEmployesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var liste = ids.Distinct().ToList();
        return await db.Employes.AsNoTracking().Where(e => liste.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => (e.Prenom + " " + e.Nom).Trim(), ct);
    }

    public async Task<PosteDetailDto> DetailAsync(Guid posteId, CancellationToken ct)
    {
        var poste = Garde.Trouve(await db.Postes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == posteId, ct), "Poste");
        var releves = await RelevesAsync(posteId, ct);
        var reglements = await ReglementsAsync(posteId, ct);
        var rapport = await RapportAsync(poste, releves, reglements, ct);
        var noms = await NomsEmployesAsync(poste.ResponsableId.HasValue ? [poste.ResponsableId.Value] : [], ct);
        return new PosteDetailDto(Resume(poste, noms), releves, reglements, rapport);
    }

    private async Task<List<ReleveDto>> RelevesAsync(Guid posteId, CancellationToken ct)
    {
        var releves = await db.Releves.AsNoTracking().Include(r => r.Pistolet).ThenInclude(p => p!.Volucompteur)
            .Include(r => r.Pistolet).ThenInclude(p => p!.Produit)
            .Where(r => r.PosteId == posteId).OrderBy(r => r.Pistolet!.Volucompteur!.Code).ThenBy(r => r.Pistolet!.Code).ToListAsync(ct);
        var noms = await NomsEmployesAsync(releves.Where(r => r.OperateurId.HasValue).Select(r => r.OperateurId!.Value), ct);
        return releves.Select(r => VersDto(r, noms)).ToList();
    }

    private static ReleveDto VersDto(ReleveIndex r, IReadOnlyDictionary<Guid, string> noms) => new(
        r.Id, r.PistoletId, r.Pistolet!.Volucompteur!.Code, r.Pistolet.Code, r.Pistolet.Produit!.Designation, r.OperateurId,
        r.OperateurId.HasValue ? noms.GetValueOrDefault(r.OperateurId.Value) : null, r.IndexDebut, r.IndexFin, r.Volume, r.PrixUnitaireTtc,
        r.TauxTva, r.MontantTtc, r.MontantHt, r.Statut,
        string.IsNullOrEmpty(r.Anomalies) ? [] : JsonSerializer.Deserialize<List<AnomalieDto>>(r.Anomalies, Json) ?? [],
        r.Commentaire, r.MotifCorrection, r.Horodatage);

    private async Task<List<ReglementPosteDto>> ReglementsAsync(Guid posteId, CancellationToken ct)
    {
        var regs = await db.ReglementsPoste.AsNoTracking().Include(r => r.Client).Where(r => r.PosteId == posteId).OrderBy(r => r.CreeLe).ToListAsync(ct);
        var noms = await NomsEmployesAsync(regs.Where(r => r.OperateurId.HasValue).Select(r => r.OperateurId!.Value), ct);
        return regs.Select(r => new ReglementPosteDto(r.Id, r.Mode, r.Montant, r.ClientId, r.Client?.Nom, r.ProduitId,
            r.Volume, r.Reference, r.OperateurId, r.OperateurId.HasValue ? noms.GetValueOrDefault(r.OperateurId.Value) : null, r.FactureId.HasValue)).ToList();
    }

    // ---------- Ouverture et relevés ----------

    public async Task<PosteDetailDto> OuvrirAsync(OuverturePosteDto dto, CancellationToken ct)
    {
        if (await db.Postes.AnyAsync(p => p.Statut == StatutPoste.Ouvert, ct))
            throw new ConflitException("Un poste est déjà ouvert : clôturez-le avant d'en ouvrir un nouveau.");
        var debut = dto.Debut ?? horloge.Maintenant;
        var dernier = await db.Postes.Where(p => p.Fin != null).MaxAsync(p => (DateTimeOffset?)p.Fin, ct);
        if (dernier.HasValue && debut < dernier.Value)
            throw new RegleMetierException($"Le début du poste chevauche le poste précédent (terminé le {dernier:dd/MM/yyyy HH:mm}).", "CHEVAUCHEMENT_PERIODE");
        if (dto.ResponsableId.HasValue) _ = Garde.Trouve(await db.Employes.FindAsync([dto.ResponsableId.Value], ct), "Responsable");
        var poste = new PosteTravail
        {
            Numero = await numerotation.ProchainAsync(TypeDocument, ct), Debut = debut, JourneeExploitation = horloge.JourneeDe(debut),
            ResponsableId = dto.ResponsableId, Commentaire = dto.Commentaire
        };
        db.Postes.Add(poste);
        await db.SaveChangesAsync(ct);
        return await DetailAsync(poste.Id, ct);
    }

    public async Task<IReadOnlyList<PreparationReleveDto>> PreparationAsync(Guid posteId, CancellationToken ct)
    {
        var poste = Garde.Trouve(await db.Postes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == posteId, ct), "Poste");
        var pistolets = await db.Pistolets.AsNoTracking().Include(p => p.Volucompteur).Include(p => p.Produit)
            .Where(p => p.Actif && p.Volucompteur!.Actif).OrderBy(p => p.Volucompteur!.Code).ThenBy(p => p.Code).ToListAsync(ct);
        var deja = await db.Releves.Where(r => r.PosteId == posteId && r.Statut != StatutReleve.Annule).Select(r => r.PistoletId).ToListAsync(ct);
        var resultat = new List<PreparationReleveDto>();
        foreach (var p in pistolets)
        {
            var prix = await tarifs.PrixApplicableAsync(p.ProduitId, TypePrix.Vente, poste.Debut, ct);
            resultat.Add(new PreparationReleveDto(p.Id, p.Volucompteur!.Code, p.Code, p.Produit!.Designation,
                await IndexPrecedentAsync(p, null, ct), prix?.Prix ?? 0, deja.Contains(p.Id)));
        }
        return resultat;
    }

    /// <summary>Index de fin du dernier relevé non annulé du cycle courant, ou index initial du cycle.</summary>
    private async Task<decimal> IndexPrecedentAsync(Pistolet p, Guid? exclureReleve, CancellationToken ct)
        => await db.Releves.Where(r => r.PistoletId == p.Id && r.Cycle == p.Cycle && r.Statut != StatutReleve.Annule && r.Id != exclureReleve)
               .MaxAsync(r => (decimal?)r.IndexFin, ct) ?? p.IndexInitialCycle;

    private async Task<PosteTravail> PosteModifiableAsync(Guid posteId, bool clotureAutorisee, CancellationToken ct)
    {
        await db.VerrouillerAsync("postes_travail", posteId, ct);
        var poste = Garde.Trouve(await db.Postes.FirstOrDefaultAsync(p => p.Id == posteId, ct), "Poste");
        await db.Entry(poste).ReloadAsync(ct);
        if (poste.Statut == StatutPoste.Valide) throw new ConflitException($"Le poste {poste.Numero} est validé : il ne peut plus être modifié (réouverture autorisée requise).");
        if (poste.Statut == StatutPoste.Cloture && !clotureAutorisee) throw new ConflitException($"Le poste {poste.Numero} est clôturé.");
        return poste;
    }

    public async Task<ResultatReleveDto> SaisirReleveAsync(Guid posteId, SaisieReleveDto dto, CancellationToken ct)
    {
        var poste = await PosteModifiableAsync(posteId, false, ct);
        var pistolet = Garde.Trouve(await db.Pistolets.Include(p => p.Volucompteur).Include(p => p.Produit)
            .FirstOrDefaultAsync(p => p.Id == dto.PistoletId, ct), "Pistolet");
        if (!pistolet.Actif) throw new RegleMetierException($"Le pistolet {pistolet.Code} est inactif.");
        if (await db.Releves.AnyAsync(r => r.PosteId == posteId && r.PistoletId == pistolet.Id && r.Statut != StatutReleve.Annule, ct))
            throw new ConflitException($"Le pistolet {pistolet.Volucompteur!.Code}/{pistolet.Code} a déjà un relevé dans ce poste : un relevé ne peut pas être comptabilisé deux fois.");
        if (await db.EvenementsCompteur.AnyAsync(e => e.PistoletId == pistolet.Id && e.Statut == StatutDemande.Demande, ct))
            throw new RegleMetierException("Un événement de compteur (remise à zéro, remplacement, correction) est en attente de validation pour ce pistolet.");
        if (dto.OperateurId.HasValue) _ = Garde.Trouve(await db.Employes.FindAsync([dto.OperateurId.Value], ct), "Opérateur");

        var precedent = await IndexPrecedentAsync(pistolet, null, ct);
        var indexDebut = dto.IndexDebut ?? precedent;
        var (releve, anomalies, message) = await PreparerReleveAsync(poste, pistolet, indexDebut, dto.IndexFin, precedent, dto.MotifCorrection, dto.ConfirmerAvertissements, null, ct);
        if (releve is null) return new ResultatReleveDto(null, anomalies, false, message);

        releve.OperateurId = dto.OperateurId;
        releve.Commentaire = dto.Commentaire;
        db.Releves.Add(releve);
        if (releve.MotifCorrection is not null)
            audit.Enregistrer("CORRECTION_INDEX", "ReleveIndex", releve.Id, $"{poste.Numero} {pistolet.Volucompteur!.Code}/{pistolet.Code}",
                releve.MotifCorrection, new { IndexDebutAttendu = precedent }, new { IndexDebut = indexDebut, dto.IndexFin });
        await db.SaveChangesAsync(ct);
        var dtoReleve = (await RelevesAsync(posteId, ct)).First(r => r.Id == releve.Id);
        return new ResultatReleveDto(dtoReleve, anomalies, true, "Relevé enregistré.");
    }

    private async Task<(ReleveIndex? Releve, List<AnomalieDto> Anomalies, string Message)> PreparerReleveAsync(
        PosteTravail poste, Pistolet pistolet, decimal indexDebut, decimal indexFin, decimal precedent, string? motifCorrection,
        bool confirmer, ReleveIndex? existant, CancellationToken ct)
    {
        if (indexFin > pistolet.IndexMaximum || indexDebut > pistolet.IndexMaximum)
            throw new RegleMetierException($"Index supérieur au maximum du totalisateur ({pistolet.IndexMaximum}). En cas de passage par zéro, utilisez la procédure de remise à zéro.");
        var recents = await db.Releves.Where(r => r.PistoletId == pistolet.Id && r.Statut == StatutReleve.Valide)
            .OrderByDescending(r => r.Horodatage).Take(10).Select(r => r.Volume).ToListAsync(ct);
        var tolerance = new ToleranceIndex(
            await parametres.DecimalAsync(Parametres.FacteurVolumeInhabituel, 3m, ct),
            await parametres.DecimalOptionnelAsync(Parametres.VolumeMaxPoste, ct));
        var anomalies = CalculsIndex.Controler(indexDebut, indexFin, precedent, recents, tolerance)
            .Select(a => new AnomalieDto(a.Code, a.Message, a.Gravite == GraviteAnomalie.Bloquant)).ToList();

        var continuite = anomalies.FirstOrDefault(a => a.Code == "CONTINUITE");
        string? motif = null;
        if (continuite is not null)
        {
            if (string.IsNullOrWhiteSpace(motifCorrection))
                return (null, anomalies, "L'index de début ne correspond pas au relevé précédent. Une correction autorisée avec motif est nécessaire.");
            Garde.Permission(courant, P.StationCorrigerIndex);
            motif = Garde.Motif(motifCorrection, "une correction d'index");
            anomalies[anomalies.IndexOf(continuite)] = continuite with { Bloquante = false, Message = continuite.Message + " Correction autorisée : " + motif };
        }
        var bloquantes = anomalies.Where(a => a.Bloquante).ToList();
        if (bloquantes.Count > 0) throw new RegleMetierException(string.Join(" ", bloquantes.Select(b => b.Message)), bloquantes[0].Code);
        if (anomalies.Any(a => a.Code != "CONTINUITE") && !confirmer)
            return (null, anomalies, "Avertissements à confirmer avant enregistrement.");

        var prix = await tarifs.PrixVenteObligatoireAsync(pistolet.ProduitId, poste.Debut, ct);
        var taux = await fiscalite.TauxProduitAsync(pistolet.ProduitId, poste.JourneeExploitation, ct);
        var volume = CalculsIndex.Volume(indexDebut, indexFin);
        var montants = CalculsIndex.Montants(volume, prix.Prix, taux);
        var r = existant ?? new ReleveIndex { PosteId = poste.Id, PistoletId = pistolet.Id, Cycle = pistolet.Cycle };
        r.IndexDebut = indexDebut; r.IndexFin = indexFin; r.Horodatage = horloge.Maintenant;
        r.PrixUnitaireTtc = prix.Prix; r.PrixProduitId = prix.Id; r.TauxTva = taux; r.Volume = volume;
        r.MontantTtc = montants.Ttc; r.MontantHt = montants.Ht;
        r.Anomalies = anomalies.Count > 0 ? JsonSerializer.Serialize(anomalies, Json) : null;
        r.MotifCorrection = motif ?? (existant?.MotifCorrection);
        return (r, anomalies, "OK");
    }

    public async Task<ResultatReleveDto> ModifierReleveAsync(Guid posteId, Guid releveId, SaisieReleveDto dto, CancellationToken ct)
    {
        var poste = await PosteModifiableAsync(posteId, false, ct);
        var releve = Garde.Trouve(await db.Releves.FirstOrDefaultAsync(r => r.Id == releveId && r.PosteId == posteId, ct), "Relevé");
        if (releve.Statut != StatutReleve.Saisi) throw new ConflitException("Seul un relevé saisi (non validé, non annulé) peut être modifié.");
        var pistolet = await db.Pistolets.Include(p => p.Volucompteur).FirstAsync(p => p.Id == releve.PistoletId, ct);
        var avant = new { releve.IndexDebut, releve.IndexFin, releve.OperateurId };
        var precedent = await IndexPrecedentAsync(pistolet, releve.Id, ct);
        var (r, anomalies, message) = await PreparerReleveAsync(poste, pistolet, dto.IndexDebut ?? releve.IndexDebut, dto.IndexFin, precedent,
            dto.MotifCorrection, dto.ConfirmerAvertissements, releve, ct);
        if (r is null)
        {
            await db.Entry(releve).ReloadAsync(ct);
            return new ResultatReleveDto(null, anomalies, false, message);
        }
        r.OperateurId = dto.OperateurId;
        r.Commentaire = dto.Commentaire;
        audit.Enregistrer("CORRECTION_INDEX", "ReleveIndex", r.Id, $"{poste.Numero} {pistolet.Volucompteur!.Code}/{pistolet.Code}",
            dto.MotifCorrection ?? "Modification avant validation", avant, new { r.IndexDebut, r.IndexFin, r.OperateurId });
        await db.SaveChangesAsync(ct);
        return new ResultatReleveDto((await RelevesAsync(posteId, ct)).First(x => x.Id == r.Id), anomalies, true, "Relevé modifié.");
    }

    public async Task AnnulerReleveAsync(Guid posteId, Guid releveId, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'un relevé");
        var poste = await PosteModifiableAsync(posteId, true, ct);
        var releve = Garde.Trouve(await db.Releves.FirstOrDefaultAsync(r => r.Id == releveId && r.PosteId == posteId, ct), "Relevé");
        if (releve.Statut != StatutReleve.Saisi) throw new ConflitException("Seul un relevé non validé peut être annulé.");
        releve.Statut = StatutReleve.Annule;
        releve.Commentaire = $"{releve.Commentaire} [Annulé : {m}]".Trim();
        audit.Enregistrer("ANNULATION_RELEVE", "ReleveIndex", releve.Id, poste.Numero, m, new { releve.IndexDebut, releve.IndexFin, releve.Volume }, null);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Règlements du poste ----------

    public async Task<ReglementPosteDto> AjouterReglementAsync(Guid posteId, SaisieReglementPosteDto dto, CancellationToken ct)
    {
        await PosteModifiableAsync(posteId, true, ct);
        Garde.Positif(dto.Montant, "montant");
        var mode = dto.Mode;
        if (mode == ModeReglement.Credit)
        {
            if (dto.ClientId is null) throw new ValidationException("Un bon de crédit doit être rattaché à un client.");
            var client = Garde.Trouve(await db.Clients.FindAsync([dto.ClientId.Value], ct), "Client");
            if (!client.Actif) throw new RegleMetierException($"Le client {client.Nom} est inactif.");
            if (client.Type != TypeClient.Credit) throw new RegleMetierException($"Le client {client.Nom} est un client au comptant : aucune vente à crédit n'est autorisée.");
            if (dto.ProduitId is null || dto.Volume is null or <= 0) throw new ValidationException("Un bon de crédit carburant précise le produit et le volume servi.");
        }
        var r = new ReglementPoste
        {
            PosteId = posteId, Mode = mode, Montant = dto.Montant, ClientId = dto.ClientId, ProduitId = dto.ProduitId,
            Volume = dto.Volume, Reference = dto.Reference, OperateurId = dto.OperateurId
        };
        db.ReglementsPoste.Add(r);
        await db.SaveChangesAsync(ct);
        await RecalculerTotauxAsync(posteId, ct);
        return (await ReglementsAsync(posteId, ct)).First(x => x.Id == r.Id);
    }

    public async Task SupprimerReglementAsync(Guid posteId, Guid reglementId, CancellationToken ct)
    {
        await PosteModifiableAsync(posteId, true, ct);
        var r = Garde.Trouve(await db.ReglementsPoste.FirstOrDefaultAsync(x => x.Id == reglementId && x.PosteId == posteId, ct), "Règlement");
        db.ReglementsPoste.Remove(r);
        await db.SaveChangesAsync(ct);
        await RecalculerTotauxAsync(posteId, ct);
    }

    private async Task RecalculerTotauxAsync(Guid posteId, CancellationToken ct)
    {
        var poste = await db.Postes.FirstAsync(p => p.Id == posteId, ct);
        poste.MontantTheoriqueTtc = await db.Releves.Where(r => r.PosteId == posteId && r.Statut != StatutReleve.Annule).SumAsync(r => r.MontantTtc, ct);
        poste.TotalReglements = await db.ReglementsPoste.Where(r => r.PosteId == posteId).SumAsync(r => r.Montant, ct);
        poste.Ecart = CalculsIndex.EcartPoste(poste.MontantTheoriqueTtc, [poste.TotalReglements]);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Clôture, validation, réouverture ----------

    public async Task<PosteDetailDto> CloturerAsync(Guid posteId, DateTimeOffset? fin, CancellationToken ct)
    {
        var poste = await PosteModifiableAsync(posteId, false, ct);
        var finPoste = fin ?? horloge.Maintenant;
        if (finPoste <= poste.Debut) throw new ValidationException("La fin du poste doit être postérieure à son début.");

        var actifs = await db.Pistolets.Include(p => p.Volucompteur).Where(p => p.Actif && p.Volucompteur!.Actif).ToListAsync(ct);
        var releves = await db.Releves.Where(r => r.PosteId == posteId && r.Statut != StatutReleve.Annule).ToListAsync(ct);
        var manquants = actifs.Where(p => releves.All(r => r.PistoletId != p.Id)).Select(p => $"{p.Volucompteur!.Code}/{p.Code}").ToList();
        if (manquants.Count > 0)
            throw new RegleMetierException($"Absence de relève pour : {string.Join(", ", manquants)}. Saisissez un relevé (même à volume nul) pour chaque pistolet actif.", "ABSENCE_RELEVE");

        // H-06 : un changement de prix pendant le poste impose de clôturer le poste à l'heure du changement.
        var produits = actifs.Select(p => p.ProduitId).Distinct().ToList();
        var changement = await db.PrixProduits.Where(p => produits.Contains(p.ProduitId) && p.Type == TypePrix.Vente
                                                         && p.Debut > poste.Debut && p.Debut < finPoste)
            .OrderBy(p => p.Debut).FirstOrDefaultAsync(ct);
        if (changement is not null)
            throw new RegleMetierException($"Un changement de prix intervient le {changement.Debut:dd/MM/yyyy HH:mm} pendant ce poste. Clôturez le poste à cette heure puis ouvrez un nouveau poste.", "CHANGEMENT_PRIX_POSTE");

        poste.Fin = finPoste;
        poste.Statut = StatutPoste.Cloture;
        poste.CloturePar = courant.Id;
        poste.ClotureLe = horloge.Maintenant;
        await db.SaveChangesAsync(ct);
        await RecalculerTotauxAsync(posteId, ct);
        return await DetailAsync(posteId, ct);
    }

    public async Task<PosteDetailDto> ValiderAsync(Guid posteId, CancellationToken ct)
    {
        await db.VerrouillerAsync("postes_travail", posteId, ct);
        var poste = Garde.Trouve(await db.Postes.FirstOrDefaultAsync(p => p.Id == posteId, ct), "Poste");
        await db.Entry(poste).ReloadAsync(ct);
        if (poste.Statut == StatutPoste.Valide) throw new ConflitException($"Le poste {poste.Numero} est déjà validé : il ne peut pas être comptabilisé deux fois.");
        if (poste.Statut != StatutPoste.Cloture) throw new RegleMetierException("Le poste doit être clôturé avant sa validation.");

        var releves = await db.Releves.Include(r => r.Pistolet).Where(r => r.PosteId == posteId && r.Statut == StatutReleve.Saisi).ToListAsync(ct);
        foreach (var r in releves)
        {
            if (r.Volume > 0)
            {
                var mvt = await stock.EcrireAsync(new DemandeMouvement(r.Pistolet!.ProduitId, r.Pistolet.CuveId, TypeMouvementStock.Distribution, -r.Volume,
                    null, TypeDocument, poste.Id, poste.Numero, $"Distribution {r.Pistolet.Code}", poste.Fin), ct);
                r.CoutUnitaire = mvt.CoutUnitaire;
            }
            else r.CoutUnitaire = 0;
            r.Statut = StatutReleve.Valide;
        }

        var reglements = await db.ReglementsPoste.Where(r => r.PosteId == posteId).ToListAsync(ct);
        foreach (var groupe in reglements.Where(r => r.Mode != ModeReglement.Credit).GroupBy(r => r.Mode))
        {
            var type = groupe.Key switch
            {
                ModeReglement.Carte => TypeCompteFinancier.AttenteCarte,
                ModeReglement.Cheque or ModeReglement.Effet => TypeCompteFinancier.ChequesAEncaisser,
                ModeReglement.Virement => TypeCompteFinancier.Banque,
                _ => TypeCompteFinancier.Caisse
            };
            var compte = await tresorerie.CompteParDefautAsync(type, ct);
            await tresorerie.EcrireAsync(compte.Id, groupe.Sum(x => x.Montant), NatureMouvementFinancier.EncaissementVenteStation,
                $"Poste {poste.Numero} — {groupe.Key}", horloge.JourneeDe(poste.Fin!.Value), m => { m.PosteId = poste.Id; m.DocumentRef = poste.Numero; }, ct);
        }

        poste.Statut = StatutPoste.Valide;
        poste.ValideLe = horloge.Maintenant;
        poste.ValidePar = courant.Id;
        await db.SaveChangesAsync(ct);
        await RecalculerTotauxAsync(posteId, ct);
        audit.Enregistrer("VALIDATION_VENTE", "PosteTravail", poste.Id, poste.Numero, null, null,
            new { poste.MontantTheoriqueTtc, poste.TotalReglements, poste.Ecart, Releves = releves.Count });
        await db.SaveChangesAsync(ct);
        return await DetailAsync(posteId, ct);
    }

    public async Task<PosteDetailDto> RouvrirAsync(Guid posteId, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "la réouverture d'un poste validé");
        await db.VerrouillerAsync("postes_travail", posteId, ct);
        var poste = Garde.Trouve(await db.Postes.FirstOrDefaultAsync(p => p.Id == posteId, ct), "Poste");
        await db.Entry(poste).ReloadAsync(ct);
        if (poste.Statut != StatutPoste.Valide) throw new ConflitException("Seul un poste validé peut être rouvert.");
        if (await db.ReglementsPoste.AnyAsync(r => r.PosteId == posteId && r.FactureId != null, ct))
            throw new RegleMetierException("Des bons de crédit de ce poste ont déjà été facturés : annulez d'abord la facture récapitulative.");

        // Contre-passation des mouvements générés (les originaux restent dans l'historique).
        var mouvements = await db.MouvementsStock.Where(x => x.DocumentType == TypeDocument && x.DocumentId == posteId).ToListAsync(ct);
        var dejaContrePasses = mouvements.Where(x => x.Type == TypeMouvementStock.ContrePassation).Select(x => x.DocumentRef).ToHashSet();
        foreach (var mvt in mouvements.Where(x => x.Type != TypeMouvementStock.ContrePassation && !dejaContrePasses.Contains($"Contre-passation mvt {x.Id}")))
            await stock.ContrePasserAsync(mvt, $"Réouverture poste {poste.Numero} : {m}", ct);

        var financiers = await db.MouvementsFinanciers.Where(x => x.PosteId == posteId).ToListAsync(ct);
        var annules = financiers.Where(x => x.MouvementAnnuleId.HasValue).Select(x => x.MouvementAnnuleId!.Value).ToHashSet();
        foreach (var mf in financiers.Where(x => x.Nature != NatureMouvementFinancier.ContrePassation && !annules.Contains(x.Id)))
            await tresorerie.ContrePasserAsync(mf, $"Réouverture poste {poste.Numero}", ct);

        foreach (var r in await db.Releves.Where(r => r.PosteId == posteId && r.Statut == StatutReleve.Valide).ToListAsync(ct))
        {
            r.Statut = StatutReleve.Saisi;
            r.CoutUnitaire = null;
        }
        var avant = new { Statut = poste.Statut.ToString(), poste.ValideLe, poste.ValidePar };
        poste.Statut = StatutPoste.Cloture;
        poste.ValideLe = null;
        poste.ValidePar = null;
        poste.NombreReouvertures++;
        audit.Enregistrer("REOUVERTURE_CLOTURE", "PosteTravail", poste.Id, poste.Numero, m, avant, new { Statut = poste.Statut.ToString() });
        await db.SaveChangesAsync(ct);
        return await DetailAsync(posteId, ct);
    }

    // ---------- Rapport de clôture ----------

    private async Task<RapportPosteDto> RapportAsync(PosteTravail poste, List<ReleveDto> releves, List<ReglementPosteDto> reglements, CancellationToken ct)
    {
        var actifs = releves.Where(r => r.Statut != StatutReleve.Annule).ToList();
        var theorique = actifs.Sum(r => r.MontantTtc);
        var totalReg = reglements.Sum(r => r.Montant);
        var ecart = CalculsIndex.EcartPoste(theorique, [totalReg]);
        var produits = await db.Produits.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Designation, ct);

        var parProduit = actifs.GroupBy(r => r.Produit).Select(g => new LigneRapportProduitDto(g.Key, g.Sum(r => r.Volume), g.Sum(r => r.MontantTtc),
            g.Sum(r => r.MontantHt), reglements.Where(x => x.Mode == ModeReglement.Credit && x.ProduitId.HasValue
                && produits.GetValueOrDefault(x.ProduitId.Value) == g.Key).Sum(x => x.Volume ?? 0))).OrderBy(x => x.Produit).ToList();

        var operateurs = actifs.Select(r => r.Operateur ?? "(non renseigné)").Union(reglements.Select(r => r.Operateur ?? "(non renseigné)")).Distinct();
        var parOperateur = operateurs.Select(o =>
        {
            var th = actifs.Where(r => (r.Operateur ?? "(non renseigné)") == o).Sum(r => r.MontantTtc);
            var rg = reglements.Where(r => (r.Operateur ?? "(non renseigné)") == o).Sum(r => r.Montant);
            return new LigneRapportOperateurDto(o, actifs.Where(r => (r.Operateur ?? "(non renseigné)") == o).Sum(r => r.Volume), th, rg, th - rg);
        }).OrderBy(x => x.Operateur).ToList();

        var parMode = reglements.GroupBy(r => r.Mode).Select(g => new LigneRapportModeDto(g.Key, g.Sum(x => x.Montant))).OrderBy(x => x.Mode).ToList();

        var manquants = new List<string>();
        if (poste.Statut == StatutPoste.Ouvert)
        {
            var actifsPist = await db.Pistolets.AsNoTracking().Include(p => p.Volucompteur).Where(p => p.Actif && p.Volucompteur!.Actif).ToListAsync(ct);
            manquants = actifsPist.Where(p => actifs.All(r => r.PistoletId != p.Id)).Select(p => $"{p.Volucompteur!.Code}/{p.Code}").ToList();
        }

        var alertes = new List<string>();
        var seuil = await parametres.DecimalAsync(Parametres.SeuilEcartPoste, 50m, ct);
        if (Math.Abs(ecart) > seuil)
            alertes.Add($"Écart de poste de {ecart:N2} DH supérieur au seuil de {seuil:N2} DH : contrôle des remises et des bons recommandé.");
        foreach (var p in parProduit.Where(p => p.VolumeBonsCredit > p.Volume))
            alertes.Add($"Les bons de crédit {p.Produit} ({p.VolumeBonsCredit:N2} L) dépassent le volume des index ({p.Volume:N2} L) : incohérence de saisie.");
        foreach (var r in actifs.Where(r => r.Anomalies.Count > 0))
            alertes.Add($"{r.Volucompteur}/{r.Pistolet} : {string.Join(" ; ", r.Anomalies.Select(a => a.Message))}");
        var clientsCredit = reglements.Where(r => r.Mode == ModeReglement.Credit && r.ClientId.HasValue).Select(r => r.ClientId!.Value).Distinct().ToList();
        foreach (var cid in clientsCredit)
        {
            var c = await db.Clients.AsNoTracking().FirstAsync(x => x.Id == cid, ct);
            var encours = await EncoursClientAsync(cid, ct);
            if (c.PlafondCredit > 0 && encours > c.PlafondCredit)
                alertes.Add($"Client {c.Nom} : encours {encours:N2} DH supérieur au plafond de crédit {c.PlafondCredit:N2} DH.");
        }

        string interpretation = ecart switch
        {
            > 0 => $"Manquant de {ecart:N2} DH (règlements inférieurs au montant théorique des index).",
            < 0 => $"Excédent de {-ecart:N2} DH (règlements supérieurs au montant théorique des index).",
            _ => "Aucun écart."
        };
        string? responsable = poste.ResponsableId.HasValue ? (await NomsEmployesAsync([poste.ResponsableId.Value], ct)).GetValueOrDefault(poste.ResponsableId.Value) : null;
        return new RapportPosteDto(theorique, totalReg, ecart, interpretation, parProduit, actifs, parOperateur, parMode, manquants, alertes, responsable);
    }

    /// <summary>Encours client (R-22) = créances sur factures validées + bons de crédit de postes non facturés.</summary>
    public async Task<decimal> EncoursClientAsync(Guid clientId, CancellationToken ct)
    {
        var factures = await db.Factures.Where(f => f.ClientId == clientId && f.Statut == StatutDocument.Valide)
            .Select(f => new { f.Id, f.Type, f.TotalTtc, f.FactureOrigineId }).ToListAsync(ct);
        var ids = factures.Select(f => f.Id).ToList();
        var affecte = await db.AffectationsReglement.Where(a => a.FactureId != null && ids.Contains(a.FactureId.Value)
                && db.Reglements.Any(r => r.Id == a.ReglementId && r.Statut == StatutReglement.Valide))
            .SumAsync(a => a.Montant, ct);
        var creance = factures.Where(f => f.Type == TypeFacture.Facture).Sum(f => f.TotalTtc)
                      - factures.Where(f => f.Type == TypeFacture.Avoir).Sum(f => f.TotalTtc) - affecte;
        var bons = await db.ReglementsPoste.Where(r => r.ClientId == clientId && r.Mode == ModeReglement.Credit && r.FactureId == null).SumAsync(r => r.Montant, ct);
        return creance + bons;
    }

    // ---------- Événements de compteur ----------

    public async Task<EvenementCompteurDto> DemanderEvenementAsync(Guid pistoletId, SaisieEvenementCompteurDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "un événement de compteur");
        var p = Garde.Trouve(await db.Pistolets.FindAsync([pistoletId], ct), "Pistolet");
        Garde.PositifOuNul(dto.NouvelIndex, "nouvelIndex");
        if (dto.NouvelIndex > p.IndexMaximum) throw new ValidationException("Le nouvel index dépasse le maximum du totalisateur.");
        if (await db.EvenementsCompteur.AnyAsync(e => e.PistoletId == pistoletId && e.Statut == StatutDemande.Demande, ct))
            throw new ConflitException("Un événement est déjà en attente de validation pour ce pistolet.");
        var e = new EvenementCompteur
        {
            PistoletId = pistoletId, Type = dto.Type, AncienIndex = dto.AncienIndex, NouvelIndex = dto.NouvelIndex,
            Horodatage = horloge.Maintenant, Motif = motif
        };
        db.EvenementsCompteur.Add(e);
        audit.Enregistrer("DEMANDE_EVENEMENT_COMPTEUR", "Pistolet", pistoletId, p.Code, motif, null, new { dto.Type, dto.AncienIndex, dto.NouvelIndex });
        await db.SaveChangesAsync(ct);
        return VersDto(e);
    }

    private static EvenementCompteurDto VersDto(EvenementCompteur e) => new(e.Id, e.PistoletId, e.Type,
        e.AncienIndex, e.NouvelIndex, e.Horodatage, e.Motif, e.Statut);

    public async Task<IReadOnlyList<EvenementCompteurDto>> EvenementsAsync(Guid? pistoletId, CancellationToken ct)
        => (await db.EvenementsCompteur.AsNoTracking().Where(e => pistoletId == null || e.PistoletId == pistoletId)
            .OrderByDescending(e => e.Horodatage).Take(200).ToListAsync(ct)).Select(VersDto).ToList();

    /// <summary>Validation : ouvre un nouveau cycle du totalisateur démarrant au nouvel index.</summary>
    public async Task<EvenementCompteurDto> DeciderEvenementAsync(Guid evenementId, DecisionDto dto, CancellationToken ct)
    {
        await db.VerrouillerAsync("evenements_compteur", evenementId, ct);
        var e = Garde.Trouve(await db.EvenementsCompteur.FirstOrDefaultAsync(x => x.Id == evenementId, ct), "Événement");
        await db.Entry(e).ReloadAsync(ct);
        if (e.Statut != StatutDemande.Demande) throw new ConflitException("Cet événement a déjà été traité.");
        var p = await db.Pistolets.FirstAsync(x => x.Id == e.PistoletId, ct);
        if (dto.Approuve)
        {
            if (await db.Releves.AnyAsync(r => r.PistoletId == p.Id && r.Statut == StatutReleve.Saisi, ct))
                throw new RegleMetierException("Un relevé non validé existe pour ce pistolet : validez ou annulez-le avant de changer de cycle.");
            var avant = new { p.Cycle, p.IndexInitialCycle };
            p.Cycle++;
            p.IndexInitialCycle = e.NouvelIndex;
            e.Statut = StatutDemande.Approuve;
            audit.Enregistrer("CORRECTION_INDEX", "Pistolet", p.Id, p.Code, e.Motif + (dto.Commentaire is null ? "" : $" — {dto.Commentaire}"),
                avant, new { p.Cycle, p.IndexInitialCycle, Type = e.Type.ToString() });
        }
        else
        {
            e.Statut = StatutDemande.Rejete;
            audit.Enregistrer("REJET_EVENEMENT_COMPTEUR", "Pistolet", p.Id, p.Code, dto.Commentaire);
        }
        e.ValidePar = courant.Id;
        e.ValideLe = horloge.Maintenant;
        await db.SaveChangesAsync(ct);
        return VersDto(e);
    }
}
