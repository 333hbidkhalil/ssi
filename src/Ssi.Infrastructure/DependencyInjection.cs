using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Ssi.Application.Commun;
using Ssi.Infrastructure.Persistance;
using Ssi.Infrastructure.Services;

namespace Ssi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AjouterInfrastructure(this IServiceCollection services, string chaineConnexion)
    {
        services.AddDbContext<SsiDbContext>((sp, o) =>
        {
            o.UseNpgsql(chaineConnexion, n => n.MigrationsHistoryTable("__historique_migrations"))
             .UseSnakeCaseNamingConvention();
        });
        services.AddScoped<ISsiDb>(sp =>
        {
            var db = sp.GetRequiredService<SsiDbContext>();
            var u = sp.GetRequiredService<IUtilisateurCourant>();
            var h = sp.GetRequiredService<IHorloge>();
            db.Contexte = () => (u.Id, h.Maintenant);
            return db;
        });
        services.AddSingleton<IHorloge, HorlogeCasablanca>();
        services.AddScoped<INumerotation, Numerotation>();
        services.AddSingleton<IHachageMotDePasse, HachageMotDePasse>();
        services.AddScoped<Initialisation>();
        return services;
    }
}

/// <summary>Fabrique utilisée par « dotnet ef » pour générer les migrations.</summary>
public class FabriqueDesign : IDesignTimeDbContextFactory<SsiDbContext>
{
    public SsiDbContext CreateDbContext(string[] args)
    {
        var o = new DbContextOptionsBuilder<SsiDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("SSI_DB") ?? "Host=localhost;Database=ssi_design;Username=postgres",
                n => n.MigrationsHistoryTable("__historique_migrations"))
            .UseSnakeCaseNamingConvention();
        return new SsiDbContext(o.Options);
    }
}
