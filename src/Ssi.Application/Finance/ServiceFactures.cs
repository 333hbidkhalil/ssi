using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Finance;

/// <summary>
/// Factures et avoirs clients / fournisseurs (§15-§17, §37). Une facture validée crée une créance ou une dette,
/// jamais un mouvement de trésorerie (R-21 à R-23). La numérotation définitive est attribuée à la validation.
/// </summary>
public class ServiceFactures(ISsiDb db, IHorloge horloge, Audit audit, INumerotation numerotation, Fiscalite fiscalite, Soldes soldes, IUtilisateurCourant courant,
    Stocks.MoteurStock moteur)
{
    /// <summary>Écart de prix d'achat incorporé au stock restant (H-07).</summary>
    public const string EcartPrixStock = "ECART_PRIX_ACHAT";
    /// <summary>Part de l'écart de prix afférente aux quantités déjà vendues : coût de la période (H-07).</summary>
    public const string EcartPrixConsomme = "ECART_PRIX_CONSOMME";

    public async Task<FactureDto> FactureAsync(Guid id, CancellationToken ct)
        => (await ListeAsync(new FiltreFactures { Ids = [id] }, ct)).SingleOrDefault() ?? throw new NonTrouveException("Facture introuvable.");

    public sealed class FiltreFactures
    {
        public IReadOnlyList<Guid>? Ids { get; init; }
        public SensFacture? Sens { get; init; }
        public Guid? ClientId { get; init; }
        public Guid? FournisseurId { get; init; }
        public StatutDocument? Statut { get; init; }
        public DateOnly? Du { get; init; }
        public DateOnly? Au { get; init; }
        public bool NonSoldeesSeulement { get; init; }
        public Guid? CommandeClientId { get; init; }
    }

    public async Task<IReadOnlyList<FactureDto>> ListeAsync(FiltreFactures f, CancellationToken ct)
    {
        var q = db.Factures.AsNoTracking().Include(x => x.Lignes).Include(x => x.Client).Include(x => x.Fournisseur).AsQueryable();
        if (f.Ids is not null) q = q.Where(x => f.Ids.Contains(x.Id));
        if (f.Sens.HasValue) q = q.Where(x => x.Sens == f.Sens);
        if (f.ClientId.HasValue) q = q.Where(x => x.ClientId == f.ClientId);
        if (f.FournisseurId.HasValue) q = q.Where(x => x.FournisseurId == f.FournisseurId);
        if (f.Statut.HasValue) q = q.Where(x => x.Statut == f.Statut);
        if (f.Du.HasValue) q = q.Where(x => x.Date >= f.Du);
        if (f.Au.HasValue) q = q.Where(x => x.Date <= f.Au);
        if (f.CommandeClientId.HasValue) q = q.Where(x => x.CommandeClientId == f.CommandeClientId);
        var factures = await q.OrderByDescending(x => x.Date).ThenByDescending(x => x.Numero).Take(1000).ToListAsync(ct);
        var situations = await soldes.FacturesAsync(factures.Select(x => x.Id).ToList(), ct);
        var resultat = factures.Select(x =>
        {
            var s = situations[x.Id];
            return new FactureDto(x.Id, x.Numero, x.Sens, x.Type, x.Origine, x.ClientId ?? x.FournisseurId, x.Client?.Nom ?? x.Fournisseur?.Nom ?? "",
                x.NumeroTiers, x.Date, x.Echeance, x.TotalHt, x.TotalTva, x.TotalTtc, s.Regle, s.AvoirsImputes, s.ResteDu, s.Etat,
                x.Statut == StatutDocument.Valide ? soldes.JoursRetard(x.Echeance, s.ResteDu) : 0, x.Statut, x.FactureOrigineId, x.CommandeClientId,
                x.Commentaire, x.Lignes.Select(l => new LigneFactureDto(l.Id, l.ProduitId, l.Designation, l.Quantite, l.PrixUnitaireHt, l.TauxTva,
                    l.MontantHt, l.MontantTva, l.MontantTtc, l.LivraisonId, l.ReceptionId)).ToList(), []);
        });
        if (f.NonSoldeesSeulement) resultat = resultat.Where(x => x.ResteDu > 0 && x.Statut == StatutDocument.Valide);
        return resultat.ToList();
    }

    public async Task<FactureDto> CreerAsync(CreationFactureDto dto, CancellationToken ct)
    {
        if (dto.Lignes.Count == 0) throw new ValidationException("Une facture comporte au moins une ligne.");
        int delai;
        if (dto.Sens == SensFacture.Client)
        {
            if (dto.ClientId is null || dto.FournisseurId is not null) throw new ValidationException("Une facture client précise le client (et aucun fournisseur).");
            var c = Garde.Trouve(await db.Clients.FindAsync([dto.ClientId.Value], ct), "Client");
            delai = c.DelaiPaiementJours;
            if (dto.Origine is OrigineFacture.AchatStock or OrigineFacture.AchatVrac or OrigineFacture.Transport)
                throw new ValidationException("Origine incompatible avec une facture client.");
            if (dto.Origine == OrigineFacture.BonsStation && dto.Type == TypeFacture.Facture)
                throw new ValidationException("Les factures récapitulatives de bons sont générées par la fonction « Facturer les bons ».");
        }
        else
        {
            if (dto.FournisseurId is null || dto.ClientId is not null) throw new ValidationException("Une facture fournisseur précise le fournisseur (et aucun client).");
            var f = Garde.Trouve(await db.Fournisseurs.FindAsync([dto.FournisseurId.Value], ct), "Fournisseur");
            delai = f.DelaiPaiementJours;
            Garde.Requis(dto.NumeroTiers, "numeroTiers");
            if (dto.Origine is OrigineFacture.Vrac or OrigineFacture.BonsStation or OrigineFacture.Articles)
                throw new ValidationException("Origine incompatible avec une facture fournisseur.");
        }

        var facture = new Facture
        {
            Numero = $"BROUILLON-{Guid.NewGuid():N}"[..20], Sens = dto.Sens, Type = dto.Type, Origine = dto.Origine, ClientId = dto.ClientId,
            FournisseurId = dto.FournisseurId, NumeroTiers = dto.NumeroTiers?.Trim(), Date = dto.Date, Echeance = dto.Echeance ?? dto.Date.AddDays(delai),
            CommandeClientId = dto.CommandeClientId, ReceptionId = dto.ReceptionId, Commentaire = dto.Commentaire, FactureOrigineId = dto.FactureOrigineId
        };
        if (facture.Echeance < facture.Date) throw new ValidationException("L'échéance ne peut pas précéder la date de facture.");

        if (dto.Type == TypeFacture.Avoir)
        {
            if (dto.FactureOrigineId is not null)
            {
                var origine = Garde.Trouve(await db.Factures.FindAsync([dto.FactureOrigineId.Value], ct), "Facture d'origine");
                if (origine.Statut != StatutDocument.Valide || origine.Type != TypeFacture.Facture) throw new RegleMetierException("Un avoir s'impute sur une facture validée.");
                if (origine.ClientId != dto.ClientId || origine.FournisseurId != dto.FournisseurId) throw new ValidationException("L'avoir doit concerner le même tiers que la facture d'origine.");
                facture.Origine = origine.Origine;
                facture.CommandeClientId = origine.CommandeClientId;
            }
        }
        else if (dto.FactureOrigineId is not null) throw new ValidationException("Seul un avoir référence une facture d'origine.");

        foreach (var l in dto.Lignes)
        {
            if (l.Quantite <= 0) throw new ValidationException("Les quantités sont positives (un avoir s'exprime en montants positifs).");
            Garde.PositifOuNul(l.PrixUnitaireHt, "prixUnitaireHt");
            string designation;
            decimal taux;
            if (l.ProduitId is Guid pid)
            {
                var p = Garde.Trouve(await db.Produits.FindAsync([pid], ct), "Produit");
                designation = string.IsNullOrWhiteSpace(l.Designation) ? p.Designation : l.Designation;
                taux = l.TauxTva ?? await fiscalite.TauxAsync(p.TaxeId, dto.Date, ct);
            }
            else
            {
                Garde.Requis(l.Designation, "designation");
                designation = l.Designation!;
                taux = l.TauxTva ?? throw new ValidationException("Le taux de TVA est obligatoire pour une ligne sans produit.");
            }
            var m = CalculsTaxes.Ligne(l.Quantite, l.PrixUnitaireHt, taux);
            facture.Lignes.Add(new LigneFacture
            {
                FactureId = facture.Id, ProduitId = l.ProduitId, Designation = designation, Quantite = l.Quantite, PrixUnitaireHt = l.PrixUnitaireHt,
                TauxTva = taux, MontantHt = m.Ht, MontantTva = m.Tva, MontantTtc = m.Ttc, LivraisonId = l.LivraisonId, ReceptionId = l.ReceptionId ?? dto.ReceptionId
            });
        }
        RecalculerTotaux(facture);
        db.Factures.Add(facture);
        await db.SaveChangesAsync(ct);
        var resultat = await FactureAsync(facture.Id, ct);
        return resultat with { Avertissements = await ControlesAsync(facture, ct) };
    }

    private static void RecalculerTotaux(Facture f)
    {
        f.TotalHt = f.Lignes.Sum(l => l.MontantHt);
        f.TotalTva = f.Lignes.Sum(l => l.MontantTva);
        f.TotalTtc = f.Lignes.Sum(l => l.MontantTtc);
    }

    /// <summary>Contrôles de cohérence (bloquants à la validation si « Bloquant : »).</summary>
    private async Task<List<string>> ControlesAsync(Facture f, CancellationToken ct)
    {
        var msgs = new List<string>();
        if (f.Sens == SensFacture.Fournisseur && f.Type == TypeFacture.Facture)
        {
            // R-25 : doublon probable (même fournisseur, même date, même montant)
            if (await db.Factures.AnyAsync(x => x.Id != f.Id && x.FournisseurId == f.FournisseurId && x.Date == f.Date && x.TotalTtc == f.TotalTtc
                                                && x.Statut != StatutDocument.Annule, ct))
                msgs.Add("Attention : une facture du même fournisseur, de même date et de même montant existe déjà (doublon probable).");
            if (await db.Factures.AnyAsync(x => x.Id != f.Id && x.FournisseurId == f.FournisseurId && x.NumeroTiers == f.NumeroTiers && x.Statut != StatutDocument.Annule, ct))
                msgs.Add("Bloquant : ce numéro de facture fournisseur est déjà enregistré.");
            // Contrôle quantités / prix par rapport aux réceptions
            foreach (var l in f.Lignes.Where(l => l.ReceptionId.HasValue && l.ProduitId.HasValue))
            {
                var lr = await db.LignesReception.AsNoTracking().Where(x => x.ReceptionId == l.ReceptionId && x.ProduitId == l.ProduitId).ToListAsync(ct);
                if (lr.Count == 0) { msgs.Add($"Ligne {l.Designation} : aucune ligne de réception correspondante."); continue; }
                var acceptee = lr.Sum(x => x.QuantiteAcceptee);
                var chargee = lr.Sum(x => x.QuantiteChargee);
                if (l.Quantite != acceptee && l.Quantite != chargee)
                    msgs.Add($"Ligne {l.Designation} : quantité facturée {l.Quantite:N3} différente de la quantité acceptée ({acceptee:N3}) et chargée ({chargee:N3}).");
                var pu = lr.First().PrixUnitaireHt;
                if (l.PrixUnitaireHt != pu) msgs.Add($"Ligne {l.Designation} : prix facturé {l.PrixUnitaireHt:N4} différent du prix de commande ({pu:N4}). À la validation, l'écart sera incorporé au stock ou passé en coût (H-07).");
            }
        }
        foreach (var l in f.Lignes.Where(l => l.LivraisonId.HasValue))
        {
            var liv = await db.LivraisonsDirectes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == l.LivraisonId, ct);
            if (liv is null) { msgs.Add("Bloquant : livraison introuvable."); continue; }
            if (liv.Statut != StatutLivraison.Validee) msgs.Add($"Bloquant : la livraison {liv.Numero} n'est pas validée.");
            if (f.Sens == SensFacture.Client && liv.ClientId != f.ClientId) msgs.Add($"Bloquant : la livraison {liv.Numero} concerne un autre client.");
            if (f.Sens == SensFacture.Fournisseur && liv.FournisseurId != f.FournisseurId && f.Origine == OrigineFacture.AchatVrac)
                msgs.Add($"Bloquant : la livraison {liv.Numero} a été exécutée par un autre fournisseur.");
            if (f.Type == TypeFacture.Facture && f.Origine != OrigineFacture.Transport)
            {
                var dejaFacturee = await db.LignesFacture.AnyAsync(x => x.LivraisonId == liv.Id && x.FactureId != f.Id &&
                    db.Factures.Any(ff => ff.Id == x.FactureId && ff.Sens == f.Sens && ff.Type == TypeFacture.Facture && ff.Statut != StatutDocument.Annule
                                          && ff.Origine != OrigineFacture.Transport), ct);
                if (dejaFacturee) msgs.Add($"Bloquant : la livraison {liv.Numero} est déjà facturée.");
            }
        }
        if (f.Type == TypeFacture.Avoir && f.FactureOrigineId.HasValue)
        {
            var origine = await db.Factures.AsNoTracking().FirstAsync(x => x.Id == f.FactureOrigineId, ct);
            var autresAvoirs = await db.Factures.Where(x => x.FactureOrigineId == origine.Id && x.Id != f.Id && x.Statut == StatutDocument.Valide).SumAsync(x => x.TotalTtc, ct);
            if (autresAvoirs + f.TotalTtc > origine.TotalTtc) msgs.Add("Bloquant : le total des avoirs dépasserait le montant de la facture d'origine.");
            var s = await soldes.FactureAsync(origine.Id, ct);
            if (f.TotalTtc > s.ResteDu) msgs.Add($"Attention : l'avoir ({f.TotalTtc:N2}) dépasse le reste dû ({s.ResteDu:N2}) : un remboursement sera dû au tiers.");
        }
        return msgs;
    }

    public async Task<FactureDto> ValiderAsync(Guid id, CancellationToken ct)
    {
        await db.VerrouillerAsync("factures", id, ct);
        var f = Garde.Trouve(await db.Factures.Include(x => x.Lignes).FirstOrDefaultAsync(x => x.Id == id, ct), "Facture");
        await db.Entry(f).ReloadAsync(ct);
        if (f.Statut == StatutDocument.Valide) throw new ConflitException($"La facture {f.Numero} est déjà validée.");
        if (f.Statut == StatutDocument.Annule) throw new ConflitException("Une facture annulée ne peut pas être validée.");
        var controles = await ControlesAsync(f, ct);
        var bloquants = controles.Where(c => c.StartsWith("Bloquant")).ToList();
        if (bloquants.Count > 0) throw new RegleMetierException(string.Join(" ", bloquants));
        var type = (f.Sens, f.Type) switch
        {
            (SensFacture.Client, TypeFacture.Facture) => "FACTURE_CLIENT",
            (SensFacture.Client, TypeFacture.Avoir) => "AVOIR_CLIENT",
            (SensFacture.Fournisseur, TypeFacture.Facture) => "FACTURE_FOURNISSEUR",
            _ => "AVOIR_FOURNISSEUR"
        };
        f.Numero = await numerotation.ProchainAsync(type, ct);
        f.Statut = StatutDocument.Valide;
        f.ValideLe = horloge.Maintenant;
        f.ValidePar = courant.Id;
        if (f.Sens == SensFacture.Fournisseur && f.Type == TypeFacture.Facture && f.Origine == OrigineFacture.AchatStock)
            controles.AddRange(await AjusterEcartsPrixAsync(f, ct));
        audit.Enregistrer("VALIDATION_FACTURE", "Facture", f.Id, f.Numero, null, null, new { f.Sens, f.Type, f.TotalHt, f.TotalTtc, f.NumeroTiers });
        await db.SaveChangesAsync(ct);
        return (await FactureAsync(id, ct)) with { Avertissements = controles };
    }

    /// <summary>
    /// H-07 : l'entrée en stock est valorisée au prix de commande ; à la validation de la facture fournisseur, l'écart
    /// (prix facturé − prix de réception) × quantité facturée est incorporé à la valeur du stock (CMP recalculé).
    /// La part correspondant aux quantités déjà sorties est aussitôt reprise en coût de la période, pour ne pas la reporter sur les ventes futures.
    /// Approximation documentée : la part restante = min(1, stock actuel de l'emplacement / quantité facturée).
    /// </summary>
    private async Task<List<string>> AjusterEcartsPrixAsync(Facture f, CancellationToken ct)
    {
        var infos = new List<string>();
        foreach (var l in f.Lignes.Where(l => l.ReceptionId.HasValue && l.ProduitId.HasValue && l.Quantite > 0))
        {
            var lr = await db.LignesReception.AsNoTracking().Where(x => x.ReceptionId == l.ReceptionId && x.ProduitId == l.ProduitId).ToListAsync(ct);
            var reception = await db.Receptions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == l.ReceptionId, ct);
            if (lr.Count == 0 || reception?.Statut != StatutDocument.Valide) continue;
            var puReception = lr[0].PrixUnitaireHt;
            var ecart = Arrondi.Montant((l.PrixUnitaireHt - puReception) * l.Quantite);
            if (ecart == 0) continue;
            var cuveId = lr[0].CuveId;
            var stock = await moteur.StockAsync(l.ProduitId!.Value, cuveId, null, ct);
            var partRestante = Arrondi.Montant(ecart * Math.Clamp(stock / l.Quantite, 0m, 1m));
            var partConsommee = ecart - partRestante;
            var reference = $"{f.Numero} / {reception.Numero}";
            // Écrit d'abord l'écart total puis la sortie de la part consommée : la position reste cohérente même si le stock est à zéro.
            await moteur.EcrireValeurAsync(l.ProduitId.Value, cuveId, ecart, EcartPrixStock, f.Id, reference,
                $"Écart de prix facture : {l.PrixUnitaireHt:N4} au lieu de {puReception:N4} HT sur {l.Quantite:N3}", ct);
            if (partConsommee != 0)
                await moteur.EcrireValeurAsync(l.ProduitId.Value, cuveId, -partConsommee, EcartPrixConsomme, f.Id, reference,
                    "Part de l'écart de prix afférente aux quantités déjà vendues (coût de la période)", ct);
            infos.Add($"Écart de prix {l.Designation} : {ecart:N2} DH HT, dont {partRestante:N2} incorporés au stock restant et {partConsommee:N2} en coût de la période (H-07).");
        }
        return infos;
    }

    public async Task<FactureDto> AnnulerAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'une facture");
        await db.VerrouillerAsync("factures", id, ct);
        var f = Garde.Trouve(await db.Factures.FirstOrDefaultAsync(x => x.Id == id, ct), "Facture");
        await db.Entry(f).ReloadAsync(ct);
        if (f.Statut == StatutDocument.Annule) throw new ConflitException("Facture déjà annulée.");
        if (f.Statut == StatutDocument.Valide)
        {
            var s = await soldes.FactureAsync(id, ct);
            if (s.Regle > 0) throw new RegleMetierException("Cette facture a reçu des règlements : annulez d'abord les règlements ou établissez un avoir.");
            if (s.AvoirsImputes > 0) throw new RegleMetierException("Des avoirs sont imputés sur cette facture : annulez-les d'abord.");
            if (await db.FraisDirects.AnyAsync(x => x.FactureId == id, ct)) throw new RegleMetierException("Des frais directs référencent cette facture.");
        }
        var avant = new { Statut = f.Statut.ToString(), f.TotalTtc };
        f.Statut = StatutDocument.Annule;
        f.AnnuleLe = horloge.Maintenant;
        f.MotifAnnulation = m;
        // Contre-passe les ajustements de valeur de stock nés de cette facture (H-07)
        if (f.Statut == StatutDocument.Annule)
            foreach (var mvt in await db.MouvementsStock.Where(x => x.DocumentId == id && x.Type == TypeMouvementStock.AjustementValeur
                                                                    && !db.MouvementsStock.Any(c => c.Type == TypeMouvementStock.ContrePassation && c.DocumentRef == "Contre-passation mvt " + x.Id))
                         .OrderByDescending(x => x.Id).ToListAsync(ct))
                await moteur.ContrePasserAsync(mvt, $"Annulation de la facture {f.Numero} : {m}", ct);
        // Libère les bons de crédit regroupés par une facture récapitulative
        foreach (var bon in await db.ReglementsPoste.Where(r => r.FactureId == id).ToListAsync(ct)) bon.FactureId = null;
        audit.Enregistrer("ANNULATION_FACTURE", "Facture", f.Id, f.Numero, m, avant, new { Statut = "Annule" });
        await db.SaveChangesAsync(ct);
        return await FactureAsync(id, ct);
    }

    /// <summary>
    /// Facture récapitulative des bons de crédit station (H-09). Elle transforme les bons en créance
    /// SANS créer de chiffre d'affaires : celui-ci a déjà été constaté par les relevés d'index.
    /// </summary>
    public async Task<FactureDto> FacturerBonsAsync(FacturationBonsDto dto, CancellationToken ct)
    {
        var client = Garde.Trouve(await db.Clients.FindAsync([dto.ClientId], ct), "Client");
        await db.VerrouillerAsync("clients", client.Id, ct);
        var bons = await db.ReglementsPoste.Include(r => r.Client)
            .Where(r => r.ClientId == client.Id && r.Mode == ModeReglement.Credit && r.FactureId == null
                        && db.Postes.Any(p => p.Id == r.PosteId && p.Statut == StatutPoste.Valide && (dto.Jusqua == null || p.JourneeExploitation <= dto.Jusqua)))
            .OrderBy(r => r.CreeLe).ToListAsync(ct);
        if (bons.Count == 0) throw new RegleMetierException("Aucun bon de crédit validé et non facturé pour ce client.");
        var postes = await db.Postes.AsNoTracking().Where(p => bons.Select(b => b.PosteId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var f = new Facture
        {
            Sens = SensFacture.Client, Type = TypeFacture.Facture, Origine = OrigineFacture.BonsStation, ClientId = client.Id, Date = dto.Date,
            Echeance = dto.Date.AddDays(client.DelaiPaiementJours), Commentaire = "Facture récapitulative des bons de crédit station (sans chiffre d'affaires supplémentaire)."
        };
        foreach (var b in bons)
        {
            var poste = postes[b.PosteId];
            var produit = b.ProduitId.HasValue ? await db.Produits.FindAsync([b.ProduitId.Value], ct) : null;
            var taux = produit is not null ? await fiscalite.TauxAsync(produit.TaxeId, poste.JourneeExploitation, ct) : 0m;
            var m = CalculsTaxes.DepuisTtc(b.Montant, taux);
            var q = b.Volume ?? 1;
            f.Lignes.Add(new LigneFacture
            {
                FactureId = f.Id, ProduitId = b.ProduitId, Quantite = q, TauxTva = taux, MontantHt = m.Ht, MontantTva = m.Tva, MontantTtc = m.Ttc,
                PrixUnitaireHt = Math.Round(m.Ht / q, 4, MidpointRounding.AwayFromZero),
                Designation = $"Bon {b.Reference ?? "-"}{(b.ReferenceCommande is { Length: > 0 } bc ? $" (commande {bc})" : "")} du {poste.JourneeExploitation:dd/MM/yyyy} ({poste.Numero}) — {produit?.Designation ?? "carburant"}"
            });
            b.FactureId = f.Id;
        }
        RecalculerTotaux(f);
        f.Numero = await numerotation.ProchainAsync("FACTURE_CLIENT", ct);
        f.Statut = StatutDocument.Valide;
        f.ValideLe = horloge.Maintenant;
        f.ValidePar = courant.Id;
        db.Factures.Add(f);
        audit.Enregistrer("VALIDATION_FACTURE", "Facture", f.Id, f.Numero, "Facturation des bons de crédit", null, new { Bons = bons.Count, f.TotalTtc });
        await db.SaveChangesAsync(ct);
        return await FactureAsync(f.Id, ct);
    }
}
