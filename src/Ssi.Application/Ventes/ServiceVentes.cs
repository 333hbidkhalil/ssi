using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Securite;
using Ssi.Application.Stocks;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Ventes;

/// <summary>
/// Ventes directes de lubrifiants et articles (§11). Le carburant ne se vend jamais ici : il est vendu par les index.
/// Une vente à crédit crée une facture client « Articles » qui ne génère pas de chiffre d'affaires supplémentaire (le CA est la vente).
/// </summary>
public class ServiceVentes(ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant, INumerotation numerotation,
    Fiscalite fiscalite, Tarifs tarifs, MoteurStock stock, MoteurTresorerie tresorerie, Soldes soldes)
{
    private const string TypeDocument = "VenteArticle";

    public async Task<Page<VenteDto>> VentesAsync(DateOnly? du, DateOnly? au, Guid? clientId, int page, int taille, CancellationToken ct)
    {
        taille = Math.Clamp(taille, 1, 200);
        var debut = du.HasValue ? horloge.DebutJournee(du.Value) : (DateTimeOffset?)null;
        var fin = au.HasValue ? horloge.DebutJournee(au.Value.AddDays(1)) : (DateTimeOffset?)null;
        var q = db.VentesArticles.AsNoTracking().Where(v => (debut == null || v.Horodatage >= debut) && (fin == null || v.Horodatage < fin) && (clientId == null || v.ClientId == clientId));
        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(v => v.Horodatage).Skip((Math.Max(page, 1) - 1) * taille).Take(taille).Select(v => v.Id).ToListAsync(ct);
        var res = new List<VenteDto>();
        foreach (var id in ids) res.Add(await VenteAsync(id, ct));
        return new Page<VenteDto>(res, total, page, taille);
    }

    public async Task<VenteDto> VenteAsync(Guid id, CancellationToken ct)
    {
        var v = Garde.Trouve(await db.VentesArticles.AsNoTracking().Include(x => x.Lignes).ThenInclude(l => l.Produit).FirstOrDefaultAsync(x => x.Id == id, ct), "Vente");
        var client = v.ClientId is { } cid ? await db.Clients.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Nom).FirstOrDefaultAsync(ct) : null;
        var factureId = await db.Factures.AsNoTracking().Where(f => f.Origine == OrigineFacture.Articles && f.Commentaire == VenteRef(v) && f.Statut == StatutDocument.Valide)
            .Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct);
        var sensible = courant.APermission(P.StockSensible);
        return new VenteDto(v.Id, v.Numero, v.Horodatage, v.ClientId, client, v.ModeReglement, v.Statut, v.TotalHt, v.TotalTva, v.TotalTtc,
            v.Lignes.Select(l => new LigneVenteDto(l.ProduitId, l.Produit!.Designation, l.Quantite, l.PrixUnitaireTtc, l.TauxTva, l.MontantHt, l.MontantTva,
                l.MontantTtc, sensible ? l.CoutUnitaire : null)).ToList(), factureId);
    }

    private static string VenteRef(VenteArticle v) => $"Vente d'articles {v.Numero}";

    public async Task<VenteDto> CreerAsync(CreationVenteDto dto, CancellationToken ct)
    {
        if (dto.Lignes.Count == 0) throw new ValidationException("Une vente comporte au moins une ligne.");
        Client? client = null;
        if (dto.ClientId is Guid cid)
        {
            client = Garde.Trouve(await db.Clients.FindAsync([cid], ct), "Client");
            if (!client.Actif) throw new RegleMetierException($"Le client {client.Nom} est inactif.");
        }
        if (dto.ModeReglement == ModeReglement.Credit)
        {
            if (client is null) throw new ValidationException("Une vente à crédit est rattachée à un client.");
            if (client.Type != TypeClient.Credit) throw new RegleMetierException($"Le client {client.Nom} est un client au comptant : aucune vente à crédit n'est autorisée.");
        }
        var maintenant = horloge.Maintenant;
        var jour = horloge.JourneeDe(maintenant);
        var v = new VenteArticle
        {
            Numero = await numerotation.ProchainAsync("VENTE", ct), Horodatage = maintenant, ClientId = dto.ClientId, ModeReglement = dto.ModeReglement,
            Statut = StatutDocument.Valide, ValideLe = maintenant, ValidePar = courant.Id
        };
        foreach (var l in dto.Lignes)
        {
            Garde.Positif(l.Quantite, "quantite");
            var p = Garde.Trouve(await db.Produits.Include(x => x.Famille).FirstOrDefaultAsync(x => x.Id == l.ProduitId, ct), "Produit");
            if (p.Famille!.Categorie == CategorieProduit.Carburant)
                throw new RegleMetierException($"{p.Designation} : le carburant est vendu uniquement par les relevés d'index des pistolets.", "CARBURANT_HORS_INDEX");
            if (!p.Actif) throw new RegleMetierException($"Le produit {p.Designation} est inactif.");
            decimal prix;
            if (l.PrixUnitaireTtc is decimal saisi)
            {
                Garde.PositifOuNul(saisi, "prixUnitaireTtc");
                var officiel = (await tarifs.PrixApplicableAsync(p.Id, TypePrix.Vente, maintenant, ct))?.Prix;
                if (officiel != saisi && !courant.APermission(P.PrixValider))
                    throw new AccesRefuseException($"Prix saisi différent du prix en vigueur ({officiel?.ToString("N2") ?? "aucun"}) : seul un utilisateur habilité peut le modifier.");
                prix = saisi;
            }
            else prix = (await tarifs.PrixVenteObligatoireAsync(p.Id, maintenant, ct)).Prix;
            var taux = await fiscalite.TauxAsync(p.TaxeId, jour, ct);
            var m = CalculsTaxes.LigneTtc(l.Quantite, prix, taux);
            v.Lignes.Add(new LigneVenteArticle
            {
                VenteId = v.Id, ProduitId = p.Id, Quantite = l.Quantite, PrixUnitaireTtc = prix, TauxTva = taux, MontantHt = m.Ht, MontantTva = m.Tva, MontantTtc = m.Ttc
            });
        }
        v.TotalHt = v.Lignes.Sum(l => l.MontantHt);
        v.TotalTva = v.Lignes.Sum(l => l.MontantTva);
        v.TotalTtc = v.Lignes.Sum(l => l.MontantTtc);
        db.VentesArticles.Add(v);
        await db.SaveChangesAsync(ct);

        foreach (var l in v.Lignes)
        {
            var dispo = await stock.StockAsync(l.ProduitId, null, null, ct);
            if (dispo < l.Quantite)
            {
                var nom = await db.Produits.Where(x => x.Id == l.ProduitId).Select(x => x.Designation).FirstAsync(ct);
                throw new RegleMetierException($"Stock insuffisant pour {nom} : {dispo:N3} disponible(s), {l.Quantite:N3} demandé(s). Enregistrez d'abord la réception ou l'inventaire.", "STOCK_INSUFFISANT");
            }
            var mvt = await stock.EcrireAsync(new DemandeMouvement(l.ProduitId, null, TypeMouvementStock.VenteArticle, -l.Quantite, null, TypeDocument, v.Id, v.Numero, null, maintenant), ct);
            l.CoutUnitaire = mvt.CoutUnitaire;
        }

        if (dto.ModeReglement == ModeReglement.Credit)
        {
            var f = new Facture
            {
                Sens = SensFacture.Client, Type = TypeFacture.Facture, Origine = OrigineFacture.Articles, ClientId = client!.Id, Date = jour,
                Echeance = jour.AddDays(client.DelaiPaiementJours), Commentaire = VenteRef(v), Statut = StatutDocument.Valide, ValideLe = maintenant, ValidePar = courant.Id,
                Numero = await numerotation.ProchainAsync("FACTURE_CLIENT", ct), TotalHt = v.TotalHt, TotalTva = v.TotalTva, TotalTtc = v.TotalTtc
            };
            foreach (var l in v.Lignes)
                f.Lignes.Add(new LigneFacture
                {
                    FactureId = f.Id, ProduitId = l.ProduitId, Designation = (await db.Produits.FindAsync([l.ProduitId], ct))!.Designation, Quantite = l.Quantite,
                    PrixUnitaireHt = CalculsTaxes.PrixHt(l.PrixUnitaireTtc, l.TauxTva), TauxTva = l.TauxTva, MontantHt = l.MontantHt, MontantTva = l.MontantTva, MontantTtc = l.MontantTtc
                });
            db.Factures.Add(f);
            var encours = await soldes.CreanceClientAsync(client.Id, ct) + await soldes.BonsNonFacturesAsync(client.Id, ct) + v.TotalTtc;
            if (client.PlafondCredit > 0 && encours > client.PlafondCredit)
                audit.Enregistrer("DEPASSEMENT_PLAFOND", "Client", client.Id, client.Code, null, null, new { encours, client.PlafondCredit, Vente = v.Numero });
        }
        else
        {
            var compte = await tresorerie.CompteAsync(dto.CompteId, dto.ModeReglement, ct);
            v.CompteId = compte.Id;
            await tresorerie.EcrireAsync(compte.Id, v.TotalTtc, NatureMouvementFinancier.EncaissementVenteArticle, $"Vente {v.Numero} — {dto.ModeReglement}", jour,
                m => { m.VenteId = v.Id; m.DocumentRef = v.Numero; }, ct);
        }
        await db.SaveChangesAsync(ct);
        return await VenteAsync(v.Id, ct);
    }

    /// <summary>Annulation par contre-passation du stock et de l'encaissement ; la facture de crédit est annulée si elle n'est pas réglée.</summary>
    public async Task<VenteDto> AnnulerAsync(Guid id, string? motif, CancellationToken ct)
    {
        var m = Garde.Motif(motif, "l'annulation d'une vente");
        await db.VerrouillerAsync("ventes_articles", id, ct);
        var v = Garde.Trouve(await db.VentesArticles.FirstOrDefaultAsync(x => x.Id == id, ct), "Vente");
        await db.Entry(v).ReloadAsync(ct);
        if (v.Statut == StatutDocument.Annule) throw new ConflitException("Vente déjà annulée.");
        var facture = await db.Factures.FirstOrDefaultAsync(f => f.Origine == OrigineFacture.Articles && f.Commentaire == VenteRef(v) && f.Statut == StatutDocument.Valide, ct);
        if (facture is not null)
        {
            var s = await soldes.FactureAsync(facture.Id, ct);
            if (s.Regle > 0 || s.AvoirsImputes > 0) throw new RegleMetierException("La facture de cette vente a reçu un règlement ou un avoir : établissez un avoir plutôt qu'une annulation.");
            facture.Statut = StatutDocument.Annule; facture.AnnuleLe = horloge.Maintenant; facture.MotifAnnulation = m;
        }
        foreach (var mvt in await db.MouvementsStock.Where(x => x.DocumentType == TypeDocument && x.DocumentId == id && x.Type != TypeMouvementStock.ContrePassation).ToListAsync(ct))
            await stock.ContrePasserAsync(mvt, m, ct);
        foreach (var mf in await db.MouvementsFinanciers.Where(x => x.VenteId == id && x.Nature != NatureMouvementFinancier.ContrePassation).ToListAsync(ct))
            await tresorerie.ContrePasserAsync(mf, m, ct);
        v.Statut = StatutDocument.Annule; v.AnnuleLe = horloge.Maintenant; v.AnnulePar = courant.Id; v.MotifAnnulation = m;
        audit.Enregistrer("ANNULATION_VENTE", "VenteArticle", v.Id, v.Numero, m, new { Statut = "Valide", v.TotalTtc }, new { Statut = "Annule" });
        await db.SaveChangesAsync(ct);
        return await VenteAsync(id, ct);
    }
}
