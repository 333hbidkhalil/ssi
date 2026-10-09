using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssi.Api.Infrastructure;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Contracts;
using Ssi.Infrastructure.Persistance;

namespace Ssi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ServiceSecurite securite, FabriqueJeton jetons, SsiDbContext db) : ControllerBase
{
    /// <summary>Connexion : hors transaction afin que les échecs soient comptés même en cas de refus.</summary>
    [HttpPost("connexion"), AllowAnonymous, SansTransaction]
    public async Task<SessionDto> Connexion(ConnexionDto dto, CancellationToken ct)
    {
        var r = await securite.ConnecterAsync(dto, id => UtilisateurCourantHttp.PermissionsDeAsync(db, id, ct), ct);
        var (jeton, expiration) = jetons.Creer(r.Utilisateur);
        return new SessionDto(jeton, expiration,
            new UtilisateurConnecteDto(r.Utilisateur.Id, r.Utilisateur.Login, r.Utilisateur.NomComplet, r.Utilisateur.DoitChangerMotDePasse, r.Permissions));
    }

    [HttpGet("moi")]
    public async Task<UtilisateurConnecteDto> Moi([FromServices] IUtilisateurCourant u, CancellationToken ct)
    {
        var utilisateur = await db.Utilisateurs.FindAsync([u.Id], ct);
        return new UtilisateurConnecteDto(utilisateur!.Id, utilisateur.Login, utilisateur.NomComplet, utilisateur.DoitChangerMotDePasse,
            (await UtilisateurCourantHttp.PermissionsDeAsync(db, utilisateur.Id, ct)).OrderBy(x => x).ToList());
    }

    /// <summary>Change le mot de passe ; renvoie un nouveau jeton (l'ancien est invalidé).</summary>
    [HttpPost("mot-de-passe")]
    public async Task<SessionDto> ChangerMotDePasse(ChangementMotDePasseDto dto, [FromServices] IUtilisateurCourant u, CancellationToken ct)
    {
        await securite.ChangerMotDePasseAsync(dto, ct);
        var utilisateur = await db.Utilisateurs.FindAsync([u.Id], ct);
        var (jeton, expiration) = jetons.Creer(utilisateur!);
        return new SessionDto(jeton, expiration, new UtilisateurConnecteDto(utilisateur!.Id, utilisateur.Login, utilisateur.NomComplet, false,
            (await UtilisateurCourantHttp.PermissionsDeAsync(db, utilisateur.Id, ct)).OrderBy(x => x).ToList()));
    }
}

[ApiController]
[Route("api/admin")]
public class AdministrationController(ServiceSecurite securite) : ControllerBase
{
    [HttpGet("utilisateurs"), Autorise(P.AdminUtilisateurs)]
    public Task<IReadOnlyList<UtilisateurDto>> Utilisateurs(CancellationToken ct) => securite.UtilisateursAsync(ct);

    [HttpPost("utilisateurs"), Autorise(P.AdminUtilisateurs)]
    public Task<UtilisateurDto> CreerUtilisateur(CreationUtilisateurDto dto, CancellationToken ct) => securite.CreerUtilisateurAsync(dto, ct);

    [HttpPut("utilisateurs/{id:guid}"), Autorise(P.AdminUtilisateurs)]
    public Task<UtilisateurDto> ModifierUtilisateur(Guid id, ModificationUtilisateurDto dto, CancellationToken ct) => securite.ModifierUtilisateurAsync(id, dto, ct);

    [HttpPost("utilisateurs/{id:guid}/mot-de-passe"), Autorise(P.AdminUtilisateurs)]
    public async Task<IActionResult> Reinitialiser(Guid id, ReinitialisationMotDePasseDto dto, CancellationToken ct)
    {
        await securite.ReinitialiserMotDePasseAsync(id, dto, ct);
        return NoContent();
    }

    [HttpGet("roles"), Autorise(P.AdminRoles, P.AdminUtilisateurs)]
    public Task<IReadOnlyList<RoleDto>> Roles(CancellationToken ct) => securite.RolesAsync(ct);

    [HttpPost("roles"), Autorise(P.AdminRoles)]
    public Task<RoleDto> CreerRole(EnregistrementRoleDto dto, CancellationToken ct) => securite.EnregistrerRoleAsync(null, dto, ct);

    [HttpPut("roles/{id:guid}"), Autorise(P.AdminRoles)]
    public Task<RoleDto> ModifierRole(Guid id, EnregistrementRoleDto dto, CancellationToken ct) => securite.EnregistrerRoleAsync(id, dto, ct);

    [HttpGet("permissions"), Autorise(P.AdminRoles, P.AdminUtilisateurs)]
    public Task<IReadOnlyList<PermissionDto>> Permissions(CancellationToken ct) => securite.PermissionsAsync(ct);

    [HttpGet("parametres"), Autorise(P.AdminParametres, P.AuditConsulter)]
    public Task<IReadOnlyList<ParametreDto>> Parametres(CancellationToken ct) => securite.ParametresAsync(ct);

    [HttpPut("parametres/{cle}"), Autorise(P.AdminParametres)]
    public Task<ParametreDto> ModifierParametre(string cle, ModificationParametreDto dto, CancellationToken ct) => securite.ModifierParametreAsync(cle, dto, ct);

    [HttpGet("societe")]
    public Task<SocieteDto> Societe(CancellationToken ct) => securite.SocieteAsync(ct);

    [HttpPut("societe"), Autorise(P.AdminParametres)]
    public Task<SocieteDto> ModifierSociete(SocieteDto dto, CancellationToken ct) => securite.ModifierSocieteAsync(dto, null, ct);

    [HttpPut("societe/logo"), Autorise(P.AdminParametres), RequestSizeLimit(2_000_000)]
    public async Task<SocieteDto> Logo(IFormFile fichier, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await fichier.CopyToAsync(ms, ct);
        return await securite.ModifierSocieteAsync(await securite.SocieteAsync(ct), ms.ToArray(), ct);
    }

    [HttpGet("taxes")]
    public Task<IReadOnlyList<TaxeDto>> Taxes(CancellationToken ct) => securite.TaxesAsync(ct);

    [HttpPost("taxes/{id:guid}/taux"), Autorise(P.AdminParametres)]
    public Task<TaxeDto> AjouterTaux(Guid id, NouveauTauxDto dto, CancellationToken ct) => securite.AjouterTauxAsync(id, dto, ct);

    [HttpGet("audit"), Autorise(P.AuditConsulter)]
    public Task<Page<EntreeAuditDto>> Audit(string? objetType, string? objetId, string? action, DateTimeOffset? du, DateTimeOffset? au,
        int page = 1, int taille = 100, CancellationToken ct = default)
        => securite.AuditAsync(objetType, objetId, action, du, au, page, taille, ct);
}
