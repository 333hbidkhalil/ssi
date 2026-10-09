using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ssi.Application.Commun;
using Ssi.Domain.Commun;
using Ssi.Infrastructure.Persistance;

namespace Ssi.Api.Infrastructure;

/// <summary>La commande exige un en-tête Idempotency-Key (validations, clôtures, règlements).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute;

/// <summary>
/// Exécute chaque requête de modification dans UNE transaction : tout ou rien.
/// Gère l'idempotence : la clé et la réponse sont enregistrées dans la même transaction que l'opération ;
/// un renvoi (double clic, reprise après coupure) rejoue la réponse initiale sans réexécuter.
/// </summary>
public class FiltreTransaction(SsiDbContext db, IUtilisateurCourant utilisateur, IHorloge horloge, ILogger<FiltreTransaction> log,
    Microsoft.Extensions.Options.IOptions<JsonOptions> jsonMvc) : IAsyncActionFilter
{
    public const string EnTeteCle = "Idempotency-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var methode = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(methode) || HttpMethods.IsHead(methode) || HttpMethods.IsOptions(methode)
            || context.ActionDescriptor.EndpointMetadata.OfType<SansTransactionAttribute>().Any())
        {
            await next();
            return;
        }

        var exigeCle = context.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any();
        var cle = context.HttpContext.Request.Headers[EnTeteCle].FirstOrDefault();
        if (exigeCle && string.IsNullOrWhiteSpace(cle))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 400, Title = "Clé d'idempotence manquante",
                Detail = $"Cette opération exige l'en-tête {EnTeteCle} pour empêcher les doubles enregistrements."
            }) { StatusCode = 400 };
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, context.HttpContext.RequestAborted);
        var operation = $"{methode} {context.HttpContext.Request.Path}";
        if (!string.IsNullOrWhiteSpace(cle) && utilisateur.Id is Guid uid)
        {
            var inseree = await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO cles_idempotence (cle, utilisateur_id, operation, code_statut, cree_le) VALUES ({cle}, {uid}, {operation}, 0, {horloge.Maintenant}) ON CONFLICT DO NOTHING");
            if (inseree == 0)
            {
                // Clé déjà utilisée et validée (la transaction concurrente a été attendue par l'index unique).
                var existante = await db.ClesIdempotence.AsNoTracking().FirstAsync(k => k.Cle == cle && k.UtilisateurId == uid);
                await tx.RollbackAsync();
                if (existante.Operation != operation)
                {
                    context.Result = new ObjectResult(new ProblemDetails
                    {
                        Status = 409, Title = "Clé d'idempotence déjà utilisée", Detail = "Cette clé a déjà servi pour une autre opération."
                    }) { StatusCode = 409 };
                    return;
                }
                context.HttpContext.Response.Headers["Idempotent-Replay"] = "true";
                context.Result = new ContentResult
                {
                    StatusCode = existante.CodeStatut, ContentType = "application/json; charset=utf-8",
                    Content = existante.Reponse ?? "{}"
                };
                return;
            }
        }

        var executed = await next();
        var statut = executed.Exception is null && !executed.ExceptionHandled ? StatutDe(executed.Result) : 500;
        if (executed.Exception is not null && !executed.ExceptionHandled || statut >= 400)
        {
            await tx.RollbackAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(cle) && utilisateur.Id is Guid u2)
        {
            var valeur = executed.Result is ObjectResult or
                ? JsonSerializer.Serialize(or.Value, or.DeclaredType ?? or.Value?.GetType() ?? typeof(object), jsonMvc.Value.JsonSerializerOptions)
                : "{}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE cles_idempotence SET code_statut = {statut}, reponse = {valeur} WHERE cle = {cle} AND utilisateur_id = {u2}");
        }
        try
        {
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Échec de validation de la transaction {Operation}", operation);
            throw;
        }
    }

    private static int StatutDe(IActionResult? r) => r switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => 200
    };
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class SansTransactionAttribute : Attribute;

