using System.Diagnostics;
using Npgsql;
using Ssi.Contracts;
using Ssi.IntegrationTests.Infrastructure;

namespace Ssi.IntegrationTests;

/// <summary>Scénario 18 : sauvegarde vérifiée puis restauration sur une base vierge, totaux de contrôle identiques.</summary>
public class ScenarioSauvegarde(FabriqueApi api) : IClassFixture<FabriqueApi>, IAsyncLifetime
{
    private JeuDemo demo = null!;
    public async Task InitializeAsync() => demo = await api.DemoAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static string Racine()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "deploy", "controles.sql"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Dossier deploy introuvable.");
    }

    private static (int Code, string Sortie) Executer(string script, params string[] args)
    {
        var b = new NpgsqlConnectionStringBuilder(FabriqueApi.Serveur);
        var psi = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(Path.Combine(Racine(), "deploy", script));
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["PGHOST"] = b.Host;
        psi.Environment["PGPORT"] = b.Port.ToString();
        psi.Environment["PGUSER"] = b.Username;
        if (!string.IsNullOrEmpty(b.Password)) psi.Environment["PGPASSWORD"] = b.Password;
        using var p = Process.Start(psi)!;
        var sortie = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(120_000);
        return (p.ExitCode, sortie);
    }

    [Fact(DisplayName = "Scénario 18 : sauvegarde vérifiée et restauration identique")]
    public async Task Scenario18_SauvegardeRestauration()
    {
        if (OperatingSystem.IsWindows()) return; // Sous Windows : deploy/sauvegarder.ps1, même logique.
        // Quelques opérations à sauvegarder
        var poste = await demo.PosteAvecRelevesAsync(120m, 80m);
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/cloture");
        await demo.Caissier.PostOkAsync<PosteDetailDto>($"/api/postes/{poste.Poste.Id}/validation", null, Guid.NewGuid().ToString());

        var dossier = Path.Combine(Path.GetTempPath(), "ssi-sauvegardes-" + Guid.NewGuid().ToString("N")[..8]);
        var (code, sortie) = Executer("sauvegarder.sh", api.NomBase, dossier);
        Assert.True(code == 0, sortie);
        Assert.Equal("VERIFIEE", await api.ScalaireAsync<string>("SELECT statut FROM sauvegardes ORDER BY id DESC LIMIT 1"));
        var fichier = Directory.GetFiles(dossier, "*.dump").Single();
        Assert.True(File.Exists(fichier + ".sha256"));

        var cible = api.NomBase + "_r";
        try
        {
            (code, sortie) = Executer("restaurer.sh", fichier, cible);
            Assert.True(code == 0, sortie);
            Assert.Contains("Totaux de contrôle identiques", sortie);
            // La restauration n'écrase jamais une base existante
            (code, _) = Executer("restaurer.sh", fichier, cible);
            Assert.NotEqual(0, code);
            // Un fichier altéré est détecté
            await File.AppendAllTextAsync(fichier, "x");
            (code, sortie) = Executer("restaurer.sh", fichier, cible + "2");
            Assert.NotEqual(0, code);
            Assert.Contains("SHA-256", sortie);
        }
        finally
        {
            await using var c = new NpgsqlConnection($"{FabriqueApi.Serveur};Database=postgres");
            await c.OpenAsync();
            foreach (var b in new[] { cible, cible + "2" })
            {
                await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {b} WITH (FORCE)", c);
                await cmd.ExecuteNonQueryAsync();
            }
            Directory.Delete(dossier, true);
        }
    }
}
