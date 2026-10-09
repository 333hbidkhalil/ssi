using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using Ssi.Api.Infrastructure;
using Ssi.Application;
using Ssi.Application.Commun;
using Ssi.Infrastructure;
using Ssi.Infrastructure.Persistance;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

// Interface et messages en français (Maroc) : séparateur décimal virgule, dates jj/mm/aaaa.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-MA");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseWindowsService(o => o.ServiceName = "SSI API");

    builder.Host.UseSerilog((ctx, sp, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(new CompactJsonFormatter(),
            Path.Combine(ctx.Configuration["Ssi:DossierLogs"] ?? Path.Combine(AppContext.BaseDirectory, "logs"), "ssi-api-.json"),
            rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30, fileSizeLimitBytes: 50_000_000, rollOnFileSizeLimit: true));

    var chaine = builder.Configuration.GetConnectionString("Ssi")
                 ?? throw new InvalidOperationException("Chaîne de connexion 'Ssi' absente de la configuration.");
    var jwt = builder.Configuration.GetSection("Jwt").Get<OptionsJwt>() ?? new OptionsJwt();
    if (string.IsNullOrWhiteSpace(jwt.Cle) || jwt.Cle.Length < 32)
        throw new InvalidOperationException("La clé de signature Jwt:Cle doit contenir au moins 32 caractères (voir deploy/installer-serveur.ps1).");
    builder.Services.Configure<OptionsJwt>(builder.Configuration.GetSection("Jwt"));

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<UtilisateurCourantHttp>();
    builder.Services.AddScoped<IUtilisateurCourant>(sp => sp.GetRequiredService<UtilisateurCourantHttp>());
    builder.Services.AjouterInfrastructure(chaine);
    builder.Services.AjouterApplication();
    builder.Services.AddScoped<FabriqueJeton>();
    builder.Services.AddScoped<FiltreTransaction>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Emetteur,
            ValidateAudience = true, ValidAudience = jwt.Emetteur,
            ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Cle)),
            NameClaimType = "unique_name"
        };
    });
    builder.Services.AddAuthorization();

    builder.Services.AddControllers(o =>
        {
            o.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
            o.Filters.AddService<FiltreTransaction>();
        })
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        });
    builder.Services.AddProblemDetails();
    builder.Services.AddHealthChecks().AddDbContextCheck<SsiDbContext>("base");

    var app = builder.Build();

    if (!app.Environment.IsEnvironment("Test"))
    {
        using var scope = app.Services.CreateScope();
        var init = scope.ServiceProvider.GetRequiredService<Initialisation>();
        await init.ExecuterAsync(app.Configuration["Ssi:MotDePasseAdminInitial"] ?? "Admin2026!");
    }

    app.UseMiddleware<MiddlewareErreurs>();
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseMiddleware<MiddlewareUtilisateur>();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Arrêt inattendu de l'API SSI");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
