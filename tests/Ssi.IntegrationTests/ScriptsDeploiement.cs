using System.Text;

namespace Ssi.IntegrationTests;

/// <summary>Contrôles statiques des scripts d'installation et de sauvegarde livrés dans deploy/.</summary>
public class ScriptsDeploiement
{
    private static string Racine()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(d.FullName, "Ssi.slnx"))) d = d.Parent!;
        return d.FullName;
    }

    [Fact(DisplayName = "Scripts PowerShell : UTF-8 avec BOM, sinon Windows PowerShell 5.1 (tâche planifiée) corrompt les accents")]
    public void ScriptsAvecBom()
    {
        var scripts = Directory.GetFiles(Path.Combine(Racine(), "deploy"), "*.ps1");
        Assert.NotEmpty(scripts);
        Assert.All(scripts, s => Assert.True(File.ReadAllBytes(s).AsSpan().StartsWith(Encoding.UTF8.Preamble), $"{Path.GetFileName(s)} sans BOM UTF-8"));
    }
}
