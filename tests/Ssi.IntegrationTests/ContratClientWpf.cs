using Ssi.Contracts;
using Ssi.IntegrationTests.Infrastructure;

namespace Ssi.IntegrationTests;

/// <summary>
/// Contrat du client Windows : chaque lecture faite par les écrans WPF (même URL, mêmes filtres, même type attendu)
/// répond et se désérialise. Le client ne pouvant pas être exécuté sous Linux, ce test garantit que ses écrans sont reliés à l'API réelle.
/// </summary>
public class ContratClientWpf(FabriqueApi api) : IClassFixture<FabriqueApi>
{
    private static readonly string Du = DateTime.Today.AddDays(-30).ToString("yyyy-MM-dd"), Au = DateTime.Today.ToString("yyyy-MM-dd");

    [Fact(DisplayName = "Client WPF : toutes les lectures des écrans répondent avec les types attendus")]
    public async Task LecturesDesEcrans()
    {
        var demo = await api.DemoAsync();
        var a = demo.Admin;
        // Tableau de bord, postes
        await a.GetOkAsync<TableauBordDto>("/api/pilotage/tableau-de-bord");
        var postes = await a.GetOkAsync<Page<PosteResumeDto>>($"/api/postes?du={Du}&taille=100");
        await a.GetOkAsync<List<ProduitDto>>("/api/produits?actif=true&categorie=Carburant");
        await a.GetOkAsync<List<ProduitDto>>("/api/produits?actif=true");
        await a.GetOkAsync<List<ClientDto>>("/api/clients?actif=true");
        await a.GetOkAsync<List<ClientDto>>("/api/clients?q=a");
        await a.GetOkAsync<List<FournisseurDto>>("/api/fournisseurs?actif=true");
        // Caisse, ventes
        await a.GetOkAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes");
        await a.GetOkAsync<List<SessionCaisseDto>>("/api/tresorerie/caisse/sessions");
        await a.GetOkAsync<List<PositionArticleDto>>("/api/stocks/articles");
        await a.GetOkAsync<Page<VenteDto>>($"/api/ventes?du={Au}&taille=200");
        // Stocks
        await a.GetOkAsync<List<EtatCuveDto>>("/api/stocks/cuves");
        await a.GetOkAsync<List<JaugeageDto>>("/api/stocks/jaugeages");
        await a.GetOkAsync<List<AjustementDto>>("/api/stocks/ajustements?statut=Demande");
        await a.GetOkAsync<List<AjustementDto>>("/api/stocks/ajustements");
        await a.GetOkAsync<Page<MouvementStockDto>>($"/api/stocks/mouvements?du={Du}&taille=300");
        await a.GetOkAsync<List<ControleCoherenceDto>>("/api/stocks/controle-coherence");
        await a.GetOkAsync<List<CuveDto>>("/api/equipements/cuves");
        // Vrac, factures, règlements
        await a.GetOkAsync<List<CommandeClientDto>>("/api/vrac/commandes");
        await a.GetOkAsync<List<FactureDto>>($"/api/factures?sens=Client&du={Du}&au={Au}");
        await a.GetOkAsync<List<FactureDto>>($"/api/factures?sens=Fournisseur&du={Du}&au={Au}");
        await a.GetOkAsync<List<ReglementDto>>($"/api/tresorerie/reglements?sens=Encaissement&du={Du}&au={Au}");
        await a.GetOkAsync<List<ReglementDto>>($"/api/tresorerie/reglements?sens=Decaissement&du={Du}&au={Au}");
        // Trésorerie et charges
        var situation = await a.GetOkAsync<SituationTresorerieDto>("/api/tresorerie/situation");
        await a.GetOkAsync<Page<MouvementFinancierDto>>($"/api/tresorerie/mouvements?compteId={situation.Comptes[0].Id}&du={Du}&au={Au}&taille=500");
        await a.GetOkAsync<List<TransfertDto>>("/api/tresorerie/transferts");
        await a.GetOkAsync<List<EcheanceDto>>("/api/tresorerie/creances");
        await a.GetOkAsync<List<EcheanceDto>>("/api/tresorerie/dettes");
        await a.GetOkAsync<List<CategorieChargeDto>>("/api/charges/categories");
        await a.GetOkAsync<List<CentreCoutDto>>("/api/charges/centres");
        await a.GetOkAsync<List<ChargeDto>>($"/api/charges?du={Du}&au={Au}");
        // Rentabilité, rapports
        var r = await a.GetOkAsync<RentabiliteDto>($"/api/pilotage/rentabilite?du={Du}&au={Au}");
        await a.GetOkAsync<SeuilRentabiliteDto>($"/api/pilotage/seuil-rentabilite?du={Du}&au={Au}");
        var catalogue = await a.GetOkAsync<Dictionary<string, string>>("/api/rapports");
        foreach (var code in catalogue.Keys)
            await a.GetOkAsync<RapportDto>($"/api/rapports/{code}?format=json&du={Du}&au={Au}" + (code == "tresorerie" ? $"&compteId={situation.Comptes[0].Id}" : ""));
        await a.GetOkAsync<List<ResultatControleDto>>("/api/pilotage/controles");
        // Administration
        await a.GetOkAsync<List<UtilisateurDto>>("/api/admin/utilisateurs");
        await a.GetOkAsync<List<RoleDto>>("/api/admin/roles");
        await a.GetOkAsync<List<ParametreDto>>("/api/admin/parametres");
        await a.GetOkAsync<List<TaxeDto>>("/api/admin/taxes");
        await a.GetOkAsync<Page<EntreeAuditDto>>($"/api/admin/audit?du={Du}&taille=500&action=CONNEXION");
        await a.GetOkAsync<List<SauvegardeDto>>("/api/admin/sauvegardes");

        Assert.NotEmpty(catalogue);
        Assert.NotNull(r.Total);
        Assert.NotNull(postes);
    }

