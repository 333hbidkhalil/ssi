using System.Globalization;
using System.Net;
using System.Text;
using Ssi.Contracts;
using Ssi.IntegrationTests.Infrastructure;

namespace Ssi.IntegrationTests;

/// <summary>Scénarios d'acceptation 10, 16, 17, 19 et 20 : rentabilité, contrôles, données sensibles, exports.</summary>
public class ScenariosPilotage(FabriqueApi api) : IClassFixture<FabriqueApi>, IAsyncLifetime
{
    private JeuDemo demo = null!;
    private ClientApi Admin => demo.Admin;
    private static string Cle() => Guid.NewGuid().ToString();
    private static readonly SemaphoreSlim Verrou = new(1, 1);
    private static bool prepare;
    private DateOnly Jour => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca")).DateTime);

    public async Task InitializeAsync()
    {
        demo = await api.DemoAsync();
        await Verrou.WaitAsync();
        try
        {
            if (prepare) return;
            // Stocks de départ, un poste validé (780,25 L GO et 500 L SP payés en espèces), une charge commune de 1 000 HT.
            await Admin.PostOkAsync<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(demo.Gasoil.Id, demo.CuveGo.Id, 10_000m, 11.20m, DateTimeOffset.UtcNow.AddDays(-2), "Stock de démarrage"), Cle());
            await Admin.PostOkAsync<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(demo.SansPlomb.Id, demo.CuveSp.Id, 8_000m, 12.40m, DateTimeOffset.UtcNow.AddDays(-2), "Stock de démarrage"), Cle());
            var poste = await demo.PosteAvecRelevesAsync(780.25m, 500m);
            await demo.Pompiste.PostOkAsync<ReglementPosteDto>($"/api/postes/{poste.Poste.Id}/reglements", new SaisieReglementPosteDto(ModeReglement.Especes, 17_126.21m, null, null, null, null, null));
            await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/cloture");
            await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/validation", null, Cle());
            var categories = await Admin.GetOkAsync<List<CategorieChargeDto>>("/api/charges/categories");
            var commun = (await Admin.GetOkAsync<List<CentreCoutDto>>("/api/charges/centres")).Single(c => c.Code == "COMMUN");
            await Admin.PostOkAsync<ChargeDto>("/api/charges", new CreationChargeDto(Jour, null, "ONEE", categories[0].Id, commun.Id, 1_000m, 200m, true, null,
                "Électricité", null, null, null), Cle());
            prepare = true;
        }
        finally { Verrou.Release(); }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Task<RentabiliteDto> RentabiliteAsync() => Admin.GetOkAsync<RentabiliteDto>($"/api/pilotage/rentabilite?du={Jour:yyyy-MM-dd}&au={Jour:yyyy-MM-dd}");

    [Fact(DisplayName = "Scénario 16 : rentabilité cohérente avec les relevés et les sorties de stock")]
    public async Task Scenario16_Rentabilite()
    {
        var r = await RentabiliteAsync();
        var go = r.Activites.Single(a => a.Activite == Activite.GasoilStation);
        var sp = r.Activites.Single(a => a.Activite == Activite.SansPlombStation);
        var caGoReleves = await api.ScalaireAsync<decimal>($"SELECT sum(r.montant_ht) FROM releves_index r JOIN pistolets p ON p.id = r.pistolet_id WHERE r.statut = 2 AND p.produit_id = '{demo.Gasoil.Id}'");
        Assert.Equal(caGoReleves, go.ChiffreAffairesHt);
        Assert.Equal(9_114.74m, go.ChiffreAffairesHt);
        Assert.Equal(8_738.80m, go.CoutDesVentes); // 780,25 L × 11,20
        Assert.Equal(go.ChiffreAffairesHt - go.CoutDesVentes, go.MargeBrute);
        Assert.Equal(780.25m, go.Quantite);
        Assert.Equal(6_200.00m, sp.CoutDesVentes); // 500 L × 12,40

        // Totaux = somme des lignes ; la charge commune est déduite une seule fois
        Assert.Equal(r.Activites.Sum(a => a.ChiffreAffairesHt), r.Total.ChiffreAffairesHt);
        Assert.Equal(r.Activites.Sum(a => a.MargeBrute), r.Total.MargeBrute);
        Assert.Equal(1_000m, r.Activites.Sum(a => a.ChargesReparties));
        Assert.Equal(r.Total.Contribution - 1_000m, r.Total.ResultatEstime);
        Assert.Equal(r.Activites.Sum(a => a.ResultatEstime), r.Total.ResultatEstime);
        Assert.True(r.Incomplet); // période en cours
        Assert.Contains("pas un résultat comptable", r.Methode);

        var seuil = await Admin.GetOkAsync<SeuilRentabiliteDto>($"/api/pilotage/seuil-rentabilite?du={Jour:yyyy-MM-dd}&au={Jour:yyyy-MM-dd}");
        Assert.Equal(1_000m, seuil.ChargesFixes);
        Assert.Equal(r.Total.ChiffreAffairesHt, seuil.ChiffreAffairesHt);

        // Simulation : aucune écriture
        var mouvements = await api.ScalaireAsync<long>("SELECT (SELECT count(*) FROM mouvements_stock) + (SELECT count(*) FROM mouvements_financiers) + (SELECT count(*) FROM journal_audit)");
        var sim = await Admin.PostOkAsync<SimulationDto>("/api/pilotage/simulations", new SimulationDemandeDto(Jour, Jour,
            [new HypotheseActiviteDto(Activite.GasoilStation, 0.10m, 0m, 0m)], 0m, 0m));
        Assert.Equal(Math.Round(780.25m * 0.10m, 2, MidpointRounding.AwayFromZero), sim.Ecart);
        Assert.Equal(mouvements, await api.ScalaireAsync<long>("SELECT (SELECT count(*) FROM mouvements_stock) + (SELECT count(*) FROM mouvements_financiers) + (SELECT count(*) FROM journal_audit)"));
    }

    [Fact(DisplayName = "Scénario 10 : un encaissement ou une facture de bons ne crée pas de chiffre d'affaires")]
    public async Task Scenario10_EncaissementSansCa()
    {
        var avant = (await RentabiliteAsync()).Total.ChiffreAffairesHt;
        var client = await Admin.PostOkAsync<ClientDto>("/api/clients", new EnregistrementClientDto($"C-{Guid.NewGuid():N}"[..10], "Société Sahara", TypeClient.Credit, null, null, null,
            null, null, null, null, null, 30, 0, true, null));
        var facture = await Admin.PostOkAsync<FactureDto>("/api/factures", new CreationFactureDto(SensFacture.Client, TypeFacture.Facture, OrigineFacture.Autre, client.Id, null, null,
            Jour, null, null, null, null, null, [new LigneFactureSaisieDto(null, "Refacturation", 1m, 100m, 0.20m, null, null)]));
        await Admin.PostOkAsync<FactureDto>($"/api/factures/{facture.Id}/validation", null, Cle());
        await Admin.PostOkAsync<ReglementDto>("/api/tresorerie/reglements", new CreationReglementDto(SensReglement.Encaissement, client.Id, null, null, ModeReglement.Especes,
            null, Jour, 120m, null, [new AffectationSaisieDto(facture.Id, null, 120m)]), Cle());
        var apres = (await RentabiliteAsync()).Total.ChiffreAffairesHt;
        Assert.Equal(avant, apres);
    }

    [Fact(DisplayName = "Scénario 17 : stocks et trésorerie se recalculent depuis les mouvements")]
    public async Task Scenario17_Controles()
    {
        var controles = await Admin.GetOkAsync<List<ResultatControleDto>>("/api/pilotage/controles");
        Assert.NotEmpty(controles);
        Assert.All(controles, c => Assert.True(c.Conforme, $"{c.Code} : {string.Join(" | ", c.Details)}"));
        var comptes = await Admin.GetOkAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes");
        foreach (var c in comptes)
            Assert.Equal(await api.ScalaireAsync<decimal>($"SELECT coalesce(sum(montant), 0) FROM mouvements_financiers WHERE compte_id = '{c.Id}'"), c.Solde);
        var tableau = await Admin.GetOkAsync<TableauBordDto>("/api/pilotage/tableau-de-bord");
        Assert.Equal(comptes.Where(c => c.Type is TypeCompteFinancier.Caisse or TypeCompteFinancier.Banque).Sum(c => c.Solde), tableau.TresorerieDisponible);
        Assert.True(tableau.Jour.VolumeGasoil >= 780.25m);
        Assert.DoesNotContain(tableau.Alertes, a => a.Message.Contains("fraude", StringComparison.OrdinalIgnoreCase) || a.Message.Contains("vol ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Scénario 19 : données sensibles masquées ou refusées selon les droits")]
    public async Task Scenario19_DonneesSensibles()
    {
        await Admin.PostOkAsync<EmployeDto>("/api/personnel/employes", new EnregistrementEmployeDto($"M{Guid.NewGuid():N}"[..8], "Alaoui", "Said", null, "Pompiste",
            new DateOnly(2024, 3, 1), "Équipe A", 3_500m, null, true));
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Pompiste.GetAsync("/api/personnel/employes")).Statut);
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Pompiste.GetAsync($"/api/pilotage/rentabilite?du={Jour:yyyy-MM-dd}&au={Jour:yyyy-MM-dd}")).Statut);
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Pompiste.GetAsync("/api/tresorerie/situation")).Statut);
        var employes = await demo.Responsable.GetOkAsync<List<EmployeDto>>("/api/personnel/employes");
        Assert.All(employes, e => Assert.Null(e.SalaireBase));
        Assert.Contains(await Admin.GetOkAsync<List<EmployeDto>>("/api/personnel/employes"), e => e.SalaireBase == 3_500m);
        var cuves = await demo.Responsable.GetOkAsync<List<EtatCuveDto>>("/api/stocks/cuves");
        Assert.All(cuves, c => { Assert.Null(c.Cmp); Assert.Null(c.Valeur); });
        Assert.All(await demo.Pompiste.GetOkAsync<List<ProduitDto>>("/api/produits"), p => Assert.Null(p.PrixAchatReferenceHt));
        // L'auditeur lit tout mais ne modifie rien
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Auditeur.PostAsync("/api/tresorerie/transferts", new CreationTransfertDto(Guid.NewGuid(), Guid.NewGuid(), 1m, 0m, Jour, "x"), Cle())).Statut);
        Assert.NotNull(await demo.Auditeur.GetOkAsync<RentabiliteDto>($"/api/pilotage/rentabilite?du={Jour:yyyy-MM-dd}&au={Jour:yyyy-MM-dd}"));
    }

    [Fact(DisplayName = "Scénario 20 : l'export CSV reprend exactement les lignes et totaux du rapport")]
    public async Task Scenario20_Exports()
    {
        var url = $"/api/rapports/ventes?du={Jour:yyyy-MM-dd}&au={Jour:yyyy-MM-dd}&produitId={demo.Gasoil.Id}";
        var rapport = await Admin.GetOkAsync<RapportDto>(url);
        Assert.NotEmpty(rapport.Lignes);
        Assert.Equal(9_114.74m, rapport.Totaux["ht"]);
        var csv = await Admin.GetAsync(url + "&format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.Statut);
        var lignes = csv.Corps.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
        Assert.Equal(rapport.Lignes.Count + 2, lignes.Count); // en-tête + lignes + total
        var entete = lignes[0].Split(';').ToList();
        var total = lignes[^1].Split(';');
        Assert.Equal("TOTAL", total[0]);
        Assert.Equal(rapport.Totaux["ht"], decimal.Parse(total[entete.IndexOf("Montant HT")], CultureInfo.GetCultureInfo("fr-MA")));
        var sommeLignes = lignes.Skip(1).SkipLast(1).Sum(l => decimal.Parse(l.Split(';')[entete.IndexOf("Montant HT")], CultureInfo.GetCultureInfo("fr-MA")));
        Assert.Equal(rapport.Totaux["ht"], sommeLignes);

        var xlsx = await Admin.Http.GetAsync(url + "&format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal("PK", Encoding.ASCII.GetString((await xlsx.Content.ReadAsByteArrayAsync())[..2]));
        var pdf = await Admin.Http.GetAsync(url + "&format=pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("%PDF", Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));
        // Le responsable consulte mais n'exporte pas
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Responsable.GetAsync(url + "&format=csv")).Statut);
    }

    [Fact(DisplayName = "Facture client imprimable en PDF après validation")]
    public async Task FacturePdf()
    {
        var client = await Admin.PostOkAsync<ClientDto>("/api/clients", new EnregistrementClientDto($"C-{Guid.NewGuid():N}"[..10], "Atlas Logistique", TypeClient.Credit, "002233445000011",
            null, null, "Zone industrielle", "Fès", null, null, null, 30, 0, true, null));
        var f = await Admin.PostOkAsync<FactureDto>("/api/factures", new CreationFactureDto(SensFacture.Client, TypeFacture.Facture, OrigineFacture.Autre, client.Id, null, null,
            Jour, null, null, null, null, null, [new LigneFactureSaisieDto(null, "Lavage camions", 4m, 150m, 0.20m, null, null)]));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Admin.GetAsync($"/api/rapports/factures/{f.Id}/pdf")).Statut);
        await Admin.PostOkAsync<FactureDto>($"/api/factures/{f.Id}/validation", null, Cle());
        var pdf = await Admin.Http.GetAsync($"/api/rapports/factures/{f.Id}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("%PDF", Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync())[..4]));
    }
}
