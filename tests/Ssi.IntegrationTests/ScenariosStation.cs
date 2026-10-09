using System.Net;
using Ssi.Contracts;
using Ssi.IntegrationTests.Infrastructure;

namespace Ssi.IntegrationTests;

/// <summary>Scénarios d'acceptation 1, 2, 3, 15 et 19 (partie station).</summary>
public class ScenariosStation(FabriqueApi api) : IClassFixture<FabriqueApi>, IAsyncLifetime
{
    private JeuDemo demo = null!;

    public async Task InitializeAsync() => demo = await api.DemoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "Scénario 1 : le volume d'un relevé est la différence des index")]
    public async Task Scenario01_VolumeIndex()
    {
        var poste = await demo.PosteAvecRelevesAsync(780.25m, 100m);
        var go = poste.Releves.Single(r => r.PistoletId == demo.PistoletGo.Id);
        Assert.Equal(780.25m, go.IndexFin - go.IndexDebut);
        Assert.Equal(780.25m, go.Volume);
        Assert.Equal(12.85m, go.PrixUnitaireTtc);
        Assert.Equal(10_026.21m, go.MontantTtc);
        Assert.Equal(9_114.74m, go.MontantHt);
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/cloture");
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/validation", null, Guid.NewGuid().ToString());
    }

    [Fact(DisplayName = "Scénario 2 : un relevé ne peut pas être comptabilisé deux fois")]
    public async Task Scenario02_PasDeDoubleComptage()
    {
        var poste = await demo.PosteAvecRelevesAsync(500m, 200m);
        var id = poste.Poste.Id;

        // Deuxième relevé du même pistolet dans le même poste
        var doublon = await demo.Pompiste.PostAsync($"/api/postes/{id}/releves", new SaisieReleveDto(demo.PistoletGo.Id, null, 127_100m, null, null, true, null));
        Assert.Equal(HttpStatusCode.Conflict, doublon.Statut);

        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{id}/cloture");
        var cle = Guid.NewGuid().ToString();
        var v1 = await demo.Caissier.PostAsync($"/api/postes/{id}/validation", null, cle);
        var v2 = await demo.Caissier.PostAsync($"/api/postes/{id}/validation", null, cle); // double clic / renvoi
        var v3 = await demo.Caissier.PostAsync($"/api/postes/{id}/validation", null, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, v1.Statut);
        Assert.Equal(HttpStatusCode.OK, v2.Statut);
        Assert.True(v1.Corps == v2.Corps, $"{v1.Corps}\n-----\n{v2.Corps}");
        Assert.Equal(HttpStatusCode.Conflict, v3.Statut);

        var mouvements = await api.ScalaireAsync<long>($"SELECT count(*) FROM mouvements_stock WHERE document_id = '{id}'");
        Assert.Equal(2, mouvements); // un par pistolet
        var sansCle = await demo.Caissier.PostAsync($"/api/postes/{id}/validation");
        Assert.Equal(HttpStatusCode.BadRequest, sansCle.Statut);
    }

    [Fact(DisplayName = "Scénario 3 : un changement de prix n'altère pas les ventes historiques")]
    public async Task Scenario03_PrixHistorique()
    {
        var poste = await demo.PosteAvecRelevesAsync(780.25m, 300m);
        var id = poste.Poste.Id;
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{id}/cloture");
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{id}/validation", null, Guid.NewGuid().ToString());
        var avant = await demo.Caissier.GetOkAsync<PosteDetailDto>($"/api/postes/{id}");

        await demo.Admin.PostOkAsync<List<PrixDto>>($"/api/produits/{demo.Gasoil.Id}/prix",
            new NouveauPrixDto(13.10m, DateTimeOffset.UtcNow.AddSeconds(1), "Hausse du prix réglementé"), Guid.NewGuid().ToString());

        var apres = await demo.Caissier.GetOkAsync<PosteDetailDto>($"/api/postes/{id}");
        var go = apres.Releves.Single(r => r.PistoletId == demo.PistoletGo.Id);
        Assert.Equal(12.85m, go.PrixUnitaireTtc);
        Assert.Equal(avant.Rapport.MontantTheoriqueTtc, apres.Rapport.MontantTheoriqueTtc);
        var historique = await demo.Admin.GetOkAsync<List<PrixDto>>($"/api/produits/{demo.Gasoil.Id}/prix");
        Assert.True(historique.Count >= 2);
        Assert.NotNull(historique.Single(p => p.Prix == 12.85m).Fin);

        // Audit du changement de prix
        var audit = await demo.Auditeur.GetOkAsync<Page<EntreeAuditDto>>($"/api/admin/audit?action=MODIFICATION_PRIX&objetId={demo.Gasoil.Id}");
        Assert.Contains(audit.Elements, e => e.Motif == "Hausse du prix réglementé" && e.Avant!.Contains("12.85"));
    }

    [Fact(DisplayName = "Scénario 15 : deux utilisateurs ne peuvent pas valider simultanément la même opération")]
    public async Task Scenario15_ValidationSimultanee()
    {
        var poste = await demo.PosteAvecRelevesAsync(720m, 400m);
        var id = poste.Poste.Id;
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{id}/cloture");
        var autre = await api.ConnecterAsync("admin", JeuDemo.MotDePasse);
        var taches = Enumerable.Range(0, 6).Select(i =>
            (i % 2 == 0 ? demo.Caissier : autre).PostAsync($"/api/postes/{id}/validation", null, Guid.NewGuid().ToString())).ToList();
        var resultats = await Task.WhenAll(taches);
        Assert.Single(resultats, r => r.Statut == HttpStatusCode.OK);
        Assert.All(resultats.Where(r => r.Statut != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.Statut));
        Assert.Equal(2, await api.ScalaireAsync<long>($"SELECT count(*) FROM mouvements_stock WHERE document_id = '{id}'"));
    }

    [Fact(DisplayName = "Contrôles d'index : fin inférieure refusée, continuité, avertissements à confirmer")]
    public async Task ControlesIndex()
    {
        var poste = await demo.Pompiste.PostOkAsync<PosteDetailDto>("/api/postes", new OuverturePosteDto(null, null, null));
        var id = poste.Poste.Id;
        var prep = await demo.Pompiste.GetOkAsync<List<PreparationReleveDto>>($"/api/postes/{id}/preparation");
        var debutGo = prep.Single(p => p.PistoletId == demo.PistoletGo.Id).IndexDebutPropose;

        var inferieur = await demo.Pompiste.PostAsync($"/api/postes/{id}/releves", new SaisieReleveDto(demo.PistoletGo.Id, null, debutGo - 1, null, null, true, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, inferieur.Statut);

        var discontinu = await demo.Pompiste.PostOkAsync<ResultatReleveDto>($"/api/postes/{id}/releves",
            new SaisieReleveDto(demo.PistoletGo.Id, debutGo + 50, debutGo + 100, null, null, true, null));
        Assert.False(discontinu.Enregistre);
        Assert.Contains(discontinu.Anomalies, a => a.Code == "CONTINUITE");

        // Le pompiste n'a pas le droit de corriger un index
        var correctionRefusee = await demo.Pompiste.PostAsync($"/api/postes/{id}/releves",
            new SaisieReleveDto(demo.PistoletGo.Id, debutGo + 50, debutGo + 100, null, null, true, "Erreur de relève du poste précédent"));
        Assert.Equal(HttpStatusCode.Forbidden, correctionRefusee.Statut);

        var inhabituel = await demo.Pompiste.PostOkAsync<ResultatReleveDto>($"/api/postes/{id}/releves",
            new SaisieReleveDto(demo.PistoletGo.Id, null, debutGo + 50_000m, null, null, false, null));
        Assert.False(inhabituel.Enregistre);
        Assert.Contains(inhabituel.Anomalies, a => !a.Bloquante);

        var ok = await demo.Pompiste.PostOkAsync<ResultatReleveDto>($"/api/postes/{id}/releves",
            new SaisieReleveDto(demo.PistoletGo.Id, null, debutGo + 700m, null, null, false, null));
        Assert.True(ok.Enregistre, ok.Message);

        // Clôture refusée : pistolet SP sans relevé
        var cloture = await demo.Caissier.PostAsync($"/api/postes/{id}/cloture");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cloture.Statut);
        Assert.Contains("Absence de relève", cloture.Corps);
        var debutSp = prep.Single(p => p.PistoletId == demo.PistoletSp.Id).IndexDebutPropose;
        await demo.Pompiste.PostOkAsync<ResultatReleveDto>($"/api/postes/{id}/releves", new SaisieReleveDto(demo.PistoletSp.Id, null, debutSp, null, null, true, null));
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{id}/cloture");
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{id}/validation", null, Guid.NewGuid().ToString());
    }

    [Fact(DisplayName = "Scénario 19 (station) : le pompiste ne peut ni modifier un prix ni administrer")]
    public async Task Scenario19_PermissionsStation()
    {
        var prix = await demo.Pompiste.PostAsync($"/api/produits/{demo.Gasoil.Id}/prix", new NouveauPrixDto(1m, DateTimeOffset.UtcNow.AddDays(1), "Tentative non autorisée"), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, prix.Statut);
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Pompiste.GetAsync("/api/admin/utilisateurs")).Statut);
        Assert.Equal(HttpStatusCode.Forbidden, (await demo.Pompiste.GetAsync("/api/admin/audit")).Statut);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client().GetAsync("/api/postes")).Statut);
    }
}