    [Fact(DisplayName = "Client WPF : le menu n'affiche à un pompiste que les écrans qu'il peut ouvrir")]
    public async Task MenuSelonPermissions()
    {
        var demo = await api.DemoAsync();
        var moi = await demo.Pompiste.GetOkAsync<UtilisateurConnecteDto>("/api/auth/moi");
        Assert.Contains("station.consulter", moi.Permissions);
        Assert.DoesNotContain("rentabilite.consulter", moi.Permissions);
        Assert.DoesNotContain("admin.utilisateurs", moi.Permissions);
        // Le serveur refuse même si l'interface était contournée.
        var refus = await demo.Pompiste.GetAsync("/api/pilotage/rentabilite?du=2026-01-01&au=2026-01-31");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, refus.Statut);
    }

    [Fact(DisplayName = "Client WPF : parcours caisse avec les corps de requête envoyés par l'écran")]
    public async Task ParcoursCaisse()
    {
        var demo = await api.DemoAsync();
        var c = demo.Caissier;
        var caisse = (await c.GetOkAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes")).First(x => x.Type == TypeCompteFinancier.Caisse && x.Actif);
        var s = await c.PostOkAsync<SessionCaisseDto>("/api/tresorerie/caisse/sessions", new OuvertureCaisseDto(caisse.Id, null, null));
        s = await c.PostOkAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{s.Id}/operations", new OperationCaisseDto(500m, true, "Fonds de caisse", null), Guid.NewGuid().ToString());
        s = await c.PostOkAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{s.Id}/cloture", new ClotureCaisseDto(s.SoldeTheorique, null));
        // Validation sans écart : l'écran envoie un corps vide.
        s = await c.PostOkAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{s.Id}/validation", null, Guid.NewGuid().ToString());
        Assert.Equal(StatutSessionCaisse.Validee, s.Statut);
        Assert.Equal(0m, s.Ecart);
    }
}
