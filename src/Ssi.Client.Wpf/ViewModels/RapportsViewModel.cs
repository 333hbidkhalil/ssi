using System.Collections.ObjectModel;
using System.Data;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Rapports filtrables, exports CSV / Excel / PDF (mêmes données que l'écran) et contrôles de cohérence transverses.</summary>
public sealed partial class RapportsViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Rapports et exports";

    public ObservableCollection<KeyValuePair<string, string>> Catalogue { get; } = [];
    public ObservableCollection<ResultatControleDto> Controles { get; } = [];
    public ObservableCollection<ProduitDto> Produits { get; } = [];
    public ObservableCollection<CompteFinancierDto> Comptes { get; } = [];

    [ObservableProperty] private KeyValuePair<string, string>? rapport;
    [ObservableProperty] private DateTime? du = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? au = DateTime.Today;
    [ObservableProperty] private ProduitDto? produit;
    [ObservableProperty] private CompteFinancierDto? compte;
    [ObservableProperty] private DataView? donnees;
    [ObservableProperty] private string? totaux;
    [ObservableProperty] private string? entete;
    [ObservableProperty] private ResultatControleDto? controle;

    public bool PeutExporter => session.A(Droits.RapportsExporter);

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Remplir(Catalogue, await api.GetAsync<Dictionary<string, string>>("/api/rapports"));
        Rapport ??= Catalogue.FirstOrDefault();
        if (session.A(Droits.CatalogueConsulter)) Remplir(Produits, await api.GetAsync<List<ProduitDto>>("/api/produits?actif=true"));
        if (session.A(Droits.BanqueConsulter, Droits.CaisseConsulter)) Remplir(Comptes, await api.GetAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes"));
    });

    private string Url(string format)
    {
        var url = $"/api/rapports/{Rapport!.Value.Key}?format={format}&du={Jour(Du):yyyy-MM-dd}&au={Jour(Au):yyyy-MM-dd}";
        if (Produit is not null) url += $"&produitId={Produit.Id}";
        if (Compte is not null) url += $"&compteId={Compte.Id}";
        return url;
    }

    [RelayCommand]
    private Task Generer() => ExecuterAsync(async () =>
    {
        if (Rapport is null) return;
        if (Rapport.Value.Key == "tresorerie" && Compte is null)
            throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Filtre", "Le journal de trésorerie se consulte par compte : choisissez le compte.", null);
        var r = await api.GetAsync<RapportDto>(Url("json"));
        var table = new DataTable();
        foreach (var c in r.Colonnes)
            table.Columns.Add(c.Libelle, c.Type is "montant" or "quantite" or "decimal" or "nombre" ? typeof(decimal) : typeof(string));
        foreach (var ligne in r.Lignes)
        {
            var row = table.NewRow();
            foreach (var c in r.Colonnes)
                row[c.Libelle] = ligne.TryGetValue(c.Cle, out var v) ? Convertir(v, table.Columns[c.Libelle]!.DataType) : DBNull.Value;
            table.Rows.Add(row);
        }
        Donnees = table.DefaultView;
        Entete = $"{r.Titre} — {r.Filtres} — généré le {r.GenereLe.LocalDateTime:dd/MM/yyyy HH:mm} par {r.GenerePar}";
        Totaux = r.Totaux.Count == 0 ? null : "Totaux : " + string.Join("   ", r.Totaux.Select(t => $"{r.Colonnes.FirstOrDefault(c => c.Cle == t.Key)?.Libelle ?? t.Key} = {t.Value:N2}"));
    });

    private static object Convertir(object? v, Type type)
    {
        if (v is not JsonElement e || e.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return v ?? DBNull.Value;
        if (type == typeof(decimal) && e.ValueKind == JsonValueKind.Number) return e.GetDecimal();
        return e.ValueKind switch
        {
            JsonValueKind.String => DateOnly.TryParseExact(e.GetString(), "yyyy-MM-dd", out var d) ? d.ToString("dd/MM/yyyy") : e.GetString() ?? "",
            JsonValueKind.True => "Oui",
            JsonValueKind.False => "Non",
            JsonValueKind.Number when type == typeof(string) => e.GetDecimal().ToString("N2"),
            _ => e.ToString()
        };
    }

    [RelayCommand] private Task ExporterCsv() => Exporter("csv");
    [RelayCommand] private Task ExporterExcel() => Exporter("xlsx");
    [RelayCommand] private Task ExporterPdf() => Exporter("pdf");

    private Task Exporter(string format) => ExecuterAsync(async () =>
    {
        if (Rapport is null) return;
        var chemin = await api.TelechargerAsync(Url(format));
        OuvrirFichier(chemin);
        Information = $"Export enregistré : {chemin}";
    });

    [RelayCommand]
    private Task Controler() => ExecuterAsync(async () =>
    {
        Remplir(Controles, await api.GetAsync<List<ResultatControleDto>>("/api/pilotage/controles"));
        var ko = Controles.Count(c => !c.Conforme);
        if (ko == 0) Information = "Tous les contrôles de cohérence sont conformes.";
        else Erreur = $"{ko} contrôle(s) signalent des écarts à examiner (ce sont des anomalies de cohérence, pas des conclusions).";
    });
}
