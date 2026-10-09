using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class CommandeFournisseur : Document
{
    public Guid FournisseurId { get; set; }
    public Fournisseur? Fournisseur { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly? DatePrevue { get; set; }
    public DestinationAchat Destination { get; set; }
    public Guid? CommandeClientId { get; set; }
    public string? ReferenceFournisseur { get; set; }
    public string? Conditions { get; set; }
    public decimal FraisPrevusHt { get; set; }
    public StatutCommande Statut { get; set; } = StatutCommande.Brouillon;
    public bool Confirmee { get; set; }
    public List<LigneCommandeFournisseur> Lignes { get; set; } = [];
}

public class LigneCommandeFournisseur
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CommandeId { get; set; }
    public Guid ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public Guid? CuveId { get; set; }
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHt { get; set; }
}

public class Reception : Document
{
    public Guid? CommandeId { get; set; }
    public Guid FournisseurId { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public string? NumeroBl { get; set; }
    public string? Transporteur { get; set; }
    public string? Vehicule { get; set; }
    public StatutDocument Statut { get; set; } = StatutDocument.Brouillon;
    public List<LigneReception> Lignes { get; set; } = [];
}

public class LigneReception
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ReceptionId { get; set; }
    public Guid ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public Guid? CuveId { get; set; }
    public decimal QuantiteCommandee { get; set; }
    public decimal QuantiteChargee { get; set; }
    public decimal QuantiteRecue { get; set; }
    public decimal QuantiteAcceptee { get; set; }
    public decimal PrixUnitaireHt { get; set; }
    public decimal FraisAttribuesHt { get; set; }
    public string? JustificationEcart { get; set; }
}

/// <summary>Vente d'articles (lubrifiants, produits annexes) avec sortie de stock au CMP.</summary>
public class VenteArticle : Document
{
    public DateTimeOffset Horodatage { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? PosteId { get; set; }
    public ModeReglement ModeReglement { get; set; }
    public Guid? CompteId { get; set; }
    public StatutDocument Statut { get; set; } = StatutDocument.Brouillon;
    public decimal TotalHt { get; set; }
    public decimal TotalTva { get; set; }
    public decimal TotalTtc { get; set; }
    public List<LigneVenteArticle> Lignes { get; set; } = [];
}

public class LigneVenteArticle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenteId { get; set; }
    public Guid ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireTtc { get; set; }
    public decimal TauxTva { get; set; }
    public decimal MontantHt { get; set; }
    public decimal MontantTva { get; set; }
    public decimal MontantTtc { get; set; }
    public decimal CoutUnitaire { get; set; }
}

public class Facture : Document
{
    public SensFacture Sens { get; set; }
    public TypeFacture Type { get; set; } = TypeFacture.Facture;
    public OrigineFacture Origine { get; set; }
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid? FournisseurId { get; set; }
    public Fournisseur? Fournisseur { get; set; }
    /// <summary>Numéro porté par la facture du fournisseur (contrôle de doublon).</summary>
    public string? NumeroTiers { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly Echeance { get; set; }
    public decimal TotalHt { get; set; }
    public decimal TotalTva { get; set; }
    public decimal TotalTtc { get; set; }
    /// <summary>Pour un avoir : facture d'origine.</summary>
    public Guid? FactureOrigineId { get; set; }
    public Guid? CommandeClientId { get; set; }
    public Guid? ReceptionId { get; set; }
    public StatutDocument Statut { get; set; } = StatutDocument.Brouillon;
    public string? Commentaire { get; set; }
    public List<LigneFacture> Lignes { get; set; } = [];

    /// <summary>Une facture récapitulative de bons de station ne crée pas de chiffre d'affaires.</summary>
    public bool GenereChiffreAffaires => Sens == SensFacture.Client && Origine is OrigineFacture.Vrac;
}

public class LigneFacture
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FactureId { get; set; }
    public Guid? ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public string Designation { get; set; } = "";
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHt { get; set; }
    public decimal TauxTva { get; set; }
    public decimal MontantHt { get; set; }
    public decimal MontantTva { get; set; }
    public decimal MontantTtc { get; set; }
    public Guid? LivraisonId { get; set; }
    public Guid? ReceptionId { get; set; }
}
