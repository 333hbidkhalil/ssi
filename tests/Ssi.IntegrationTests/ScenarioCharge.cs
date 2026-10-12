using System.Diagnostics;
using System.Net;
using Ssi.Contracts;
using Ssi.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace Ssi.IntegrationTests;

/// <summary>
/// Essai de charge : 8 postes de travail simultanés, chacun avec son compte et son rôle, sur la même base.
/// Les écritures se disputent volontairement les mêmes lignes (stock d'un article, caisse principale,
/// séquences de numérotation, cuves) pour vérifier qu'aucune opération n'est perdue, doublée ou mal numérotée.
/// </summary>
public class ScenarioCharge(FabriqueApi api, ITestOutputHelper sortie) : IClassFixture<FabriqueApi>
{
    private const int Postes = 8;
    private const int Tours = 20;
    private static string Cle() => Guid.NewGuid().ToString();

    private sealed record Appel(int Poste, string Operation, HttpStatusCode Statut, double Ms, string Corps);

    [Fact(DisplayName = "Charge : 8 postes simultanés sans perte, doublon ni trou de numérotation")]
    public async Task HuitPostesSimultanes()
    {
        var demo = await api.DemoAsync();
        var a = demo.Admin;
        var code = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // Préparation : un article en stock, du carburant en cuve, une caisse approvisionnée, une banque, un client.
        var familles = await a.GetOkAsync<List<FamilleDto>>("/api/produits/familles");
        var taxes = await a.GetOkAsync<List<TaxeDto>>("/api/admin/taxes");
        var huile = await a.PostOkAsync<ProduitDto>("/api/produits", new EnregistrementProduitDto($"LUB-{code}", "Huile charge", familles.Single(f => f.Code == "LUB").Id,
            "U", null, null, taxes.Single(t => t.Code == "TVA_NORMAL").Id, 5m, null, true));
        await a.PostOkAsync<List<PrixDto>>($"/api/produits/{huile.Id}/prix", new NouveauPrixDto(60m, JeuDemo.DebutPrix, "Prix catalogue"), Cle());
        await a.PostOkAsync<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(huile.Id, null, 1_000m, 40m, null, "Inventaire de départ"), Cle());
        await a.PostOkAsync<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(demo.Gasoil.Id, demo.CuveGo.Id, 30_000m, 11.20m, null, "Stock de démarrage"), Cle());
        await a.PostOkAsync<MouvementStockDto>("/api/stocks/initial", new StockInitialDto(demo.SansPlomb.Id, demo.CuveSp.Id, 25_000m, 12.40m, null, "Stock de démarrage"), Cle());
        var caisse = (await a.GetOkAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes")).Single(c => c.Code == "CAISSE-PRINC");
        await a.PostOkAsync<CompteFinancierDto>("/api/tresorerie/comptes/solde-initial", new SoldeInitialCompteDto(caisse.Id, 100_000m, new DateOnly(2026, 10, 1), "Fonds de caisse de l'essai"), Cle());
        var banque = await a.PostOkAsync<CompteFinancierDto>("/api/tresorerie/comptes", new EnregistrementCompteDto($"BQ-{code}", "Banque essai", TypeCompteFinancier.Banque, null, null, true));
        var client = await a.PostOkAsync<ClientDto>("/api/clients", new EnregistrementClientDto($"C-{code}", "Client charge", TypeClient.Credit, null, null, null, null, null,
            null, null, null, 30, 0m, true, null));
        var soldeCaisseAvant = (await a.GetOkAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes")).Single(c => c.Id == caisse.Id).Solde;
        var numerosAvant = await NumerosAsync();

        // Huit comptes, un par poste, avec les rôles réels de l'exploitation.
        string[] roles = ["POMPISTE", "POMPISTE", "POMPISTE", "RESP_CAISSE", "RESP_COMMERCIAL", "RESP_STATION", "COMPTABLE", "AUDITEUR"];
        var clients = new ClientApi[Postes];
        for (var i = 0; i < Postes; i++)
        {
            var login = $"p{i + 1}{code}".ToLowerInvariant();
            await a.PostOkAsync<UtilisateurDto>("/api/admin/utilisateurs", new CreationUtilisateurDto(login, $"Poste {i + 1}", "Initial2026", [roles[i]], null));
            clients[i] = await api.ConnecterAsync(login, "Initial2026");
            var s = await clients[i].PostOkAsync<SessionDto>("/api/auth/mot-de-passe", new ChangementMotDePasseDto("Initial2026", JeuDemo.MotDePasse));
            clients[i].Http.DefaultRequestHeaders.Authorization = new("Bearer", s.Jeton);
        }

        var appels = new System.Collections.Concurrent.ConcurrentBag<Appel>();
        async Task<ReponseApi> Mesurer(int poste, string operation, Func<Task<ReponseApi>> appel)
        {
            var chrono = Stopwatch.StartNew();
            var r = await appel();
            appels.Add(new Appel(poste, operation, r.Statut, chrono.Elapsed.TotalMilliseconds, r.Corps));
            return r;
        }

        var ventes = new System.Collections.Concurrent.ConcurrentBag<VenteDto>();
        var transferts = new System.Collections.Concurrent.ConcurrentBag<decimal>();
        var jour = DateOnly.FromDateTime(DateTime.Today);
        var du = jour.AddDays(-7).ToString("yyyy-MM-dd");
        var au = jour.ToString("yyyy-MM-dd");
        var depart = new TaskCompletionSource();

        async Task Poste(int i)
        {
            var c = clients[i];
            await depart.Task;
            for (var t = 0; t < Tours; t++)
            {
                switch (roles[i])
                {
                    case "POMPISTE":
                    {
                        // Vente au comptoir ; un tour sur cinq, le poste renvoie la même requête (double clic, réseau coupé).
                        var cle = Cle();
                        var corps = new CreationVenteDto(null, ModeReglement.Especes, null, [new LigneVenteSaisieDto(huile.Id, 1m + t % 3, null)]);
                        var r = await Mesurer(i, "vente", () => c.PostAsync("/api/ventes", corps, cle));
                        if (r.Statut == HttpStatusCode.OK) ventes.Add(r.Lire<VenteDto>());
                        if (t % 5 == 0)
                        {
                            var rejeu = await Mesurer(i, "vente (rejeu)", () => c.PostAsync("/api/ventes", corps, cle));
                            if (rejeu.Statut == HttpStatusCode.OK && r.Statut == HttpStatusCode.OK)
                                Assert.Equal(r.Lire<VenteDto>().Id, rejeu.Lire<VenteDto>().Id);
                        }
                        await Mesurer(i, "lecture ventes", () => c.GetAsync($"/api/ventes?du={au}&taille=200"));
                        break;
                    }
                    case "RESP_CAISSE":
                    {
                        var r = await Mesurer(i, "vente", () => c.PostAsync("/api/ventes",
                            new CreationVenteDto(null, ModeReglement.Especes, null, [new LigneVenteSaisieDto(huile.Id, 1m, null)]), Cle()));
                        if (r.Statut == HttpStatusCode.OK) ventes.Add(r.Lire<VenteDto>());
                        var montant = 100m + t;
                        var v = await Mesurer(i, "versement banque", () => c.PostAsync("/api/tresorerie/transferts",
                            new CreationTransfertDto(caisse.Id, banque.Id, montant, 0m, jour, $"Versement {t}"), Cle()));
                        if (v.Statut == HttpStatusCode.OK) transferts.Add(montant);
                        await Mesurer(i, "lecture comptes", () => c.GetAsync("/api/tresorerie/comptes"));
                        break;
                    }
                    case "RESP_COMMERCIAL":
                    {
                        var f = await Mesurer(i, "facture", () => c.PostAsync("/api/factures", new CreationFactureDto(SensFacture.Client, TypeFacture.Facture,
                            OrigineFacture.Autre, client.Id, null, null, jour, null, null, null, null, null,
                            [new LigneFactureSaisieDto(null, "Prestation de lavage", 1m, 200m, 0.20m, null, null)])));
                        if (f.Statut == HttpStatusCode.OK)
                            await Mesurer(i, "validation facture", () => c.PostAsync($"/api/factures/{f.Lire<FactureDto>().Id}/validation", null, Cle()));
                        break;
                    }
                    case "RESP_STATION":
                    {
                        // Cycle complet d'un poste de pompe : ouverture, deux relevés, clôture, validation.
                        var o = await Mesurer(i, "ouverture poste", () => c.PostAsync("/api/postes", new OuverturePosteDto(null, null, null)));
                        if (o.Statut != HttpStatusCode.OK) break;
                        var id = o.Lire<PosteDetailDto>().Poste.Id;
                        var prep = (await c.GetOkAsync<List<PreparationReleveDto>>($"/api/postes/{id}/preparation"));
                        foreach (var p in new[] { demo.PistoletGo, demo.PistoletSp })
                            await Mesurer(i, "relevé", () => c.PostAsync($"/api/postes/{id}/releves",
                                new SaisieReleveDto(p.Id, null, prep.Single(x => x.PistoletId == p.Id).IndexDebutPropose + 150m, null, null, true, null)));
                        await Mesurer(i, "clôture poste", () => c.PostAsync($"/api/postes/{id}/cloture"));
                        await Mesurer(i, "validation poste", () => c.PostAsync($"/api/postes/{id}/validation", null, Cle()));
                        break;
                    }
                    case "COMPTABLE":
                        await Mesurer(i, "tableau de bord", () => c.GetAsync("/api/pilotage/tableau-de-bord"));
                        await Mesurer(i, "rentabilité", () => c.GetAsync($"/api/pilotage/rentabilite?du={du}&au={au}"));
                        await Mesurer(i, "rapport ventes", () => c.GetAsync($"/api/rapports/ventes?format=json&du={du}&au={au}"));
                        break;
                    default:
                        await Mesurer(i, "contrôles", () => c.GetAsync("/api/pilotage/controles"));
                        await Mesurer(i, "mouvements stock", () => c.GetAsync($"/api/stocks/mouvements?du={du}&taille=300"));
                        break;
                }
            }
        }

        var taches = Enumerable.Range(0, Postes).Select(Poste).ToList();
        var chronoTotal = Stopwatch.StartNew();
        depart.SetResult();
        await Task.WhenAll(taches);
        chronoTotal.Stop();

        // Rapport de l'essai (visible avec --logger "console;verbosity=detailed").
        sortie.WriteLine($"{appels.Count} appels en {chronoTotal.Elapsed.TotalSeconds:0.0} s ({appels.Count / chronoTotal.Elapsed.TotalSeconds:0} appels/s)");
        foreach (var g in appels.GroupBy(x => x.Operation).OrderBy(g => g.Key))
        {
            var ms = g.Select(x => x.Ms).OrderBy(x => x).ToList();
            sortie.WriteLine($"{g.Key,-20} n={ms.Count,3}  médiane={ms[ms.Count / 2],6:0} ms  p95={ms[(int)(ms.Count * 0.95)],6:0} ms  max={ms[^1],6:0} ms  " +
                             $"statuts={string.Join(",", g.GroupBy(x => (int)x.Statut).Select(s => $"{s.Key}×{s.Count()}"))}");
        }

        // 1. Aucune erreur : ni erreur serveur, ni conflit de concurrence remonté à l'utilisateur.
        var echecs = appels.Where(x => (int)x.Statut >= 300).ToList();
        Assert.True(echecs.Count == 0, string.Join("\n", echecs.Take(5).Select(x => $"{x.Operation} {(int)x.Statut} {x.Corps}")));

        // 2. Aucune vente perdue ni doublée par les rejeux, et le stock de l'article est exact.
        var nbVentes = await api.ScalaireAsync<long>($"SELECT count(DISTINCT v.id) FROM ventes_articles v JOIN lignes_vente_article l ON l.vente_id = v.id WHERE l.produit_id = '{huile.Id}'");
        Assert.Equal(ventes.Count, nbVentes);
        var vendu = ventes.Sum(v => v.Lignes.Sum(l => l.Quantite));
        var article = (await a.GetOkAsync<List<PositionArticleDto>>("/api/stocks/articles")).Single(p => p.ProduitId == huile.Id);
        Assert.Equal(1_000m - vendu, article.Quantite);

        // 3. Caisse et banque : chaque encaissement et chaque versement est compté une fois.
        var comptes = await a.GetOkAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes");
        var encaissementsPostes = await api.ScalaireAsync<decimal>(
            $"SELECT coalesce(sum(montant), 0) FROM mouvements_financiers WHERE compte_id = '{caisse.Id}' AND nature = {(int)NatureMouvementFinancier.EncaissementVenteStation}");
        Assert.Equal(soldeCaisseAvant + ventes.Sum(v => v.TotalTtc) - transferts.Sum() + encaissementsPostes, comptes.Single(c => c.Id == caisse.Id).Solde);
        Assert.Equal(transferts.Sum(), comptes.Single(c => c.Id == banque.Id).Solde);

        // 4. Numérotation : chaque type de document reste continu, sans trou ni doublon.
        var numerosApres = await NumerosAsync();
        foreach (var (type, numeros) in numerosApres)
        {
            var nouveaux = numeros.Except(numerosAvant.GetValueOrDefault(type, [])).ToList();
            if (nouveaux.Count == 0) continue;
            Assert.Equal(nouveaux.Count, nouveaux.Distinct().Count());
            var max = numeros.Max();
            Assert.True(numeros.Count == max, $"{type} : {numeros.Count} documents pour un dernier numéro {max} (trou dans la numérotation)");
        }

        // 5. Les contrôles transverses et le recalcul des stocks depuis les mouvements restent conformes.
        Assert.All(await a.GetOkAsync<List<ControleCoherenceDto>>("/api/stocks/controle-coherence"), x => Assert.True(x.Coherent, x.Libelle));
        var controles = await a.GetOkAsync<List<ResultatControleDto>>("/api/pilotage/controles");
        Assert.All(controles.Where(x => !x.Conforme), x => sortie.WriteLine($"Contrôle non conforme : {x.Libelle} {string.Join("; ", x.Details)}"));
        Assert.All(controles, x => Assert.True(x.Conforme, $"{x.Libelle} : {string.Join("; ", x.Details)}"));

        // 6. Temps de réponse : seuil volontairement large (machine de test partagée), à mesurer sur le serveur réel.
        var p95 = appels.Select(x => x.Ms).OrderBy(x => x).ElementAt((int)(appels.Count * 0.95));
        Assert.True(p95 < 2_000, $"p95 = {p95:0} ms");
    }

    /// <summary>Numéros séquentiels par type de document (ventes, factures, postes, virements).</summary>
    private async Task<Dictionary<string, List<int>>> NumerosAsync()
    {
        var resultat = new Dictionary<string, List<int>>();
        foreach (var (type, sql) in new[]
        {
            ("VTE", "SELECT numero FROM ventes_articles"), ("FAC", "SELECT numero FROM factures WHERE numero LIKE 'FAC-%'"),
            ("PST", "SELECT numero FROM postes_travail"), ("VIR", "SELECT numero FROM transferts_internes"),
        })
        {
            var liste = await api.ScalaireAsync<string>($"SELECT coalesce(string_agg(numero, ','), '') FROM ({sql}) n");
            resultat[type] = liste.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => int.Parse(n[(n.LastIndexOf('-') + 1)..])).ToList();
        }
        return resultat;
    }
}
