using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>
/// Personnel : fiches, absences, éléments de rémunération et masse salariale. Suivi de gestion, sans calcul de paie légale (H-16) ;
/// les salaires ne sont visibles et modifiables qu'avec la permission personnel.sensible.
/// </summary>
public sealed partial class PersonnelViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Personnel";

    public ObservableCollection<EmployeDto> Employes { get; } = [];
    public ObservableCollection<AbsenceDto> Absences { get; } = [];
    public ObservableCollection<ElementRemunerationDto> Elements { get; } = [];
    public IReadOnlyList<TypeElementRemuneration> TypesElement { get; } = Enum.GetValues<TypeElementRemuneration>();

    [ObservableProperty] private EmployeDto? employe;
    [ObservableProperty] private Guid? employeId;
    [ObservableProperty] private string matricule = "";
    [ObservableProperty] private string nom = "";
    [ObservableProperty] private string prenom = "";
    [ObservableProperty] private string? cin;
    [ObservableProperty] private string fonction = "";
    [ObservableProperty] private DateTime? dateEmbauche = DateTime.Today;
    [ObservableProperty] private string? affectation;
    [ObservableProperty] private string salaire = "";
    [ObservableProperty] private string? telephone;
    [ObservableProperty] private bool actif = true;
    [ObservableProperty] private DateTime? debutAbsence = DateTime.Today;
    [ObservableProperty] private DateTime? finAbsence = DateTime.Today;
    [ObservableProperty] private string typeAbsence = "Congé";
    [ObservableProperty] private bool justifiee = true;
    [ObservableProperty] private TypeElementRemuneration typeElement = TypeElementRemuneration.Prime;
    [ObservableProperty] private string montantElement = "";
    [ObservableProperty] private string? commentaireElement;
    [ObservableProperty] private DateTime? mois = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private MasseSalarialeDto? masse;

    public bool PeutCreer => session.A(Droits.PersonnelCreer);
    public bool PeutModifier => session.A(Droits.PersonnelModifier, Droits.PersonnelCreer);
    public bool VoitSalaires => session.A(Droits.PersonnelSensible);
    private DateOnly Periode => new(Mois?.Year ?? DateTime.Today.Year, Mois?.Month ?? DateTime.Today.Month, 1);

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Remplir(Employes, await api.GetAsync<List<EmployeDto>>("/api/personnel/employes"));
        Employe = Employes.FirstOrDefault(e => e.Id == EmployeId);
        await ChargerPeriodeAsync();
    });

    private async Task ChargerPeriodeAsync()
    {
        Masse = await api.GetAsync<MasseSalarialeDto>($"/api/personnel/masse-salariale?mois={Periode:yyyy-MM-dd}");
        Remplir(Elements, await api.GetAsync<List<ElementRemunerationDto>>($"/api/personnel/remunerations?periode={Periode:yyyy-MM-dd}"
            + (EmployeId is { } id ? $"&employeId={id}" : "")));
        Remplir(Absences, EmployeId is { } e ? await api.GetAsync<List<AbsenceDto>>($"/api/personnel/absences?employeId={e}") : []);
    }

    [RelayCommand] private Task ChangerPeriode() => ExecuterAsync(ChargerPeriodeAsync);

    async partial void OnEmployeChanged(EmployeDto? value)
    {
        if (value is null) return;
        EmployeId = value.Id; Matricule = value.Matricule; Nom = value.Nom; Prenom = value.Prenom; Cin = value.Cin; Fonction = value.Fonction;
        DateEmbauche = value.DateEmbauche.ToDateTime(TimeOnly.MinValue); Affectation = value.Affectation; Telephone = value.Telephone; Actif = value.Actif;
        Salaire = value.SalaireBase?.ToString() ?? "";
        await ExecuterAsync(ChargerPeriodeAsync);
    }

    [RelayCommand]
    private void Nouveau()
    {
        Employe = null; EmployeId = null; Matricule = ""; Nom = ""; Prenom = ""; Cin = null; Fonction = ""; Affectation = null; Telephone = null;
        Salaire = ""; Actif = true; DateEmbauche = DateTime.Today; Absences.Clear();
    }

    [RelayCommand]
    private Task Enregistrer() => ExecuterAsync(async () =>
    {
        // Sans accès aux salaires, le salaire n'est pas transmis : le serveur le laisse inchangé.
        var salaire = VoitSalaires ? NombreOptionnel(Salaire, "Salaire de base") ?? 0m : (decimal?)null;
        var dto = new EnregistrementEmployeDto(Matricule.Trim(), Nom.Trim(), Prenom.Trim(), Cin, Fonction.Trim(), Jour(DateEmbauche), Affectation, salaire, Telephone, Actif);
        var e = EmployeId is { } id ? await api.PutAsync<EmployeDto>($"/api/personnel/employes/{id}", dto) : await api.PostAsync<EmployeDto>("/api/personnel/employes", dto);
        EmployeId = e.Id;
        await ChargerAsync();
    }, "Fiche enregistrée.");

    [RelayCommand]
    private Task AjouterAbsence() => ExecuterAsync(async () =>
    {
        if (EmployeId is not { } id) return;
        await api.PostAsync<AbsenceDto>("/api/personnel/absences", new SaisieAbsenceDto(id, Jour(DebutAbsence), Jour(FinAbsence), TypeAbsence, Justifiee));
        await ChargerPeriodeAsync();
    }, "Absence enregistrée.");

    [RelayCommand]
    private Task AjouterElement() => ExecuterAsync(async () =>
    {
        if (EmployeId is not { } id) return;
        await api.PostAsync<ElementRemunerationDto>("/api/personnel/remunerations", new SaisieElementRemunerationDto(id, Periode, TypeElement,
            Nombre(MontantElement, "Montant"), CommentaireElement));
        MontantElement = ""; CommentaireElement = null;
        await ChargerPeriodeAsync();
    }, "Élément enregistré. Pour qu'il compte dans la rentabilité, saisissez le coût en charge de personnel (H-21).");
}
