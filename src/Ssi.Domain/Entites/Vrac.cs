using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class CommandeClient : Document
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid ProduitId { get; set; }
    public Produit? Produit { get; set; }
    public decimal Volume { get; set; }
    public decimal PrixVenteHt { get; set; }
    public DateOnly? DatePrevue { get; set; }
    public string? LieuLivraison { get; set; }
    public string? ConditionsReglement { get; set; }
    public string? Transporteur { get; set; }
    public Guid? FournisseurPrevuId { get; set; }
    public StatutCommande Statut { get; set; } = StatutCommande.Brouillon;
    public decimal? MargePrevueHt { get; set; }
    public decimal? PrixAchatPrevuHt { get; set; }
    public decimal FraisPrevusHt { get; set; }
    public Guid? DerogationPar { get; set; }
    public string? DerogationMotif { get; set; }
}

/// <summary>Livraison directe fournisseur → client. N'a aucun lien avec les cuves ni les mouvements de stock.</summary>
public class LivraisonDirecte : Document
{
    public Guid CommandeClientId { get; set; }
    public Guid? CommandeFournisseurId { get; set; }
    public Guid FournisseurId { get; set; }
    public Guid ClientId { get; set; }
    public Guid ProduitId { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public decimal QuantiteCommandee { get; set; }
    public decimal QuantiteChargee { get; set; }
    public decimal QuantiteLivree { get; set; }
    public string? NumeroBlFournisseur { get; set; }
    public string? NumeroReceptionClient { get; set; }
    public string? Vehicule { get; set; }
    public string? Transporteur { get; set; }
    public string? Chauffeur { get; set; }
    public decimal PrixAchatHt { get; set; }
    public decimal PrixVenteHt { get; set; }
    public string? JustificationEcart { get; set; }
    public string? Reserves { get; set; }
    public StatutLivraison Statut { get; set; } = StatutLivraison.Planifiee;
}

public class FraisDirect : Entite
{
    public TypeFrais Type { get; set; }
    public string Libelle { get; set; } = "";
    public Guid? FournisseurId { get; set; }
    public Guid? FactureId { get; set; }
    public DateOnly Date { get; set; }
    public decimal MontantHt { get; set; }
    public MethodeRepartitionFrais Methode { get; set; } = MethodeRepartitionFrais.Volume;
    public List<RepartitionFrais> Repartitions { get; set; } = [];
}

public class RepartitionFrais
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid FraisId { get; set; }
    public Guid LivraisonId { get; set; }
    public decimal MontantHt { get; set; }
}
