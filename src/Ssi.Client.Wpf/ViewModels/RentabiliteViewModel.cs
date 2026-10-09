using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Hypothèse de simulation éditable pour une activité.</summary>
public sealed partial class LigneHypothese : ObservableObject
{
    public required Activite Activite { get; init; }
    public required string Libelle { get; init; }
    [ObservableProperty] private string prixVente = "0";
    [ObservableProperty] private string prixAchat = "0";
    [ObservableProperty] private string volumePct = "0";
}

/// <summary>Rentabilité par activité, seuil de rentabilité et simulations (sans aucune écriture).</summary>
public sealed partial class RentabiliteViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Rentabilité";

    public ObservableCollection<LigneRentabiliteDto> Lignes { get; } = [];
    public ObservableCollection<RepartitionChargeDto> Repartitions { get; } = [];
    public ObservableCollection<string> Avertissements { get; } = [];
    public ObservableCollection<LigneHypothese> Hypotheses { get; } = [];
    public ObservableCollection<LigneSimulationDto> LignesSimulation { get; } = [];

    [ObservableProperty] private DateTime? du = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? au = DateTime.Today;
    [ObservableProperty] private RentabiliteDto? resultat;
    [ObservableProperty] private SeuilRentabiliteDto? seuil;
    [ObservableProperty] private SimulationDto? simulation;
    [ObservableProperty] private string variationChargesFixes = "0";
    [ObservableProperty] private string variationChargesVariablesPct = "0";

    public bool PeutSimuler => session.A(Droits.RentabiliteSimuler);

    private static readonly Dictionary<Activite, string> Libelles = new()
    {
        [Activite.GasoilStation] = "Gasoil station", [Activite.SansPlombStation] = "Sans plomb station", [Activite.GasoilVrac] = "Gasoil livraison directe",
        [Activite.SansPlombVrac] = "Sans plomb livraison directe", [Activite.Lubrifiants] = "Lubrifiants", [Activite.Autres] = "Autres articles"
    };

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        var periode = $"du={Jour(Du):yyyy-MM-dd}&au={Jour(Au):yyyy-MM-dd}";
        Resultat = await api.GetAsync<RentabiliteDto>($"/api/pilotage/rentabilite?{periode}");
        Remplir(Lignes, Resultat.Activites.Append(Resultat.Total));
        Remplir(Repartitions, Resultat.Charges);
        Remplir(Avertissements, Resultat.Avertissements);
        Seuil = await api.GetAsync<SeuilRentabiliteDto>($"/api/pilotage/seuil-rentabilite?{periode}");
        if (Hypotheses.Count == 0)   // toutes les activités, y compris celles sans opération sur la période
            foreach (var a in Enum.GetValues<Activite>())
                Hypotheses.Add(new LigneHypothese { Activite = a, Libelle = Resultat.Activites.FirstOrDefault(l => l.Activite == a)?.Libelle ?? Libelles[a] });
    });

    [RelayCommand] private Task Calculer() => ChargerAsync();

    [RelayCommand]
    private Task Simuler() => ExecuterAsync(async () =>
    {
        var h = Hypotheses.Select(x => new HypotheseActiviteDto(x.Activite, Nombre(x.PrixVente, $"{x.Libelle} : variation prix de vente"),
            Nombre(x.PrixAchat, $"{x.Libelle} : variation prix d'achat"), Nombre(x.VolumePct, $"{x.Libelle} : variation volume"))).ToList();
        Simulation = await api.PostAsync<SimulationDto>("/api/pilotage/simulations", new SimulationDemandeDto(Jour(Du), Jour(Au), h,
            Nombre(VariationChargesFixes, "Variation des charges fixes"), Nombre(VariationChargesVariablesPct, "Variation des charges variables")));
        Remplir(LignesSimulation, Simulation.Activites);
    });
}
