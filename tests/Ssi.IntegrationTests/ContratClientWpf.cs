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
        // Achats, paramétrage, personnel
        await a.GetOkAsync<List<CommandeFournisseurDto>>("/api/achats/commandes");
        await a.GetOkAsync<List<ReceptionDto>>("/api/achats/receptions");
        await a.GetOkAsync<List<ProduitDto>>("/api/produits");
        await a.GetOkAsync<List<FamilleDto>>("/api/produits/familles");
        await a.GetOkAsync<List<PrixDto>>($"/api/produits/{demo.Gasoil.Id}/prix");
        await a.GetOkAsync<List<VolucompteurDto>>("/api/equipements/volucompteurs");
        var mois = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd");
        var employes = await a.GetOkAsync<List<EmployeDto>>("/api/personnel/employes");
        await a.GetOkAsync<MasseSalarialeDto>($"/api/personnel/masse-salariale?mois={mois}");
        await a.GetOkAsync<List<ElementRemunerationDto>>($"/api/personnel/remunerations?periode={mois}");
        await a.GetOkAsync<List<AbsenceDto>>($"/api/personnel/absences?employeId={Guid.NewGuid()}");

        Assert.NotEmpty(catalogue);
        Assert.NotNull(r.Total);
        Assert.NotNull(postes);
    }

    [Fact(DisplayName = "Client WPF : chaque écran déclaré a son entrée dans le menu")]
    public void ChaqueEcranAuMenu()
    {
        // Lecture des sources : le projet WPF (net10.0-windows) n'est pas référençable depuis ce projet de tests.
        var racine = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(racine.FullName, "Ssi.slnx"))) racine = racine.Parent!;
        var client = Path.Combine(racine.FullName, "src", "Ssi.Client.Wpf");
        var ecrans = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Path.Combine(client, "App.xaml")), @"x:Type vm:(\w+ViewModel)")
            .Select(m => m.Groups[1].Value).ToList();
        var menu = File.ReadAllText(Path.Combine(client, "ViewModels", "ShellViewModel.cs"));
        Assert.True(ecrans.Count >= 15, $"{ecrans.Count} écrans déclarés");
        Assert.All(ecrans, e => Assert.Contains($"typeof({e})", menu));
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

    [Fact(DisplayName = "Client WPF : parcours achats de l'écran (commande, réception de la commande, facture rattachée)")]
    public async Task ParcoursAchats()
    {
        var demo = await api.DemoAsync();
        var a = demo.Admin;
        var code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var f = await a.PostOkAsync<FournisseurDto>("/api/fournisseurs", new EnregistrementFournisseurDto($"F-{code}", "Afriquia test", null, null, null, null, null,
            null, null, null, 30, null, null, true, null));
        var cuve = await a.PostOkAsync<CuveDto>("/api/equipements/cuves", new EnregistrementCuveDto($"CA-{code}", demo.Gasoil.Id, 30_000m, 0m, 100m, 0.5m, true, null));
        var cmd = await a.PostOkAsync<CommandeFournisseurDto>("/api/achats/commandes", new CreationCommandeFournisseurDto(f.Id, DateOnly.FromDateTime(DateTime.Today), null,
            DestinationAchat.Cuve, "REF-1", null, 0m, [new LigneCommandeFournisseurDto(demo.Gasoil.Id, cuve.Id, 5_000m, 11.00m)]));
        cmd = await a.PostOkAsync<CommandeFournisseurDto>($"/api/achats/commandes/{cmd.Id}/confirmation");
        Assert.Equal(StatutCommande.Confirmee, cmd.Statut);
        // L'écran reprend le reste à recevoir de la commande
        var l = cmd.Lignes.Single();
        var r = await a.PostOkAsync<ReceptionDto>("/api/achats/receptions", new CreationReceptionDto(cmd.Id, f.Id, null, "BL-1", null, null,
            [new LigneReceptionSaisieDto(l.ProduitId, l.CuveId, l.Quantite - l.QuantiteRecue, 5_000m, 4_990m, 4_990m, l.PrixUnitaireHt!.Value, "Écart de 10 L constaté au jaugeage")]));
        r = await a.PostOkAsync<ReceptionDto>($"/api/achats/receptions/{r.Id}/validation", null, Guid.NewGuid().ToString());
        Assert.Equal(StatutDocument.Valide, r.Statut);
        var fa = await a.PostOkAsync<FactureDto>("/api/factures", new CreationFactureDto(SensFacture.Fournisseur, TypeFacture.Facture, OrigineFacture.AchatStock,
            null, f.Id, $"FA-{code}", DateOnly.FromDateTime(DateTime.Today), null, null, null, r.Id, null,
            [.. r.Lignes.Select(x => new LigneFactureSaisieDto(x.ProduitId, null, x.QuantiteAcceptee, x.PrixUnitaireHt!.Value, null, null, r.Id))]));
        fa = await a.PostOkAsync<FactureDto>($"/api/factures/{fa.Id}/validation", null, Guid.NewGuid().ToString());
        Assert.Equal(StatutDocument.Valide, fa.Statut);
        Assert.Equal(54_890.00m, fa.TotalHt);   // 4 990 L × 11,00
    }

    [Fact(DisplayName = "Client WPF : avoir saisi sur une facture client et fiche employé modifiée sans accès aux salaires")]
    public async Task AvoirEtPersonnel()
    {
        var demo = await api.DemoAsync();
        var a = demo.Admin;
        var code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var client = await a.PostOkAsync<ClientDto>("/api/clients", new EnregistrementClientDto($"C-{code}", "Client avoir", TypeClient.Credit, null, null, null, null, null,
            null, null, null, 30, 0m, true, null));
        var jour = DateOnly.FromDateTime(DateTime.Today);
        var fc = await a.PostOkAsync<FactureDto>("/api/factures", new CreationFactureDto(SensFacture.Client, TypeFacture.Facture, OrigineFacture.Autre, client.Id, null, null,
            jour, null, null, null, null, null, [new LigneFactureSaisieDto(null, "Location d'emplacement", 1m, 1_000m, 0.20m, null, null)]));
        fc = await a.PostOkAsync<FactureDto>($"/api/factures/{fc.Id}/validation", null, Guid.NewGuid().ToString());
        var avoir = await a.PostOkAsync<FactureDto>("/api/factures", new CreationFactureDto(SensFacture.Client, TypeFacture.Avoir, OrigineFacture.Autre, client.Id, null, null,
            jour, null, fc.Id, null, null, "Geste commercial", [new LigneFactureSaisieDto(null, "Location d'emplacement", 1m, 250m, 0.20m, null, null)]));
        await a.PostOkAsync<FactureDto>($"/api/factures/{avoir.Id}/validation", null, Guid.NewGuid().ToString());
        Assert.Equal(900.00m, (await a.GetOkAsync<FactureDto>($"/api/factures/{fc.Id}")).ResteDu);   // 1 200 − 300

        // Un gestionnaire sans accès aux salaires modifie la fiche : le salaire n'est pas transmis et reste inchangé.
        var e = await a.PostOkAsync<EmployeDto>("/api/personnel/employes", new EnregistrementEmployeDto($"M{code}", "Bennani", "Amal", null, "Caissière",
            jour, "Boutique", (decimal?)4_500m, null, true));
        await a.PostOkAsync<RoleDto>("/api/admin/roles", new EnregistrementRoleDto($"GEST_{code}", "Gestion du personnel", ["personnel.consulter", "personnel.modifier"], "Création rôle RH de test"));
        await a.PostOkAsync<UtilisateurDto>("/api/admin/utilisateurs", new CreationUtilisateurDto($"rh{code}".ToLowerInvariant(), "RH", "Initial2026", [$"GEST_{code}"], null));
        var rh = api.Client();
        await rh.ConnecterAsync($"rh{code}".ToLowerInvariant(), "Initial2026");
        var s = await rh.PostOkAsync<SessionDto>("/api/auth/mot-de-passe", new ChangementMotDePasseDto("Initial2026", JeuDemo.MotDePasse));
        rh.Http.DefaultRequestHeaders.Authorization = new("Bearer", s.Jeton);
        var vu = (await rh.GetOkAsync<List<EmployeDto>>("/api/personnel/employes")).Single(x => x.Id == e.Id);
        Assert.Null(vu.SalaireBase);
        await rh.PutOkAsync<EmployeDto>($"/api/personnel/employes/{e.Id}", new EnregistrementEmployeDto(vu.Matricule, vu.Nom, vu.Prenom, vu.Cin, "Caissière principale",
            vu.DateEmbauche, vu.Affectation, null, vu.Telephone, vu.Actif));
        var apres = (await a.GetOkAsync<List<EmployeDto>>("/api/personnel/employes")).Single(x => x.Id == e.Id);
        Assert.Equal("Caissière principale", apres.Fonction);
        Assert.Equal(4_500m, apres.SalaireBase);
    }
}
