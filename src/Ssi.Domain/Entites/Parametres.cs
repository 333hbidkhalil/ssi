using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class Societe : Entite
{
    public string RaisonSociale { get; set; } = "";
    public string? FormeJuridique { get; set; }
    public string? Ice { get; set; }
    public string? IdentifiantFiscal { get; set; }
    public string? RegistreCommerce { get; set; }
    public string? Patente { get; set; }
    public string? Cnss { get; set; }
    public string? Adresse { get; set; }
    public string? Ville { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public byte[]? Logo { get; set; }
    public string? MentionsLegales { get; set; }
}

public class Parametre
{
    public string Cle { get; set; } = "";
    public string Valeur { get; set; } = "";
    public string Type { get; set; } = "texte";
    public string Description { get; set; } = "";
    public DateTimeOffset? ModifieLe { get; set; }
    public Guid? ModifiePar { get; set; }
}

public class Taxe : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public List<TauxTaxe> Taux { get; set; } = [];
}

/// <summary>Taux daté : aucun taux fiscal n'est codé en dur.</summary>
public class TauxTaxe : Entite
{
    public Guid TaxeId { get; set; }
    public decimal Taux { get; set; }
    public DateOnly DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
    public string? Commentaire { get; set; }
}

public class SequenceDocument
{
    public string TypeDocument { get; set; } = "";
    public string Prefixe { get; set; } = "";
    public int Annee { get; set; }
    public long ProchainNumero { get; set; } = 1;
    public int Longueur { get; set; } = 6;
}

public class Objectif : Entite
{
    public DateOnly Periode { get; set; }
    public string Indicateur { get; set; } = "";
    public Activite? Activite { get; set; }
    public decimal Valeur { get; set; }
}

public class Simulation : Entite
{
    public string Nom { get; set; } = "";
    public string Parametres { get; set; } = "{}";
}

public class Sauvegarde
{
    public long Id { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public string Fichier { get; set; } = "";
    public long Taille { get; set; }
    public string? Sha256 { get; set; }
    public string Statut { get; set; } = "";
    public string? Message { get; set; }
}
