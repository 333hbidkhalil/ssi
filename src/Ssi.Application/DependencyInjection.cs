using Microsoft.Extensions.DependencyInjection;
using Ssi.Application.Achats;
using Ssi.Application.Charges;
using Ssi.Application.Tiers;
using Ssi.Application.Ventes;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Referentiels;
using Ssi.Application.Securite;
using Ssi.Application.Station;
using Ssi.Application.Stocks;
using Ssi.Application.Vrac;

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
        s.AddScoped<Soldes>();
        // Modules
        s.AddScoped<ServiceSecurite>();
        s.AddScoped<ServiceCatalogue>();
        s.AddScoped<ServiceEquipements>();
        s.AddScoped<ServicePostes>();
        s.AddScoped<ServiceStocks>();
        s.AddScoped<ServiceFactures>();
        s.AddScoped<ServiceAchats>();
        s.AddScoped<ServiceVrac>();
        s.AddScoped<ServiceTiers>();
        s.AddScoped<ServiceVentes>();
        s.AddScoped<ServiceTresorerie>();
        s.AddScoped<ServiceCharges>();
        return s;
    }
}
