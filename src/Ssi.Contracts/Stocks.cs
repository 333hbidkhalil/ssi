namespace Ssi.Contracts;

public sealed record EtatCuveDto(Guid CuveId, string Code, string Produit, decimal Capacite, decimal SeuilMinimal, decimal StockTheorique,
    decimal TauxRemplissagePct, bool SousSeuil, decimal? Cmp, decimal? Valeur, decimal? JoursCouverture,
    DateTimeOffset? DernierJaugeage, decimal? DernierVolumeMesure, decimal? DernierEcartLitres, decimal? DernierEcartPct, bool DernierEcartHorsTolerance);

public sealed record MouvementStockDto(long Id, DateTimeOffset Horodatage, string Produit, string? Cuve, TypeMouvementStock Type, decimal Quantite,
    decimal? CoutUnitaire, decimal? Valeur, decimal QuantiteApres, string? DocumentRef, string? Motif);

public sealed record StockInitialDto(Guid ProduitId, Guid? CuveId, decimal Quantite, decimal CoutUnitaireHt, DateTimeOffset? Date, string Motif);

public sealed record SaisieJaugeageDto(Guid CuveId, DateTimeOffset? Horodatage, decimal VolumeMesure, decimal? HauteurCm, decimal? TemperatureC,
    MethodeMesure Methode);
public sealed record JaugeageDto(Guid Id, Guid CuveId, string Cuve, DateTimeOffset Horodatage, decimal VolumeMesure, decimal? HauteurCm,
    decimal? TemperatureC, MethodeMesure Methode, decimal StockTheorique, decimal EcartLitres, decimal? EcartPct, decimal? ValeurIndicative,
    bool HorsTolerance, StatutJaugeage Statut, CauseEcart Cause, string? Explication, string SaisiPar, IReadOnlyList<JaugeageResumeDto> HistoriqueRecent);
public sealed record JaugeageResumeDto(DateTimeOffset Horodatage, decimal VolumeMesure, decimal StockTheorique, decimal EcartLitres, decimal? EcartPct);
public sealed record ExplicationEcartDto(CauseEcart Cause, string Explication);

public sealed record DemandeAjustementDto(Guid ProduitId, Guid? CuveId, decimal Quantite, CauseEcart Cause, string Motif, Guid? JaugeageId);
public sealed record AjustementDto(Guid Id, string Numero, string Produit, string? Cuve, decimal Quantite, CauseEcart Cause, string Motif,
    StatutDemande Statut, string DemandePar, string? DecidePar, DateTimeOffset CreeLe, string? CommentaireDecision);

public sealed record TransfertStockDto(Guid ProduitId, Guid? CuveSourceId, Guid? CuveDestinationId, decimal Quantite, string Motif);

public sealed record PositionArticleDto(Guid ProduitId, string Code, string Designation, string Famille, string Unite, decimal Quantite,
    decimal SeuilAlerte, bool SousSeuil, decimal? Cmp, decimal? Valeur);

public sealed record InventaireArticleDto(Guid ProduitId, decimal QuantiteComptee, string Motif);

public sealed record ControleCoherenceDto(string Objet, string Libelle, decimal ValeurEnregistree, decimal ValeurRecalculee, bool Coherent);
