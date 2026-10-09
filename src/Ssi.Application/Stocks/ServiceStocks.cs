using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Stocks;

/// <summary>Cuves, jaugeages, écarts, ajustements autorisés, transferts et stock d'articles (§18-§20).</summary>
public class ServiceStocks(ISsiDb db, IHorloge horloge, Audit audit, IUtilisateurCourant courant, INumerotation numerotation, MoteurStock moteur)
{
    private bool VoirValeurs => courant.APermission(P.StockSensible);

    public async Task<IReadOnlyList<EtatCuveDto>> EtatCuvesAsync(CancellationToken ct)
    {
        var cuves = await db.Cuves.AsNoTracking().Include(c => c.Produit).Where(c => c.Actif).OrderBy(c => c.Code).ToListAsync(ct);
        var positions = await db.PositionsStock.AsNoTracking().Where(p => p.CuveId != null).ToDictionaryAsync(p => p.CuveId!.Value, ct);
        var depuis = horloge.Maintenant.AddDays(-7);
        var distribue = await db.MouvementsStock.AsNoTracking()
            .Where(m => m.CuveId != null && m.Type == TypeMouvementStock.Distribution && m.Horodatage >= depuis)
            .GroupBy(m => m.CuveId).Select(g => new { Cuve = g.Key!.Value, Volume = -g.Sum(m => m.Quantite) }).ToDictionaryAsync(x => x.Cuve, x => x.Volume, ct);
        var resultat = new List<EtatCuveDto>();
        foreach (var c in cuves)
        {
            var stock = await moteur.StockAsync(c.ProduitId, c.Id, null, ct);
            positions.TryGetValue(c.Id, out var pos);
            var dernier = await db.Jaugeages.AsNoTracking().Where(j => j.CuveId == c.Id).OrderByDescending(j => j.Horodatage).FirstOrDefaultAsync(ct);
            var couverture = CalculsStock.JoursCouverture(stock, distribue.GetValueOrDefault(c.Id), 7);
            resultat.Add(new EtatCuveDto(c.Id, c.Code, c.Produit!.Designation, c.Capacite, c.SeuilMinimal, stock,
                Arrondi.Pourcentage(stock / c.Capacite * 100), stock < c.SeuilMinimal,
                VoirValeurs ? pos?.Cmp : null, VoirValeurs ? Arrondi.Montant(stock * (pos?.Cmp ?? 0)) : null, couverture.Valeur,
                dernier?.Horodatage, dernier?.VolumeMesure, dernier?.EcartLitres, dernier?.EcartPct, dernier?.HorsTolerance ?? false));
        }
        return resultat;
    }

