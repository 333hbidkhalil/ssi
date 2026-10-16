using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class Cuve : Entite
{
    public string Code { get; set; } = "";
    public Guid ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public decimal Capacite { get; set; }
    public decimal SeuilMinimal { get; set; }
    public decimal ToleranceEcartLitres { get; set; } = 100m;
    public decimal ToleranceEcartPct { get; set; } = 0.5m;
    public bool Actif { get; set; } = true;
    public string? Observations { get; set; }
}

public class Volucompteur : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public bool Actif { get; set; } = true;
    public List<Pistolet> Pistolets { get; set; } = [];
}

/// <summary>Index indépendant (pistolet / totalisateur) d'un volucompteur.</summary>
public class Pistolet : Entite
{
    public Guid VolucompteurId { get; set; }
    public Volucompteur? Volucompteur { get; set; }
    public string Code { get; set; } = "";
    public Guid ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public Guid CuveId { get; set; }
    public Cuve? Cuve { get; set; }
    public decimal IndexMaximum { get; set; } = 999_999.99m;
    /// <summary>Index de départ du cycle courant (après installation, remise à zéro ou remplacement).</summary>
    public decimal IndexInitialCycle { get; set; }
    /// <summary>Numéro de cycle du totalisateur, incrémenté à chaque remise à zéro / remplacement validé.</summary>
    public int Cycle { get; set; } = 1;
    public bool Actif { get; set; } = true;
}

public class PosteTravail : Document
{
    public DateTimeOffset Debut { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public DateOnly JourneeExploitation { get; set; }
    public StatutPoste Statut { get; set; } = StatutPoste.Ouvert;
    public Guid? ResponsableId { get; set; }
    public Guid? CloturePar { get; set; }
    public DateTimeOffset? ClotureLe { get; set; }
    public decimal MontantTheoriqueTtc { get; set; }
    public decimal TotalReglements { get; set; }
    public decimal Ecart { get; set; }
    public string? Commentaire { get; set; }
    public int NombreReouvertures { get; set; }
    public List<ReleveIndex> Releves { get; set; } = [];
    public List<ReglementPoste> Reglements { get; set; } = [];
}

public class ReleveIndex : Entite
{
    public Guid PosteId { get; set; }
    public PosteTravail? Poste { get; set; }
    public Guid PistoletId { get; set; }
    public Pistolet? Pistolet { get; set; }
    public int Cycle { get; set; }
    public Guid? OperateurId { get; set; }
    public decimal IndexDebut { get; set; }
    public decimal IndexFin { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public decimal PrixUnitaireTtc { get; set; }
    public Guid? PrixProduitId { get; set; }
    public decimal TauxTva { get; set; }
    public decimal Volume { get; set; }
    public decimal MontantTtc { get; set; }
    public decimal MontantHt { get; set; }
    public StatutReleve Statut { get; set; } = StatutReleve.Saisi;
    public string? Anomalies { get; set; }
    public string? Commentaire { get; set; }
    /// <summary>Correction de continuité acceptée avec motif (index début ≠ fin précédente).</summary>
    public string? MotifCorrection { get; set; }
    /// <summary>Coût unitaire de sortie (CMP de la cuve) fixé à la validation.</summary>
    public decimal? CoutUnitaire { get; set; }
}

public class EvenementCompteur : Entite
{
    public Guid PistoletId { get; set; }
    public TypeEvenementCompteur Type { get; set; }
    public decimal AncienIndex { get; set; }
    public decimal NouvelIndex { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public string Motif { get; set; } = "";
    public StatutDemande Statut { get; set; } = StatutDemande.Demande;
    public Guid? ValidePar { get; set; }
    public DateTimeOffset? ValideLe { get; set; }
}

/// <summary>Ventilation des règlements d'un poste (espèces, carte, bons de crédit…). Ne crée jamais de vente.</summary>
public class ReglementPoste : Entite
{
    public Guid PosteId { get; set; }
    public ModeReglement Mode { get; set; }
    public decimal Montant { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid? ProduitId { get; set; }
    public decimal? Volume { get; set; }
    /// <summary>Numéro du bon de livraison (bon de crédit) ou de la pièce du règlement.</summary>
    public string? Reference { get; set; }
    /// <summary>Numéro du bon de commande du client, quand il en remet un.</summary>
    public string? ReferenceCommande { get; set; }
    public Guid? OperateurId { get; set; }
    /// <summary>Facture récapitulative ayant regroupé ce bon de crédit.</summary>
    public Guid? FactureId { get; set; }
}
