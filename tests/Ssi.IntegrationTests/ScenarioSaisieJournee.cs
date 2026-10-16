using System.Net;
using Ssi.Contracts;
using Ssi.IntegrationTests.Infrastructure;

namespace Ssi.IntegrationTests;

/// <summary>Saisie de la journée au format du tableau de l'exploitant : production, clients, charges, solde.</summary>
public class ScenarioSaisieJournee(FabriqueApi api) : IClassFixture<FabriqueApi>, IAsyncLifetime
{
    private JeuDemo demo = null!;

    public async Task InitializeAsync() => demo = await api.DemoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Saisie de la journée : production, bons clients, charges payées en caisse et solde, en une seule opération")]
    public async Task JourneeComplete()
    {
        var a = demo.Admin;
        var jour = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca")).DateTime);
        var prep = await a.GetOkAsync<PreparationJourneeDto>($"/api/journees/preparation?journee={jour:yyyy-MM-dd}");
        Assert.Null(prep.Blocage);
        var go = prep.Index.Single(i => i.PistoletId == demo.PistoletGo.Id);
        var sp = prep.Index.Single(i => i.PistoletId == demo.PistoletSp.Id);
        Assert.Equal(12.85m, go.PrixUnitaireTtc);
        Assert.Equal(14.20m, sp.PrixUnitaireTtc);

        var client = await a.PostOkAsync<ClientDto>("/api/clients", new EnregistrementClientDto("C-JOUR", "Transports X1", TypeClient.Credit, null, null, null,
            null, null, null, null, null, 30, 0, true, null));
        var eau = (await a.GetOkAsync<List<CategorieChargeDto>>("/api/charges/categories")).Single(c => c.Code == "ENERGIE");
        var index = new List<IndexJourneeDto> { new(go.PistoletId, go.IndexDebutPropose + 300m), new(sp.PistoletId, sp.IndexDebutPropose + 100m) };
        var bons = new List<BonClientJourneeDto> { new(client.Id, demo.Gasoil.Id, "123", "321", 20m, 12.85m) };

        // Tout ou rien : une charge invalide annule la journée entière (aucun poste, aucun bon n'est conservé).
        var postesAvant = await api.ScalaireAsync<long>("SELECT count(*) FROM postes_travail");
        var refus = await a.PostAsync("/api/journees", new SaisieJourneeDto(jour, index, bons,
            [new ChargeJourneeDto(Guid.NewGuid(), "ONE", 35.32m)], 100m, true), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, refus.Statut);
        Assert.Equal(postesAvant, await api.ScalaireAsync<long>("SELECT count(*) FROM postes_travail"));

        // Production 300 × 12,85 + 100 × 14,20 = 5 275,00 ; clients 257,00 ; charges 35,32 ; solde attendu 4 982,68 ; remis 4 972,68.
        var r = await a.PostOkAsync<ResultatJourneeDto>("/api/journees", new SaisieJourneeDto(jour, index, bons,
            [new ChargeJourneeDto(eau.Id, "ONE", 35.32m)], 4_972.68m, true), Guid.NewGuid().ToString());
        Assert.Equal(5_275.00m, r.Production);
        Assert.Equal(257.00m, r.Clients);
        Assert.Equal(35.32m, r.Charges);
        Assert.Equal(4_982.68m, r.SoldeAttendu);
        Assert.Equal(-10.00m, r.Ecart);
        Assert.Single(r.NumerosCharges);
        Assert.Equal(StatutPoste.Cloture, r.Poste.Poste.Statut);
        Assert.Equal(2, r.Poste.Releves.Count);

        var bon = r.Poste.Reglements.Single(x => x.Mode == ModeReglement.Credit);
        Assert.Equal(("321", "123", 257.00m, 20m), (bon.Reference, bon.ReferenceCommande, bon.Montant, bon.Volume!.Value));
        // Espèces encaissées = remises + charges payées sur la recette ; l'écart du poste est celui du solde.
        Assert.Equal(5_008.00m, r.Poste.Reglements.Single(x => x.Mode == ModeReglement.Especes).Montant);
        Assert.Equal(10.00m, Math.Abs(r.Poste.Rapport.Ecart));

        // La charge est enregistrée ; elle sera réglée par la caisse quand la recette y entrera (validation du poste).
        async Task<ChargeDto> Charge() => (await a.GetOkAsync<List<ChargeDto>>($"/api/charges?du={jour:yyyy-MM-dd}&au={jour:yyyy-MM-dd}"))
            .Single(c => c.Numero == r.NumerosCharges[0]);
        Assert.Equal((35.32m, 35.32m, "ONE"), ((await Charge()).MontantTtc, (await Charge()).ResteDu, (await Charge()).Tiers));

        // Validation habituelle : la caisse reçoit les espèces encaissées, la charge en sort ; le solde net est celui remis.
        await a.PostOkAsync<PosteDetailDto>($"/api/postes/{r.Poste.Poste.Id}/validation", null, Guid.NewGuid().ToString());
        var caisse = "SELECT coalesce(sum(m.montant), 0) FROM mouvements_financiers m JOIN comptes_financiers c ON c.id = m.compte_id WHERE c.type = 1";
        Assert.Equal(5_008.00m, await api.ScalaireAsync<decimal>($"{caisse} AND m.poste_id = '{r.Poste.Poste.Id}'"));
        Assert.Equal(4_972.68m, await api.ScalaireAsync<decimal>(caisse));
        Assert.Equal(0m, (await Charge()).ResteDu);

        // Réouverture : la recette et le paiement de la charge sont contre-passés ; la charge redevient à payer.
        await a.PostOkAsync<PosteDetailDto>($"/api/postes/{r.Poste.Poste.Id}/reouverture", new MotifDto("Erreur de saisie des espèces remises"));
        Assert.Equal(0m, await api.ScalaireAsync<decimal>(caisse));
        Assert.Equal(35.32m, (await Charge()).ResteDu);
        // Nouvelle validation : tout est de nouveau comptabilisé, une seule fois.
        await a.PostOkAsync<PosteDetailDto>($"/api/postes/{r.Poste.Poste.Id}/validation", null, Guid.NewGuid().ToString());
        Assert.Equal(4_972.68m, await api.ScalaireAsync<decimal>(caisse));
        Assert.Equal(0m, (await Charge()).ResteDu);

        // Un index inhabituel n'est enregistré qu'après confirmation ; sans elle, rien n'est conservé.
        var suite = await a.GetOkAsync<PreparationJourneeDto>($"/api/journees/preparation?journee={jour:yyyy-MM-dd}");
        var inhabituel = suite.Index.Select(i => new IndexJourneeDto(i.PistoletId, i.IndexDebutPropose + (i.PistoletId == go.PistoletId ? 50_000m : 0m))).ToList();
        var postes = await api.ScalaireAsync<long>("SELECT count(*) FROM postes_travail");
        var aConfirmer = await a.PostAsync("/api/journees", new SaisieJourneeDto(jour, inhabituel, [], [], 0m, false), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, aConfirmer.Statut);
        Assert.Contains("CONFIRMATION_REQUISE", aConfirmer.Corps);
        Assert.Equal(postes, await api.ScalaireAsync<long>("SELECT count(*) FROM postes_travail"));
    }
}
