using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Domain.Calculs;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Application.Stocks;

public sealed record DemandeMouvement(
    Guid ProduitId, Guid? CuveId, TypeMouvementStock Type, decimal Quantite, decimal? CoutUnitaireEntree,
    string DocumentType, Guid? DocumentId, string? DocumentRef, string? Motif, DateTimeOffset? Horodatage = null);

/// <summary>
/// Seul point d'écriture des mouvements de stock. Verrouille la position (produit, cuve) pendant le calcul
/// du coût moyen pondéré pour garantir la cohérence sous accès concurrents.
/// </summary>
public class MoteurStock(ISsiDb db, IUtilisateurCourant utilisateur, IHorloge horloge)
{
    public async Task<PositionStock> VerrouillerPositionAsync(Guid produitId, Guid? cuveId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO positions_stock (id, produit_id, cuve_id, quantite, valeur, cmp, dernier_mouvement_id) VALUES ({Guid.CreateVersion7()}, {produitId}, {cuveId}, 0, 0, 0, 0) ON CONFLICT (produit_id, cuve_id) DO NOTHING", ct);
        var position = await db.PositionsStock
            .FromSqlInterpolated($"SELECT * FROM positions_stock WHERE produit_id = {produitId} AND cuve_id IS NOT DISTINCT FROM {cuveId} FOR UPDATE")
            .AsTracking().SingleAsync(ct);
        await db.Entry(position).ReloadAsync(ct);
        return position;
    }

    /// <summary>Écrit un mouvement (quantité signée) et met à jour la position. Doit être appelé dans une transaction.</summary>
    public async Task<MouvementStock> EcrireAsync(DemandeMouvement d, CancellationToken ct)
    {
        if (d.Quantite == 0) throw new RegleMetierException("Un mouvement de stock ne peut pas être nul.");
        var position = await VerrouillerPositionAsync(d.ProduitId, d.CuveId, ct);
        var avant = new PositionValorisee(position.Quantite, position.Cmp, position.Valeur);

        PositionValorisee apres;
        decimal cout, valeur;
        if (d.Quantite > 0)
        {
            cout = d.CoutUnitaireEntree ?? position.Cmp;
            apres = CalculsStock.Entree(avant, d.Quantite, cout);
            valeur = Arrondi.Montant(d.Quantite * cout);
        }
        else
        {
            var (p, valeurSortie) = CalculsStock.Sortie(avant, -d.Quantite);
            apres = p;
            cout = avant.Cmp;
            valeur = -valeurSortie;
        }

        var maintenant = horloge.Maintenant;
        var mvt = new MouvementStock
        {
            Horodatage = d.Horodatage ?? maintenant,
            ProduitId = d.ProduitId,
            CuveId = d.CuveId,
            Type = d.Type,
            Quantite = d.Quantite,
            CoutUnitaire = cout,
            Valeur = valeur,
            QuantiteApres = apres.Quantite,
            CmpApres = apres.Cmp,
            DocumentType = d.DocumentType,
            DocumentId = d.DocumentId,
            DocumentRef = d.DocumentRef,
            Motif = d.Motif,
            CreePar = utilisateur.Id,
            CreeLe = maintenant
        };
        db.MouvementsStock.Add(mvt);
        await db.SaveChangesAsync(ct);

        position.Quantite = apres.Quantite;
        position.Cmp = apres.Cmp;
        position.Valeur = position.Valeur + valeur;
        position.DernierMouvementId = mvt.Id;
        await db.SaveChangesAsync(ct);
        return mvt;
    }

    /// <summary>
    /// Mouvement de valeur seule (quantité nulle) : écart de prix d'achat constaté sur facture (H-07).
    /// La valeur s'ajoute à la position et le CMP est recalculé ; doit être appelé dans une transaction.
    /// </summary>
    public async Task<MouvementStock> EcrireValeurAsync(Guid produitId, Guid? cuveId, decimal valeur, string documentType, Guid? documentId,
        string? documentRef, string motif, CancellationToken ct)
    {
        if (valeur == 0) throw new RegleMetierException("Un ajustement de valeur ne peut pas être nul.");
        var position = await VerrouillerPositionAsync(produitId, cuveId, ct);
        var nouvelleValeur = position.Valeur + valeur;
        var cmp = position.Quantite > 0 ? Arrondi.CoutUnitaire(nouvelleValeur / position.Quantite) : position.Cmp;
        if (cmp < 0) throw new RegleMetierException("L'ajustement de valeur rendrait le coût moyen négatif.");
        var maintenant = horloge.Maintenant;
        var mvt = new MouvementStock
        {
            Horodatage = maintenant, ProduitId = produitId, CuveId = cuveId, Type = TypeMouvementStock.AjustementValeur, Quantite = 0,
            CoutUnitaire = 0, Valeur = valeur, QuantiteApres = position.Quantite, CmpApres = cmp, DocumentType = documentType,
            DocumentId = documentId, DocumentRef = documentRef, Motif = motif, CreePar = utilisateur.Id, CreeLe = maintenant
        };
        db.MouvementsStock.Add(mvt);
        await db.SaveChangesAsync(ct);
        position.Valeur = nouvelleValeur;
        position.Cmp = cmp;
        position.DernierMouvementId = mvt.Id;
        await db.SaveChangesAsync(ct);
        return mvt;
    }

    /// <summary>Contre-passe un mouvement existant (correction traçable) au coût d'origine.</summary>
    public async Task<MouvementStock> ContrePasserAsync(MouvementStock origine, string motif, CancellationToken ct)
    {
        var position = await VerrouillerPositionAsync(origine.ProduitId, origine.CuveId, ct);
        var quantite = -origine.Quantite;
        var valeur = -origine.Valeur;
        var nouvelleQuantite = position.Quantite + quantite;
        var nouvelleValeur = position.Valeur + valeur;
        var cmp = nouvelleQuantite > 0 ? Arrondi.CoutUnitaire(nouvelleValeur / nouvelleQuantite) : position.Cmp;
        if (cmp < 0) cmp = position.Cmp;
        var maintenant = horloge.Maintenant;
        var mvt = new MouvementStock
        {
            Horodatage = maintenant, ProduitId = origine.ProduitId, CuveId = origine.CuveId,
            Type = TypeMouvementStock.ContrePassation, Quantite = quantite, CoutUnitaire = origine.CoutUnitaire,
            Valeur = valeur, QuantiteApres = nouvelleQuantite, CmpApres = cmp, DocumentType = origine.DocumentType,
            DocumentId = origine.DocumentId, DocumentRef = $"Contre-passation mvt {origine.Id}", Motif = motif,
            CreePar = utilisateur.Id, CreeLe = maintenant
        };
        db.MouvementsStock.Add(mvt);
        await db.SaveChangesAsync(ct);
        position.Quantite = nouvelleQuantite;
        position.Valeur = nouvelleValeur;
        position.Cmp = cmp;
        position.DernierMouvementId = mvt.Id;
        await db.SaveChangesAsync(ct);
        return mvt;
    }

    /// <summary>Stock théorique à un instant (somme des mouvements, R-09).</summary>
    public Task<decimal> StockAsync(Guid produitId, Guid? cuveId, DateTimeOffset? jusqua = null, CancellationToken ct = default)
        => db.MouvementsStock.Where(m => m.ProduitId == produitId && m.CuveId == cuveId && (jusqua == null || m.Horodatage <= jusqua))
            .SumAsync(m => m.Quantite, ct);
}
