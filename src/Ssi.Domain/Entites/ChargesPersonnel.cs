using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class CategorieCharge : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public bool FixeParDefaut { get; set; }
}

/// <summary>Centre de coût : lié à une activité (charge spécifique) ou commun (réparti par une clé).</summary>
public class CentreCout : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public Activite? Activite { get; set; }
    public Guid? CleRepartitionId { get; set; }
    public CleRepartition? CleRepartition { get; set; }
}

public class CleRepartition : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public MethodeCleRepartition Methode { get; set; }
    public List<CleRepartitionManuelle> Pourcentages { get; set; } = [];
}

public class CleRepartitionManuelle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CleId { get; set; }
    public Activite Activite { get; set; }
    public decimal Pourcentage { get; set; }
}

public class Charge : Document
{
    public DateOnly Date { get; set; }
    public Guid? FournisseurId { get; set; }
    public string? Beneficiaire { get; set; }
    public Guid CategorieId { get; set; }
    public CategorieCharge? Categorie { get; set; }
    public Guid CentreCoutId { get; set; }
    public CentreCout? CentreCout { get; set; }
    public decimal MontantHt { get; set; }
    public decimal MontantTva { get; set; }
    public decimal MontantTtc { get; set; }
    public bool Fixe { get; set; }
    public DateOnly Echeance { get; set; }
    public string? Description { get; set; }
    public string? ReferencePiece { get; set; }
    public StatutDocument Statut { get; set; } = StatutDocument.Valide;
}

public class Employe : Entite
{
    public string Matricule { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string? Cin { get; set; }
    public string Fonction { get; set; } = "";
    public DateOnly DateEmbauche { get; set; }
    public string? Affectation { get; set; }
    /// <summary>Donnée sensible : non renvoyée sans la permission personnel.sensible.</summary>
    public decimal SalaireBase { get; set; }
    public string? Telephone { get; set; }
    public bool Actif { get; set; } = true;
    public string NomComplet => $"{Prenom} {Nom}".Trim();
}

public class AffectationEmploye : Entite
{
    public Guid EmployeId { get; set; }
    public string Fonction { get; set; } = "";
    public string? Affectation { get; set; }
    public DateOnly Debut { get; set; }
    public DateOnly? Fin { get; set; }
}

public class ElementRemuneration : Entite
{
    public Guid EmployeId { get; set; }
    public DateOnly Periode { get; set; }
    public TypeElementRemuneration Type { get; set; }
    public decimal Montant { get; set; }
    public string? Commentaire { get; set; }
}

public class Absence : Entite
{
    public Guid EmployeId { get; set; }
    public DateOnly Debut { get; set; }
    public DateOnly Fin { get; set; }
    public string Type { get; set; } = "";
    public bool Justifiee { get; set; }
}