/// <summary>Traduit les exceptions métier et base de données en ProblemDetails compréhensibles (en français).</summary>
public class MiddlewareErreurs(RequestDelegate next, ILogger<MiddlewareErreurs> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            var (statut, titre, detail, erreurs) = Traduire(ex);
            if (statut >= 500) log.LogError(ex, "Erreur non gérée");
            else log.LogInformation("Refus {Statut} : {Detail}", statut, detail);
            ctx.Response.Clear();
            ctx.Response.StatusCode = statut;
            var pb = new ProblemDetails { Status = statut, Title = titre, Detail = detail };
            pb.Extensions["correlation"] = ctx.TraceIdentifier;
            if (ex is RegleMetierException { Code: not null } rm) pb.Extensions["code"] = rm.Code;
            if (erreurs is not null) pb.Extensions["erreurs"] = erreurs;
            await ctx.Response.WriteAsJsonAsync(pb, (JsonSerializerOptions?)null, "application/problem+json");
        }
    }

    public static (int, string, string, object?) Traduire(Exception ex)
    {
        var racine = ex is DbUpdateException { InnerException: not null } due ? due.InnerException : ex;
        return racine switch
        {
            ValidationException v => (400, "Données invalides", v.Message, v.Erreurs),
            RegleMetierException r => (422, "Règle métier non respectée", r.Message, null),
            ConflitException c => (409, "Conflit", c.Message, null),
            NonTrouveException n => (404, "Introuvable", n.Message, null),
            AccesRefuseException a => (403, "Accès refusé", a.Message, null),
            DbUpdateConcurrencyException => (409, "Modification simultanée",
                "Cet enregistrement a été modifié ou validé par un autre utilisateur. Rechargez-le avant de recommencer.", null),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg => (409, "Doublon", MessageContrainte(pg.ConstraintName), null),
            PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } pg => (409, "Chevauchement", MessageContrainte(pg.ConstraintName), null),
            PostgresException { SqlState: PostgresErrorCodes.CheckViolation } pg => (400, "Valeur non autorisée", MessageContrainte(pg.ConstraintName), null),
            PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } => (409, "Référence invalide",
                "L'opération fait référence à un élément inexistant ou encore utilisé.", null),
            PostgresException { SqlState: "P0001" } pg => (422, "Opération interdite", pg.MessageText, null),
            PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected } =>
                (409, "Opération simultanée", "Une autre opération concurrente a été détectée. Veuillez réessayer.", null),
            NpgsqlException or TimeoutException => (503, "Base de données indisponible",
                "Le serveur de base de données ne répond pas. Aucune donnée partielle n'a été enregistrée ; réessayez dans un instant.", null),
            _ => (500, "Erreur interne", "Une erreur inattendue est survenue. Communiquez l'identifiant de corrélation au support.", null)
        };
    }

    private static string MessageContrainte(string? contrainte) => contrainte switch
    {
        "ux_releve_poste_pistolet" => "Ce pistolet a déjà un relevé dans ce poste : un relevé ne peut pas être comptabilisé deux fois.",
        "ex_releve_plages_index" => "Cette plage d'index chevauche un relevé existant du même pistolet (double saisie probable).",
        "ex_prix_periodes" => "La période de ce prix chevauche une période existante.",
        "ex_taux_periodes" => "La période de ce taux chevauche une période existante.",
        "ux_facture_fournisseur_numero" => "Cette facture fournisseur (même fournisseur, même numéro) est déjà enregistrée.",
        "ux_poste_unique_ouvert" => "Un poste est déjà ouvert : clôturez-le avant d'en ouvrir un nouveau.",
        "ux_session_caisse_ouverte" => "Une session est déjà ouverte pour cette caisse.",
        "ck_reglement_poste_credit" => "Un bon de crédit doit être rattaché à un client.",
        "ck_ajustement_approbateur" => "L'ajustement doit être approuvé par un autre utilisateur que le demandeur.",
        "ck_releve_index" => "L'index final doit être supérieur ou égal à l'index initial.",
        "ck_lr_acceptee" => "La quantité acceptée ne peut pas dépasser la quantité reçue.",
        "ck_charge_montants" => "Les montants de la charge sont incohérents (HT + TVA doit être égal au TTC).",
        { } c when c.StartsWith("ix_") && c.EndsWith("_code") => "Ce code est déjà utilisé.",
        { } c when c.Contains("login") => "Cet identifiant est déjà utilisé.",
        { } c when c.Contains("numero") => "Ce numéro de document existe déjà.",
        _ => $"Contrainte d'intégrité non respectée ({contrainte})."
    };
}
