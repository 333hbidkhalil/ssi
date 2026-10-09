using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public abstract class TiersBase : Entite
{
    public string Code { get; set; } = "";
    public string Nom { get; set; } = "";
    public string? Ice { get; set; }
    public string? IdentifiantFiscal { get; set; }
    public string? RegistreCommerce { get; set; }
    public string? Adresse { get; set; }
    public string? Ville { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Contact { get; set; }
    public int DelaiPaiementJours { get; set; }
    public bool Actif { get; set; } = true;
    public string? Notes { get; set; }
}

public class Client : TiersBase
{
    public TypeClient Type { get; set; } = TypeClient.Comptant;
    public decimal PlafondCredit { get; set; }
}

public class Fournisseur : TiersBase
{
    public string? ConditionsAchat { get; set; }
    public string? ProduitsFournis { get; set; }
}

public class IncidentFournisseur : Entite
{
    public Guid FournisseurId { get; set; }
    public DateOnly Date { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string? DocumentRef { get; set; }
}

public class Relance : Entite
{
    public Guid ClientId { get; set; }
    public Guid? FactureId { get; set; }
    public DateOnly Date { get; set; }
    public string Moyen { get; set; } = "";
    public string? Commentaire { get; set; }
}

public class PieceJointe : Entite
{
    public string ObjetType { get; set; } = "";
    public Guid ObjetId { get; set; }
    public string NomFichier { get; set; } = "";
    public string TypeMime { get; set; } = "application/octet-stream";
    public long Taille { get; set; }
    public byte[] Contenu { get; set; } = [];
}
