namespace Ssi.Contracts;

public sealed record ConnexionDto(string Login, string MotDePasse);
public sealed record SessionDto(string Jeton, DateTimeOffset Expiration, UtilisateurConnecteDto Utilisateur);
public sealed record UtilisateurConnecteDto(Guid Id, string Login, string NomComplet, bool DoitChangerMotDePasse, IReadOnlyList<string> Permissions);
public sealed record ChangementMotDePasseDto(string AncienMotDePasse, string NouveauMotDePasse);

public sealed record UtilisateurDto(Guid Id, string Login, string NomComplet, bool Actif, Guid? EmployeId,
    IReadOnlyList<string> Roles, IReadOnlyList<PermissionIndividuelleDto> PermissionsIndividuelles, DateTimeOffset? DerniereConnexion, bool Verrouille);
public sealed record PermissionIndividuelleDto(string Code, bool Accorde);
public sealed record CreationUtilisateurDto(string Login, string NomComplet, string MotDePasse, IReadOnlyList<string> Roles, Guid? EmployeId);
public sealed record ModificationUtilisateurDto(string NomComplet, bool Actif, Guid? EmployeId, IReadOnlyList<string> Roles,
    IReadOnlyList<PermissionIndividuelleDto> PermissionsIndividuelles, string Motif);
public sealed record ReinitialisationMotDePasseDto(string NouveauMotDePasse, string Motif);

public sealed record RoleDto(Guid Id, string Code, string Libelle, bool Systeme, IReadOnlyList<string> Permissions);
public sealed record EnregistrementRoleDto(string Code, string Libelle, IReadOnlyList<string> Permissions, string Motif);
public sealed record PermissionDto(string Code, string Module, string Libelle, bool Sensible);

public sealed record ParametreDto(string Cle, string Valeur, string Type, string Description);
public sealed record ModificationParametreDto(string Valeur, string Motif);

public sealed record SocieteDto(string RaisonSociale, string? FormeJuridique, string? Ice, string? IdentifiantFiscal, string? RegistreCommerce,
    string? Patente, string? Cnss, string? Adresse, string? Ville, string? Telephone, string? Email, string? MentionsLegales, bool LogoPresent);

public sealed record TaxeDto(Guid Id, string Code, string Libelle, IReadOnlyList<TauxTaxeDto> Taux);
public sealed record TauxTaxeDto(Guid Id, decimal Taux, DateOnly DateDebut, DateOnly? DateFin, string? Commentaire);
public sealed record NouveauTauxDto(decimal Taux, DateOnly DateDebut, string Motif);

public sealed record EntreeAuditDto(long Id, DateTimeOffset Horodatage, string Login, string Action, string ObjetType, string? ObjetId,
    string? ObjetRef, string? Motif, string? Avant, string? Apres, string? AdresseIp);

public sealed record SauvegardeDto(long Id, DateTimeOffset Horodatage, string Fichier, long Taille, string? Sha256, string Statut, string? Message);
public sealed record EtatSauvegardesDto(SauvegardeDto? DerniereReussie, SauvegardeDto? DerniereVerifiee, IReadOnlyList<SauvegardeDto> Historique, bool Alerte, string Message);
