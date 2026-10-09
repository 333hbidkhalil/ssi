using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Application.Stocks;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Achats;

/// <summary>Cycle d'achat pour les cuves et le magasin : commande, réception contrôlée, entrée en stock (§17).</summary>
public class ServiceAchats(ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant, INumerotation numerotation,
    Parametres parametres, MoteurStock stock)
{
    private bool VoirPrix => courant.APermission(P.AchatsSensible) || courant.APermission(P.VracSensible);

    public async Task<IReadOnlyList<CommandeFournisseurDto>> CommandesAsync(Guid? fournisseurId, StatutCommande? statut, Guid? commandeClientId, CancellationToken ct)
    {
        var commandes = await db.CommandesFournisseur.AsNoTracking().Include(c => c.Fournisseur).Include(c => c.Lignes).ThenInclude(l => l.Produit)
            .Where(c => (fournisseurId == null || c.FournisseurId == fournisseurId) && (statut == null || c.Statut == statut)
                        && (commandeClientId == null || c.CommandeClientId == commandeClientId))
            .OrderByDescending(c => c.Date).ThenByDescending(c => c.Numero).Take(500).ToListAsync(ct);
        var ids = commandes.Select(c => c.Id).ToList();
        var recues = await db.Receptions.AsNoTracking().Where(r => r.CommandeId != null && ids.Contains(r.CommandeId.Value) && r.Statut == StatutDocument.Valide)
            .SelectMany(r => r.Lignes.Select(l => new { r.CommandeId, l.ProduitId, l.QuantiteAcceptee })).ToListAsync(ct);
        var livrees = await db.LivraisonsDirectes.AsNoTracking().Where(l => l.CommandeFournisseurId != null && ids.Contains(l.CommandeFournisseurId.Value) && l.Statut == StatutLivraison.Validee)
            .Select(l => new { l.CommandeFournisseurId, l.ProduitId, l.QuantiteChargee }).ToListAsync(ct);
        var v = VoirPrix;
        return commandes.Select(c => new CommandeFournisseurDto(c.Id, c.Numero, c.FournisseurId, c.Fournisseur!.Nom, c.Date, c.DatePrevue, c.Destination,
            c.CommandeClientId, c.ReferenceFournisseur, c.Conditions, v ? c.FraisPrevusHt : 0, c.Statut,
            c.Lignes.Select(l => new LigneCommandeDetailDto(l.Id, l.ProduitId, l.Produit!.Designation, l.CuveId, l.Quantite, v ? l.PrixUnitaireHt : null,
                recues.Where(r => r.CommandeId == c.Id && r.ProduitId == l.ProduitId).Sum(r => r.QuantiteAcceptee)
                + livrees.Where(r => r.CommandeFournisseurId == c.Id && r.ProduitId == l.ProduitId).Sum(r => r.QuantiteChargee))).ToList(),
            v ? c.Lignes.Sum(l => Arrondi.Montant(l.Quantite * l.PrixUnitaireHt)) : 0)).ToList();
    }

    public async Task<CommandeFournisseurDto> CreerCommandeAsync(CreationCommandeFournisseurDto dto, CancellationToken ct)
    {
        if (dto.Destination == DestinationAchat.LivraisonDirecte)
            throw new ValidationException("Les commandes fournisseur de livraison directe se créent depuis la commande client vrac.");
        var fournisseur = Garde.Trouve(await db.Fournisseurs.FindAsync([dto.FournisseurId], ct), "Fournisseur");
        if (!fournisseur.Actif) throw new RegleMetierException("Fournisseur inactif.");
        if (dto.Lignes.Count == 0) throw new ValidationException("La commande comporte au moins une ligne.");
        Garde.PositifOuNul(dto.FraisPrevusHt, "fraisPrevusHt");
        var c = new CommandeFournisseur
        {
            Numero = await numerotation.ProchainAsync("COMMANDE_FOURNISSEUR", ct), FournisseurId = dto.FournisseurId, Date = dto.Date, DatePrevue = dto.DatePrevue,
            Destination = dto.Destination, ReferenceFournisseur = dto.ReferenceFournisseur, Conditions = dto.Conditions, FraisPrevusHt = dto.FraisPrevusHt
        };
        foreach (var l in dto.Lignes)
        {
            Garde.Positif(l.Quantite, "quantite");
            Garde.PositifOuNul(l.PrixUnitaireHt, "prixUnitaireHt");
            await ControlerDestinationAsync(dto.Destination, l.ProduitId, l.CuveId, ct);
            c.Lignes.Add(new LigneCommandeFournisseur { CommandeId = c.Id, ProduitId = l.ProduitId, CuveId = l.CuveId, Quantite = l.Quantite, PrixUnitaireHt = l.PrixUnitaireHt });
        }
        db.CommandesFournisseur.Add(c);
        await db.SaveChangesAsync(ct);
        return (await CommandesAsync(null, null, null, ct)).First(x => x.Id == c.Id);
    }

    private async Task ControlerDestinationAsync(DestinationAchat destination, Guid produitId, Guid? cuveId, CancellationToken ct)
    {
        var p = Garde.Trouve(await db.Produits.Include(x => x.Famille).FirstOrDefaultAsync(x => x.Id == produitId, ct), "Produit");
        var carburant = p.Famille!.Categorie == CategorieProduit.Carburant;
        if (destination == DestinationAchat.Cuve)
        {
            if (!carburant || cuveId is null) throw new ValidationException("Un achat pour cuve concerne un carburant et précise la cuve de destination.");
            var cuve = Garde.Trouve(await db.Cuves.FindAsync([cuveId.Value], ct), "Cuve");
            if (cuve.ProduitId != produitId) throw new ValidationException($"La cuve {cuve.Code} ne contient pas {p.Designation}.");
        }
        else if (destination == DestinationAchat.Magasin && (carburant || cuveId is not null))
            throw new ValidationException("Un achat pour le magasin concerne des lubrifiants ou produits annexes.");
    }

    public async Task<CommandeFournisseurDto> ConfirmerCommandeAsync(Guid id, CancellationToken ct)
    {
        await db.VerrouillerAsync("commandes_fournisseur", id, ct);
        var c = Garde.Trouve(await db.CommandesFournisseur.FirstOrDefaultAsync(x => x.Id == id, ct), "Commande");
        await db.Entry(c).ReloadAsync(ct);
        if (c.Statut != StatutCommande.Brouillon) throw new ConflitException("Seule une commande en brouillon peut être confirmée.");
        c.Statut = StatutCommande.Confirmee;
        c.Confirmee = true;
        c.ValideLe = horloge.Maintenant;
        c.ValidePar = courant.Id;
        await db.SaveChangesAsync(ct);
        return (await CommandesAsync(null, null, null, ct)).First(x => x.Id == c.Id);
    }

    public async Task AnnulerCommandeAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'une commande");
        var c = Garde.Trouve(await db.CommandesFournisseur.FirstOrDefaultAsync(x => x.Id == id, ct), "Commande");
        if (await db.Receptions.AnyAsync(r => r.CommandeId == id && r.Statut == StatutDocument.Valide, ct)
            || await db.LivraisonsDirectes.AnyAsync(l => l.CommandeFournisseurId == id && l.Statut == StatutLivraison.Validee, ct))
            throw new RegleMetierException("Des réceptions ou livraisons validées existent pour cette commande.");
        c.Statut = StatutCommande.Annulee;
        c.AnnuleLe = horloge.Maintenant; c.AnnulePar = courant.Id; c.MotifAnnulation = m;
        audit.Enregistrer("ANNULATION_COMMANDE", "CommandeFournisseur", c.Id, c.Numero, m);
        await db.SaveChangesAsync(ct);
    }

    // ---------- Réceptions ----------

    public async Task<IReadOnlyList<ReceptionDto>> ReceptionsAsync(Guid? id, Guid? fournisseurId, CancellationToken ct)
    {
        var recs = await db.Receptions.AsNoTracking().Include(r => r.Lignes).ThenInclude(l => l.Produit)
            .Where(r => (id == null || r.Id == id) && (fournisseurId == null || r.FournisseurId == fournisseurId))
            .OrderByDescending(r => r.Horodatage).Take(500).ToListAsync(ct);
        var fournisseurs = await db.Fournisseurs.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.Nom, ct);
        var cuves = await db.Cuves.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var tol = await parametres.DecimalAsync(Parametres.ToleranceReceptionPct, 0.5m, ct);
        var v = VoirPrix;
        return recs.Select(r => new ReceptionDto(r.Id, r.Numero, r.CommandeId, r.FournisseurId, fournisseurs.GetValueOrDefault(r.FournisseurId, "?"), r.Horodatage,
            r.NumeroBl, r.Transporteur, r.Vehicule, r.Statut,
            r.Lignes.Select(l => new LigneReceptionDto(l.Id, l.ProduitId, l.Produit!.Designation, l.CuveId, l.CuveId.HasValue ? cuves.GetValueOrDefault(l.CuveId.Value) : null,
                l.QuantiteCommandee, l.QuantiteChargee, l.QuantiteRecue, l.QuantiteAcceptee, v ? l.PrixUnitaireHt : null, l.JustificationEcart)).ToList(),
            Controles(r.Lignes, tol))).ToList();
    }

    private static List<string> Controles(IEnumerable<LigneReception> lignes, decimal tolerancePct)
    {
        var msgs = new List<string>();
        foreach (var l in lignes)
        {
            var (ecart, pct, justif) = CalculsVrac.EcartQuantite(l.QuantiteChargee, l.QuantiteRecue, tolerancePct);
            if (ecart != 0) msgs.Add($"{l.Produit?.Designation} : écart chargé/reçu {ecart:N3} ({pct:N3} %){(justif ? " — justification obligatoire" : "")}.");
            if (l.QuantiteAcceptee < l.QuantiteRecue) msgs.Add($"{l.Produit?.Designation} : {l.QuantiteRecue - l.QuantiteAcceptee:N3} refusés à la réception.");
            if (l.QuantiteCommandee > 0 && l.QuantiteRecue != l.QuantiteCommandee) msgs.Add($"{l.Produit?.Designation} : reçu {l.QuantiteRecue:N3} pour {l.QuantiteCommandee:N3} commandés.");
        }
        return msgs;
    }

    public async Task<ReceptionDto> CreerReceptionAsync(CreationReceptionDto dto, CancellationToken ct)
    {
        _ = Garde.Trouve(await db.Fournisseurs.FindAsync([dto.FournisseurId], ct), "Fournisseur");
        CommandeFournisseur? commande = null;
        if (dto.CommandeId.HasValue)
        {
            commande = Garde.Trouve(await db.CommandesFournisseur.Include(c => c.Lignes).FirstOrDefaultAsync(c => c.Id == dto.CommandeId, ct), "Commande");
            if (commande.Destination == DestinationAchat.LivraisonDirecte) throw new ValidationException("Une livraison directe n'est pas réceptionnée en station (aucun effet sur le stock).");
            if (commande.Statut is StatutCommande.Annulee or StatutCommande.Brouillon) throw new RegleMetierException("La commande doit être confirmée pour être réceptionnée.");
            if (commande.FournisseurId != dto.FournisseurId) throw new ValidationException("Le fournisseur diffère de celui de la commande.");
        }
        if (dto.Lignes.Count == 0) throw new ValidationException("La réception comporte au moins une ligne.");
        var r = new Reception
        {
            Numero = await numerotation.ProchainAsync("RECEPTION", ct), CommandeId = dto.CommandeId, FournisseurId = dto.FournisseurId,
            Horodatage = dto.Horodatage ?? horloge.Maintenant, NumeroBl = dto.NumeroBl, Transporteur = dto.Transporteur, Vehicule = dto.Vehicule
        };
        foreach (var l in dto.Lignes)
        {
            Garde.PositifOuNul(l.QuantiteChargee, "quantiteChargee");
            Garde.PositifOuNul(l.QuantiteRecue, "quantiteRecue");
            Garde.PositifOuNul(l.QuantiteAcceptee, "quantiteAcceptee");
            Garde.PositifOuNul(l.PrixUnitaireHt, "prixUnitaireHt");
            if (l.QuantiteAcceptee > l.QuantiteRecue) throw new ValidationException("La quantité acceptée ne peut pas dépasser la quantité reçue.");
            await ControlerDestinationAsync(l.CuveId.HasValue ? DestinationAchat.Cuve : DestinationAchat.Magasin, l.ProduitId, l.CuveId, ct);
            var commandee = l.QuantiteCommandee;
            if (commande is not null && commandee == 0) commandee = commande.Lignes.Where(x => x.ProduitId == l.ProduitId).Sum(x => x.Quantite);
            r.Lignes.Add(new LigneReception
            {
                ReceptionId = r.Id, ProduitId = l.ProduitId, CuveId = l.CuveId, QuantiteCommandee = commandee, QuantiteChargee = l.QuantiteChargee,
                QuantiteRecue = l.QuantiteRecue, QuantiteAcceptee = l.QuantiteAcceptee, PrixUnitaireHt = l.PrixUnitaireHt, JustificationEcart = l.JustificationEcart
            });
        }
        db.Receptions.Add(r);
        await db.SaveChangesAsync(ct);
        return (await ReceptionsAsync(r.Id, null, ct)).Single();
    }

    /// <summary>R-09 : la validation ajoute la quantité ACCEPTÉE au stock, au coût HT (+ frais attribués).</summary>
    public async Task<ReceptionDto> ValiderReceptionAsync(Guid id, CancellationToken ct)
    {
        await db.VerrouillerAsync("receptions", id, ct);
        var r = Garde.Trouve(await db.Receptions.Include(x => x.Lignes).ThenInclude(l => l.Produit).FirstOrDefaultAsync(x => x.Id == id, ct), "Réception");
        await db.Entry(r).ReloadAsync(ct);
        if (r.Statut == StatutDocument.Valide) throw new ConflitException($"La réception {r.Numero} est déjà validée : elle ne peut pas être enregistrée deux fois.");
        if (r.Statut == StatutDocument.Annule) throw new ConflitException("Réception annulée.");
        var tol = await parametres.DecimalAsync(Parametres.ToleranceReceptionPct, 0.5m, ct);
        foreach (var l in r.Lignes)
        {
            var (_, _, justif) = CalculsVrac.EcartQuantite(l.QuantiteChargee, l.QuantiteRecue, tol);
            if ((justif || l.QuantiteAcceptee < l.QuantiteRecue) && string.IsNullOrWhiteSpace(l.JustificationEcart))
                throw new RegleMetierException($"{l.Produit!.Designation} : l'écart de quantité dépasse la tolérance ({tol} %) ou une quantité a été refusée ; une justification est obligatoire.");
            if (l.QuantiteAcceptee == 0) continue;
            if (l.CuveId.HasValue)
            {
                var cuve = await db.Cuves.FirstAsync(c => c.Id == l.CuveId, ct);
                var actuel = await stock.StockAsync(l.ProduitId, l.CuveId, null, ct);
                if (actuel + l.QuantiteAcceptee > cuve.Capacite)
                    throw new RegleMetierException($"La cuve {cuve.Code} ne peut pas recevoir {l.QuantiteAcceptee:N0} L (stock théorique {actuel:N0} L, capacité {cuve.Capacite:N0} L). Vérifiez les quantités ou faites un jaugeage.");
            }
            var cout = l.PrixUnitaireHt + (l.QuantiteAcceptee > 0 ? l.FraisAttribuesHt / l.QuantiteAcceptee : 0);
            await stock.EcrireAsync(new DemandeMouvement(l.ProduitId, l.CuveId, TypeMouvementStock.Reception, l.QuantiteAcceptee, cout,
                "RECEPTION", r.Id, r.Numero, r.NumeroBl is null ? null : $"BL {r.NumeroBl}", r.Horodatage), ct);
        }
        r.Statut = StatutDocument.Valide;
        r.ValideLe = horloge.Maintenant;
        r.ValidePar = courant.Id;
        if (r.CommandeId.HasValue)
        {
            var c = await db.CommandesFournisseur.Include(x => x.Lignes).FirstAsync(x => x.Id == r.CommandeId, ct);
            await db.SaveChangesAsync(ct);
            var recu = await db.LignesReception.Where(l => db.Receptions.Any(x => x.Id == l.ReceptionId && x.CommandeId == c.Id && x.Statut == StatutDocument.Valide))
                .SumAsync(l => l.QuantiteRecue, ct);
            c.Statut = recu >= c.Lignes.Sum(x => x.Quantite) ? StatutCommande.Terminee : StatutCommande.EnCours;
        }
        audit.Enregistrer("VALIDATION_RECEPTION", "Reception", r.Id, r.Numero, null, null,
            r.Lignes.Select(l => new { l.ProduitId, l.CuveId, l.QuantiteChargee, l.QuantiteRecue, l.QuantiteAcceptee }).ToList());
        await db.SaveChangesAsync(ct);
        return (await ReceptionsAsync(id, null, ct)).Single();
    }

    public async Task<ReceptionDto> AnnulerReceptionAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'une réception");
        await db.VerrouillerAsync("receptions", id, ct);
        var r = Garde.Trouve(await db.Receptions.FirstOrDefaultAsync(x => x.Id == id, ct), "Réception");
        await db.Entry(r).ReloadAsync(ct);
        if (r.Statut == StatutDocument.Annule) throw new ConflitException("Réception déjà annulée.");
        if (await db.Factures.AnyAsync(f => (f.ReceptionId == id || f.Lignes.Any(l => l.ReceptionId == id)) && f.Statut == StatutDocument.Valide, ct))
            throw new RegleMetierException("Une facture fournisseur validée référence cette réception : annulez-la ou établissez un avoir.");
        if (r.Statut == StatutDocument.Valide)
            foreach (var mvt in await db.MouvementsStock.Where(x => x.DocumentType == "RECEPTION" && x.DocumentId == id && x.Type == TypeMouvementStock.Reception).ToListAsync(ct))
                await stock.ContrePasserAsync(mvt, $"Annulation réception {r.Numero} : {m}", ct);
        var avant = r.Statut.ToString();
        r.Statut = StatutDocument.Annule;
        r.AnnuleLe = horloge.Maintenant; r.AnnulePar = courant.Id; r.MotifAnnulation = m;
        audit.Enregistrer("ANNULATION_RECEPTION", "Reception", r.Id, r.Numero, m, new { Statut = avant }, new { Statut = "Annule" });
        await db.SaveChangesAsync(ct);
        return (await ReceptionsAsync(id, null, ct)).Single();
    }
}
