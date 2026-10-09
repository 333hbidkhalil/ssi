using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class FamilleProduit : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public CategorieProduit Categorie { get; set; }
}

public class Produit : Entite
{
    public string Code { get; set; } = "";
    public string Designation { get; set; } = "";
    public Guid FamilleId { get; set; }
    public FamilleProduit? Famille { get; set; }
    public string Unite { get; set; } = "L";
    public string? Marque { get; set; }
    public string? Conditionnement { get; set; }
    public Guid TaxeId { get; set; }
    public Taxe? Taxe { get; set; }
    public MethodeValorisation MethodeValorisation { get; set; } = MethodeValorisation.CoutMoyenPondere;
    public decimal SeuilAlerte { get; set; }
    public TypeCarburant? TypeCarburant { get; set; }
    public bool Actif { get; set; } = true;
}

/// <summary>Prix daté. Les périodes d'un même produit et type ne se chevauchent pas (contrainte d'exclusion).</summary>
public class PrixProduit : Entite
{
    public Guid ProduitId { get; set; }
    public TypePrix Type { get; set; }
    /// <summary>Prix de vente TTC (type Vente) ou prix d'achat HT de référence.</summary>
    public decimal Prix { get; set; }
    public DateTimeOffset Debut { get; set; }
    public DateTimeOffset? Fin { get; set; }
    public string? Motif { get; set; }
    public Guid? ValidePar { get; set; }
}
