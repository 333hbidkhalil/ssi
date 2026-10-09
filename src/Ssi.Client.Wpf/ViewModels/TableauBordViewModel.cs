using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

public sealed partial class TableauBordViewModel(ClientApi api) : PageViewModel
{
    public override string Titre => "Tableau de bord";

    [ObservableProperty] private TableauBordDto? donnees;
    [ObservableProperty] private ISeries[] series = [];
    [ObservableProperty] private Axis[] axeX = [new Axis()];
    public ObservableCollection<AlerteDto> Alertes { get; } = [];
    public ObservableCollection<EtatCuveDto> Cuves { get; } = [];

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        var d = await api.GetAsync<TableauBordDto>("/api/pilotage/tableau-de-bord");
        Donnees = d;
        Remplir(Alertes, d.Alertes);
        Remplir(Cuves, d.Cuves);
        // Graphique : valeurs converties en double pour l'affichage uniquement (les calculs restent décimaux côté serveur).
        Series =
        [
            new ColumnSeries<double> { Name = "CA HT (DH)", Values = d.Evolution30Jours.Select(p => (double)p.ChiffreAffairesHt).ToArray() },
            new LineSeries<double> { Name = "Volume carburant (L)", Values = d.Evolution30Jours.Select(p => (double)p.VolumeCarburant).ToArray(), ScalesYAt = 1, GeometrySize = 4 }
        ];
        AxeX = [new Axis { Labels = d.Evolution30Jours.Select(p => p.Date.ToString("dd/MM")).ToArray(), LabelsRotation = 45 }];
    });

    public Axis[] AxesY { get; } = [new Axis { Name = "DH" }, new Axis { Name = "Litres", Position = LiveChartsCore.Measure.AxisPosition.End }];
}
