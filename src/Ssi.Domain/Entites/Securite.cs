using Ssi.Domain.Commun;

namespace Ssi.Domain.Entites;

public class Utilisateur : Entite
{
    public string Login { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public string HashMotDePasse { get; set; } = "";
    public bool Actif { get; set; } = true;
    public Guid? EmployeId { get; set; }
    public bool DoitChangerMotDePasse { get; set; }
    public int EchecsConnexion { get; set; }
    public DateTimeOffset? VerrouilleJusquA { get; set; }
    public DateTimeOffset? DerniereConnexion { get; set; }
    /// <summary>Change à chaque modification de droits ou de mot de passe : invalide les jetons émis avant.</summary>
    public Guid HorodatageSecurite { get; set; } = Guid.NewGuid();
    public List<UtilisateurRole> Roles { get; set; } = [];
    public List<UtilisateurPermission> Permissions { get; set; } = [];
}

public class Role : Entite
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public bool Systeme { get; set; }
    public List<RolePermission> Permissions { get; set; } = [];
}

public class Permission
{
    public string Code { get; set; } = "";
    public string Module { get; set; } = "";
    public string Libelle { get; set; } = "";
    public bool Sensible { get; set; }
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = "";
}

public class UtilisateurRole
{
    public Guid UtilisateurId { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

/// <summary>Permission individuelle : accordée en plus des rôles, ou retirée (Accorde = false).</summary>
public class UtilisateurPermission
{
    public Guid UtilisateurId { get; set; }
    public string PermissionCode { get; set; } = "";
    public bool Accorde { get; set; } = true;
}

/// <summary>Journal d'audit append-only (UPDATE/DELETE interdits par trigger).</summary>
public class JournalAudit
{
    public long Id { get; set; }
    public DateTimeOffset Horodatage { get; set; }
    public Guid? UtilisateurId { get; set; }
    public string Login { get; set; } = "";
    public string Action { get; set; } = "";
    public string ObjetType { get; set; } = "";
    public string? ObjetId { get; set; }
    public string? ObjetRef { get; set; }
    public string? Motif { get; set; }
    public string? Avant { get; set; }
    public string? Apres { get; set; }
    public string? AdresseIp { get; set; }
}

/// <summary>Clé d'idempotence : une commande rejouée renvoie la réponse initiale sans être réexécutée.</summary>
public class CleIdempotence
{
    public string Cle { get; set; } = "";
    public Guid UtilisateurId { get; set; }
    public string Operation { get; set; } = "";
    public int CodeStatut { get; set; }
    public string? Reponse { get; set; }
    public DateTimeOffset CreeLe { get; set; }
}
