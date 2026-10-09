using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ssi.Application.Commun;
using Ssi.Domain.Entites;
using Ssi.Infrastructure.Persistance;

namespace Ssi.Api.Infrastructure;

public class OptionsJwt
{
    public string Cle { get; set; } = "";
    public string Emetteur { get; set; } = "SSI";
    public int DureeHeures { get; set; } = 8;
}

/// <summary>Utilisateur courant chargé une fois par requête depuis la base (droits toujours à jour).</summary>
public class UtilisateurCourantHttp(IHttpContextAccessor http) : IUtilisateurCourant
{
    public const string ClaimSecurite = "ssi_sec";
    internal HashSet<string> PermissionsEffectives { get; set; } = [];
    internal bool Charge { get; set; }

    public Guid? Id => Guid.TryParse(http.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                     ?? http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : SystemeId;
    public string Login => http.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                           ?? http.HttpContext?.User.Identity?.Name ?? SystemeLogin;
    public string? AdresseIp => http.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public bool APermission(string code) => PermissionsEffectives.Contains(code);

    /// <summary>Hors requête HTTP (initialisation), les opérations sont attribuées au système.</summary>
    internal Guid? SystemeId { get; set; }
    internal string SystemeLogin { get; set; } = "systeme";

    public static async Task<HashSet<string>> PermissionsDeAsync(SsiDbContext db, Guid utilisateurId, CancellationToken ct)
    {
        var parRoles = await db.UtilisateurRoles.Where(r => r.UtilisateurId == utilisateurId)
            .Join(db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionCode)
            .ToListAsync(ct);
        var individuelles = await db.UtilisateurPermissions.Where(p => p.UtilisateurId == utilisateurId).ToListAsync(ct);
        var set = parRoles.ToHashSet();
        foreach (var p in individuelles)
            if (p.Accorde) set.Add(p.PermissionCode); else set.Remove(p.PermissionCode);
        return set;
    }
}

/// <summary>Charge les permissions et refuse les jetons d'un compte désactivé ou dont les droits ont changé.</summary>
public class MiddlewareUtilisateur(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IUtilisateurCourant courant, SsiDbContext db)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && courant is UtilisateurCourantHttp u && u.Id is Guid id)
        {
            var sec = ctx.User.FindFirstValue(UtilisateurCourantHttp.ClaimSecurite);
            var utilisateur = await db.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ctx.RequestAborted);
            if (utilisateur is null || !utilisateur.Actif || utilisateur.HorodatageSecurite.ToString() != sec)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 401, Title = "Session expirée",
                    Detail = "Votre compte a été modifié ou désactivé. Veuillez vous reconnecter."
                });
                return;
            }
            if (utilisateur.DoitChangerMotDePasse && !ctx.Request.Path.StartsWithSegments("/api/auth"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 403, Title = "Changement de mot de passe obligatoire",
                    Detail = "Vous devez changer votre mot de passe initial avant d'utiliser SSI."
                });
                return;
            }
            u.PermissionsEffectives = await UtilisateurCourantHttp.PermissionsDeAsync(db, id, ctx.RequestAborted);
            u.Charge = true;
        }
        await next(ctx);
    }
}

public class FabriqueJeton(Microsoft.Extensions.Options.IOptions<OptionsJwt> options, IHorloge horloge)
{
    public (string Jeton, DateTimeOffset Expiration) Creer(Utilisateur u)
    {
        var o = options.Value;
        var expiration = horloge.Maintenant.AddHours(o.DureeHeures);
        var cle = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Cle));
        var jeton = new JwtSecurityToken(
            issuer: o.Emetteur, audience: o.Emetteur,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, u.Login),
                new Claim(UtilisateurCourantHttp.ClaimSecurite, u.HorodatageSecurite.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: horloge.Maintenant.UtcDateTime,
            expires: expiration.UtcDateTime,
            signingCredentials: new SigningCredentials(cle, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jeton), expiration);
    }
}

/// <summary>Exige au moins une des permissions indiquées.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class AutoriseAttribute(params string[] permissions) : Attribute, IAuthorizationFilter
{
    public string[] Permissions { get; } = permissions;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return; // [Authorize] renverra 401
        var u = context.HttpContext.RequestServices.GetRequiredService<IUtilisateurCourant>();
        if (!Permissions.Any(u.APermission))
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 403, Title = "Accès refusé",
                Detail = $"Cette action nécessite l'autorisation : {string.Join(" ou ", Permissions)}."
            }) { StatusCode = 403 };
    }
}