    public async Task<Page<MouvementStockDto>> MouvementsAsync(Guid? produitId, Guid? cuveId, DateTimeOffset? du, DateTimeOffset? au, int page, int taille, CancellationToken ct)
    {
        var q = db.MouvementsStock.AsNoTracking().AsQueryable();
        if (produitId.HasValue) q = q.Where(m => m.ProduitId == produitId);
        if (cuveId.HasValue) q = q.Where(m => m.CuveId == cuveId);
        if (du.HasValue) q = q.Where(m => m.Horodatage >= du);
        if (au.HasValue) q = q.Where(m => m.Horodatage <= au);
        var total = await q.CountAsync(ct);
        taille = Math.Clamp(taille, 1, 500);
        var mvts = await q.OrderByDescending(m => m.Id).Skip((Math.Max(page, 1) - 1) * taille).Take(taille).ToListAsync(ct);
        var produits = await db.Produits.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Designation, ct);
        var cuves = await db.Cuves.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var v = VoirValeurs;
        return new Page<MouvementStockDto>(mvts.Select(m => new MouvementStockDto(m.Id, m.Horodatage, produits.GetValueOrDefault(m.ProduitId, "?"),
            m.CuveId.HasValue ? cuves.GetValueOrDefault(m.CuveId.Value) : null, m.Type, m.Quantite, v ? m.CoutUnitaire : null, v ? m.Valeur : null,
            m.QuantiteApres, m.DocumentRef, m.Motif)).ToList(), total, page, taille);
    }

    private async Task<(Produit Produit, Cuve? Cuve)> ControlerEmplacementAsync(Guid produitId, Guid? cuveId, CancellationToken ct)
    {
        var produit = Garde.Trouve(await db.Produits.Include(p => p.Famille).FirstOrDefaultAsync(p => p.Id == produitId, ct), "Produit");
        Cuve? cuve = null;
        if (produit.Famille!.Categorie == CategorieProduit.Carburant)
        {
            if (cuveId is null) throw new ValidationException("Le stock d'un carburant est tenu par cuve : précisez la cuve.");
            cuve = Garde.Trouve(await db.Cuves.FindAsync([cuveId.Value], ct), "Cuve");
            if (cuve.ProduitId != produitId) throw new ValidationException($"La cuve {cuve.Code} ne contient pas ce produit.");
        }
        else if (cuveId is not null) throw new ValidationException("Un article (lubrifiant, annexe) est stocké au magasin, pas en cuve.");
        return (produit, cuve);
    }

    public async Task<MouvementStockDto> StockInitialAsync(StockInitialDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "la saisie d'un stock initial");
        Garde.Positif(dto.Quantite, "quantite");
        Garde.PositifOuNul(dto.CoutUnitaireHt, "coutUnitaireHt");
        var (produit, cuve) = await ControlerEmplacementAsync(dto.ProduitId, dto.CuveId, ct);
        await moteur.VerrouillerPositionAsync(dto.ProduitId, dto.CuveId, ct);
        if (await db.MouvementsStock.AnyAsync(m => m.ProduitId == dto.ProduitId && m.CuveId == dto.CuveId, ct))
            throw new RegleMetierException("Des mouvements existent déjà : le stock initial ne peut plus être saisi. Utilisez un ajustement autorisé.");
        if (cuve is not null && dto.Quantite > cuve.Capacite) throw new RegleMetierException($"La quantité dépasse la capacité de la cuve ({cuve.Capacite:N0} L).");
        var mvt = await moteur.EcrireAsync(new DemandeMouvement(dto.ProduitId, dto.CuveId, TypeMouvementStock.StockInitial, dto.Quantite, dto.CoutUnitaireHt,
            "STOCK_INITIAL", null, "Stock initial", motif, dto.Date), ct);
        audit.Enregistrer("AJUSTEMENT_STOCK", "Stock", produit.Id, cuve?.Code ?? produit.Code, motif, null, new { Type = "Stock initial", dto.Quantite, dto.CoutUnitaireHt });
        await db.SaveChangesAsync(ct);
        return (await MouvementsAsync(dto.ProduitId, dto.CuveId, null, null, 1, 1, ct)).Elements.First(m => m.Id == mvt.Id);
    }

    // ---------- Jaugeages ----------

    public async Task<JaugeageDto> SaisirJaugeageAsync(SaisieJaugeageDto dto, CancellationToken ct)
    {
        Garde.PositifOuNul(dto.VolumeMesure, "volumeMesure");
        var cuve = Garde.Trouve(await db.Cuves.FindAsync([dto.CuveId], ct), "Cuve");
        var instant = dto.Horodatage ?? horloge.Maintenant;
        if (instant > horloge.Maintenant.AddMinutes(5)) throw new ValidationException("Un jaugeage ne peut pas être daté dans le futur.");
        if (dto.VolumeMesure > cuve.Capacite * 1.05m) throw new ValidationException($"Le volume mesuré dépasse la capacité de la cuve ({cuve.Capacite:N0} L) : vérifiez la saisie.");
        var theorique = await moteur.StockAsync(cuve.ProduitId, cuve.Id, instant, ct);
        var cmp = await db.PositionsStock.Where(p => p.ProduitId == cuve.ProduitId && p.CuveId == cuve.Id).Select(p => (decimal?)p.Cmp).FirstOrDefaultAsync(ct) ?? 0;
        var ecart = CalculsStock.Ecart(dto.VolumeMesure, theorique, cmp, cuve.ToleranceEcartLitres, cuve.ToleranceEcartPct);
        var j = new Jaugeage
        {
            CuveId = cuve.Id, Horodatage = instant, VolumeMesure = dto.VolumeMesure, HauteurCm = dto.HauteurCm, TemperatureC = dto.TemperatureC,
            Methode = dto.Methode, StockTheorique = theorique, EcartLitres = ecart.EcartLitres, EcartPct = ecart.EcartPourcentage,
            ValeurIndicative = ecart.ValeurIndicative, HorsTolerance = ecart.HorsTolerance
        };
        db.Jaugeages.Add(j);
        await db.SaveChangesAsync(ct);
        return await JaugeageAsync(j.Id, ct);
    }

    public async Task<JaugeageDto> JaugeageAsync(Guid id, CancellationToken ct)
    {
        var j = Garde.Trouve(await db.Jaugeages.AsNoTracking().Include(x => x.Cuve).FirstOrDefaultAsync(x => x.Id == id, ct), "Jaugeage");
        var historique = await db.Jaugeages.AsNoTracking().Where(x => x.CuveId == j.CuveId && x.Horodatage < j.Horodatage)
            .OrderByDescending(x => x.Horodatage).Take(10)
            .Select(x => new JaugeageResumeDto(x.Horodatage, x.VolumeMesure, x.StockTheorique, x.EcartLitres, x.EcartPct)).ToListAsync(ct);
        var saisiPar = await db.Utilisateurs.Where(u => u.Id == j.CreePar).Select(u => u.Login).FirstOrDefaultAsync(ct) ?? "?";
        return new JaugeageDto(j.Id, j.CuveId, j.Cuve!.Code, j.Horodatage, j.VolumeMesure, j.HauteurCm, j.TemperatureC, j.Methode, j.StockTheorique,
            j.EcartLitres, j.EcartPct, VoirValeurs ? j.ValeurIndicative : null, j.HorsTolerance, j.Statut, j.Cause, j.Explication, saisiPar, historique);
    }

    public async Task<IReadOnlyList<JaugeageDto>> JaugeagesAsync(Guid? cuveId, bool? horsTolerance, CancellationToken ct)
    {
        var ids = await db.Jaugeages.AsNoTracking().Where(j => (cuveId == null || j.CuveId == cuveId) && (horsTolerance == null || j.HorsTolerance == horsTolerance))
            .OrderByDescending(j => j.Horodatage).Take(100).Select(j => j.Id).ToListAsync(ct);
        var resultat = new List<JaugeageDto>();
        foreach (var id in ids) resultat.Add(await JaugeageAsync(id, ct));
        return resultat;
    }

    /// <summary>Explication d'un écart : distingue mesure, température, saisie et perte réelle. Ne modifie jamais le stock.</summary>
    public async Task<JaugeageDto> ExpliquerAsync(Guid id, ExplicationEcartDto dto, CancellationToken ct)
    {
        Garde.Requis(dto.Explication, "explication");
        var j = Garde.Trouve(await db.Jaugeages.FindAsync([id], ct), "Jaugeage");
        if (j.Statut == StatutJaugeage.Regularise) throw new ConflitException("Cet écart a déjà été régularisé.");
        var avant = new { j.Cause, j.Explication };
        j.Cause = dto.Cause;
        j.Explication = dto.Explication.Trim();
        j.Statut = StatutJaugeage.Explique;
        audit.Enregistrer("EXPLICATION_ECART_STOCK", "Jaugeage", j.Id, null, dto.Explication, avant, new { dto.Cause, dto.Explication });
        await db.SaveChangesAsync(ct);
        return await JaugeageAsync(id, ct);
    }

    // ---------- Ajustements (demande puis autorisation par une autre personne) ----------

    public async Task<AjustementDto> DemanderAjustementAsync(DemandeAjustementDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "un ajustement de stock");
        if (dto.Quantite == 0) throw new ValidationException("La quantité d'ajustement ne peut pas être nulle.");
        await ControlerEmplacementAsync(dto.ProduitId, dto.CuveId, ct);
        if (dto.JaugeageId.HasValue)
        {
            var j = Garde.Trouve(await db.Jaugeages.FindAsync([dto.JaugeageId.Value], ct), "Jaugeage");
            if (j.CuveId != dto.CuveId) throw new ValidationException("Le jaugeage ne concerne pas cette cuve.");
            if (j.Statut == StatutJaugeage.Regularise) throw new ConflitException("Cet écart a déjà été régularisé.");
        }
        var a = new AjustementStock
        {
            Numero = await numerotation.ProchainAsync("AJUSTEMENT", ct), ProduitId = dto.ProduitId, CuveId = dto.CuveId, Quantite = dto.Quantite,
            Motif = motif, Cause = dto.Cause, JaugeageId = dto.JaugeageId
        };
        db.AjustementsStock.Add(a);
        audit.Enregistrer("DEMANDE_AJUSTEMENT_STOCK", "AjustementStock", a.Id, a.Numero, motif, null, new { dto.Quantite, dto.Cause, dto.CuveId });
        await db.SaveChangesAsync(ct);
        return (await AjustementsAsync(null, ct)).First(x => x.Id == a.Id);
    }

    public async Task<IReadOnlyList<AjustementDto>> AjustementsAsync(StatutDemande? statut, CancellationToken ct)
    {
        var liste = await db.AjustementsStock.AsNoTracking().Where(a => statut == null || a.Statut == statut).OrderByDescending(a => a.CreeLe).Take(200).ToListAsync(ct);
        var produits = await db.Produits.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Designation, ct);
        var cuves = await db.Cuves.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var users = await db.Utilisateurs.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Login, ct);
        return liste.Select(a => new AjustementDto(a.Id, a.Numero, produits.GetValueOrDefault(a.ProduitId, "?"), a.CuveId.HasValue ? cuves.GetValueOrDefault(a.CuveId.Value) : null,
            a.Quantite, a.Cause, a.Motif, a.Statut, a.CreePar.HasValue ? users.GetValueOrDefault(a.CreePar.Value, "?") : "?",
            a.ValidePar.HasValue ? users.GetValueOrDefault(a.ValidePar.Value) : null, a.CreeLe, a.CommentaireDecision)).ToList();
    }

    public async Task<AjustementDto> DeciderAjustementAsync(Guid id, DecisionDto dto, CancellationToken ct)
    {
        await db.VerrouillerAsync("ajustements_stock", id, ct);
        var a = Garde.Trouve(await db.AjustementsStock.FirstOrDefaultAsync(x => x.Id == id, ct), "Ajustement");
        await db.Entry(a).ReloadAsync(ct);
        if (a.Statut != StatutDemande.Demande) throw new ConflitException($"L'ajustement {a.Numero} a déjà été traité.");
        if (a.CreePar == courant.Id) throw new AccesRefuseException("Un ajustement doit être autorisé par une autre personne que le demandeur.");
        a.ValidePar = courant.Id;
        a.ValideLe = horloge.Maintenant;
        a.CommentaireDecision = dto.Commentaire;
        if (dto.Approuve)
        {
            var avant = await moteur.StockAsync(a.ProduitId, a.CuveId, null, ct);
            var mvt = await moteur.EcrireAsync(new DemandeMouvement(a.ProduitId, a.CuveId, TypeMouvementStock.Ajustement, a.Quantite, null,
                "AJUSTEMENT", a.Id, a.Numero, $"{a.Cause} : {a.Motif}"), ct);
            a.MouvementId = mvt.Id;
            a.Statut = StatutDemande.Approuve;
            if (a.JaugeageId.HasValue)
            {
                var j = await db.Jaugeages.FirstAsync(x => x.Id == a.JaugeageId, ct);
                j.Statut = StatutJaugeage.Regularise;
                if (j.Cause == CauseEcart.NonDeterminee) j.Cause = a.Cause;
            }
            audit.Enregistrer("AJUSTEMENT_STOCK", "AjustementStock", a.Id, a.Numero, a.Motif, new { Stock = avant }, new { Stock = avant + a.Quantite, a.Quantite });
        }
        else
        {
            a.Statut = StatutDemande.Rejete;
            audit.Enregistrer("REJET_AJUSTEMENT_STOCK", "AjustementStock", a.Id, a.Numero, dto.Commentaire);
        }
        await db.SaveChangesAsync(ct);
        return (await AjustementsAsync(null, ct)).First(x => x.Id == a.Id);
    }

    // ---------- Transferts et articles ----------

    public async Task TransfererAsync(TransfertStockDto dto, CancellationToken ct)
    {
        var motif = Garde.Motif(dto.Motif, "un transfert de stock");
        Garde.Positif(dto.Quantite, "quantite");
        if (dto.CuveSourceId == dto.CuveDestinationId) throw new ValidationException("La source et la destination doivent être différentes.");
        await ControlerEmplacementAsync(dto.ProduitId, dto.CuveSourceId, ct);
        var (_, dest) = await ControlerEmplacementAsync(dto.ProduitId, dto.CuveDestinationId, ct);
        var reference = $"TRF-{horloge.Maintenant:yyyyMMddHHmmss}";
        var sortie = await moteur.EcrireAsync(new DemandeMouvement(dto.ProduitId, dto.CuveSourceId, TypeMouvementStock.TransfertSortant, -dto.Quantite, null,
            "TRANSFERT", null, reference, motif), ct);
        await moteur.EcrireAsync(new DemandeMouvement(dto.ProduitId, dto.CuveDestinationId, TypeMouvementStock.TransfertEntrant, dto.Quantite, sortie.CoutUnitaire,
            "TRANSFERT", null, reference, motif), ct);
        if (dest is not null && await moteur.StockAsync(dto.ProduitId, dest.Id, null, ct) > dest.Capacite)
            throw new RegleMetierException($"Le transfert dépasserait la capacité de la cuve {dest.Code}.");
        audit.Enregistrer("TRANSFERT_STOCK", "Stock", dto.ProduitId, reference, motif, null, dto);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PositionArticleDto>> ArticlesAsync(CancellationToken ct)
    {
        var produits = await db.Produits.AsNoTracking().Include(p => p.Famille).Where(p => p.Famille!.Categorie != CategorieProduit.Carburant)
            .OrderBy(p => p.Code).ToListAsync(ct);
        var positions = await db.PositionsStock.AsNoTracking().Where(p => p.CuveId == null).ToDictionaryAsync(p => p.ProduitId, ct);
        var v = VoirValeurs;
        return produits.Select(p =>
        {
            positions.TryGetValue(p.Id, out var pos);
            var q = pos?.Quantite ?? 0;
            return new PositionArticleDto(p.Id, p.Code, p.Designation, p.Famille!.Libelle, p.Unite, q, p.SeuilAlerte, q <= p.SeuilAlerte,
                v ? pos?.Cmp : null, v ? pos?.Valeur : null);
        }).ToList();
    }

    /// <summary>Inventaire d'un article : l'écart compté/théorique devient une demande d'ajustement à autoriser.</summary>
    public async Task<AjustementDto?> InventaireAsync(InventaireArticleDto dto, CancellationToken ct)
    {
        Garde.PositifOuNul(dto.QuantiteComptee, "quantiteComptee");
        await ControlerEmplacementAsync(dto.ProduitId, null, ct);
        var theorique = await moteur.StockAsync(dto.ProduitId, null, null, ct);
        var ecart = dto.QuantiteComptee - theorique;
        if (ecart == 0) return null;
        return await DemanderAjustementAsync(new DemandeAjustementDto(dto.ProduitId, null, ecart, CauseEcart.NonDeterminee,
            $"Inventaire : compté {dto.QuantiteComptee}, théorique {theorique}. {dto.Motif}", null), ct);
    }

    /// <summary>R-38 : chaque position en cache doit égaler la somme de ses mouvements.</summary>
    public async Task<IReadOnlyList<ControleCoherenceDto>> ControlerAsync(CancellationToken ct)
    {
        var sommes = await db.MouvementsStock.AsNoTracking().GroupBy(m => new { m.ProduitId, m.CuveId })
            .Select(g => new { g.Key.ProduitId, g.Key.CuveId, Quantite = g.Sum(m => m.Quantite), Valeur = g.Sum(m => m.Valeur) }).ToListAsync(ct);
        var positions = await db.PositionsStock.AsNoTracking().ToListAsync(ct);
        var produits = await db.Produits.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var cuves = await db.Cuves.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var res = new List<ControleCoherenceDto>();
        foreach (var p in positions)
        {
            var s = sommes.FirstOrDefault(x => x.ProduitId == p.ProduitId && x.CuveId == p.CuveId);
            var lib = $"{produits.GetValueOrDefault(p.ProduitId)} {(p.CuveId.HasValue ? cuves.GetValueOrDefault(p.CuveId.Value) : "magasin")}";
            res.Add(new ControleCoherenceDto("Stock (quantité)", lib, p.Quantite, s?.Quantite ?? 0, p.Quantite == (s?.Quantite ?? 0)));
            res.Add(new ControleCoherenceDto("Stock (valeur)", lib, p.Valeur, s?.Valeur ?? 0, p.Valeur == (s?.Valeur ?? 0)));
        }
        return res;
    }
}
