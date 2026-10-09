using Microsoft.Extensions.DependencyInjection;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Referentiels;
using Ssi.Application.Securite;
using Ssi.Application.Station;
using Ssi.Application.Stocks;

namespace Ssi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AjouterApplication(this IServiceCollection s)
    {
        // Socle
        s.AddScoped<Audit>();
        s.AddScoped<Parametres>();
        s.AddScoped<Fiscalite>();
        s.AddScoped<Tarifs>();
        s.AddScoped<MoteurStock>();
        s.AddScoped<MoteurTresorerie>();
        // Modules
        s.AddScoped<ServiceSecurite>();
        s.AddScoped<ServiceCatalogue>();
        s.AddScoped<ServiceEquipements>();
        s.AddScoped<ServicePostes>();
        return s;
    }
}
