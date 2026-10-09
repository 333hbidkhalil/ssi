using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

/// <summary>Mouvement de stock append-only. Le stock est la somme des mouvements (R-09).</summary>
public class MouvementStock
{
    public long Id { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public Guid ProduitId { get; set; }
    /// <summary>Cuve concernée ; null = magasin (lubrifiants et annexes).</summary>
    public Guid? CuveId { get; set; }
    public TypeMouvementStock Type { get; set; }
    public decimal Quantite { get; set; }
    public decimal CoutUnitaire { get; set; }
    public decimal Valeur { get; set; }
    public decimal QuantiteApres { get; set; }
    public decimal CmpApres { get; set; }
    public string? DocumentType { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentRef { get; set; }
    public string? Motif { get; set; }
    public Guid? CreePar { get; set; }
    public DateTimeOffset CreeLe { get; set; }
}

/// <summary>Position courante (cache recalculable depuis les mouvements, verrouillée pendant les écritures).</summary>
public class PositionStock
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProduitId { get; set; }
    public Guid? CuveId { get; set; }
    public decimal Quantite { get; set; }
    public decimal Valeur { get; set; }
    public decimal Cmp { get; set; }
    public long DernierMouvementId { get; set; }
}

public class Jaugeage : Entite
{
    public Guid CuveId { get; set; }
    public Cuve? Cuve { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public decimal VolumeMesure { get; set; }
    public decimal? HauteurCm { get; set; }
    public decimal? TemperatureC { get; set; }
    public MethodeMesure Methode { get; set; }
    public decimal StockTheorique { get; set; }
    public decimal EcartLitres { get; set; }
    public decimal? EcartPct { get; set; }
    public decimal ValeurIndicative { get; set; }
    public bool HorsTolerance { get; set; }
    public StatutJaugeage Statut { get; set; } = StatutJaugeage.Saisi;
    public CauseEcart Cause { get; set; } = CauseEcart.NonDeterminee;
    public string? Explication { get; set; }
}

public class AjustementStock : Document
{
    public Guid ProduitId { get; set; }
    public Guid? CuveId { get; set; }
    public decimal Quantite { get; set; }
    public string Motif { get; set; } = "";
    public CauseEcart Cause { get; set; }
    public Guid? JaugeageId { get; set; }
    public StatutDemande Statut { get; set; } = StatutDemande.Demande;
    public long? MouvementId { get; set; }
    public string? CommentaireDecision { get; set; }
}
