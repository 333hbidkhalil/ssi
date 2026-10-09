using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Sessions de caisse : ouverture, entrées et sorties, comptage, validation de l'écart, réouverture motivée.</summary>
public sealed partial class CaisseViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Caisse";

    public ObservableCollection<CompteFinancierDto> Caisses { get; } = [];
    public ObservableCollection<SessionCaisseDto> Sessions { get; } = [];

    [ObservableProperty] private CompteFinancierDto? caisse;
    [ObservableProperty] private SessionCaisseDto? sessionSelectionnee;
    [ObservableProperty] private SessionCaisseDto? detail;
    [ObservableProperty] private string montantOperation = "";
    [ObservableProperty] private bool entree = true;
    [ObservableProperty] private string libelleOperation = "";
    [ObservableProperty] private string? referenceOperation;
    [ObservableProperty] private string montantCompte = "";
    [ObservableProperty] private string? commentaireCloture;

    private string? cleOperation, cleValidation;

    public bool PeutCreer => session.A(Droits.CaisseCreer);
    public bool PeutValider => session.A(Droits.CaisseValider);
    public bool PeutRouvrir => session.A(Droits.CaisseRouvrir);
    public bool EstOuverte => Detail?.Statut == StatutSessionCaisse.Ouverte;
    public bool EstCloturee => Detail?.Statut == StatutSessionCaisse.Cloturee;
    partial void OnDetailChanged(SessionCaisseDto? value) { OnPropertyChanged(nameof(EstOuverte)); OnPropertyChanged(nameof(EstCloturee)); }

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        Remplir(Caisses, (await api.GetAsync<List<CompteFinancierDto>>("/api/tresorerie/comptes")).Where(c => c.Type == TypeCompteFinancier.Caisse && c.Actif));
        Caisse ??= Caisses.FirstOrDefault();
        await ChargerSessionsAsync(SessionSelectionnee?.Id);
    });

    private async Task ChargerSessionsAsync(Guid? garder)
    {
        Remplir(Sessions, (await api.GetAsync<List<SessionCaisseDto>>("/api/tresorerie/caisse/sessions")).OrderByDescending(s => s.Ouverture).Take(60));
        var id = garder ?? Sessions.FirstOrDefault(s => s.Statut != StatutSessionCaisse.Validee)?.Id ?? Sessions.FirstOrDefault()?.Id;
        SessionSelectionnee = Sessions.FirstOrDefault(s => s.Id == id);
        Detail = SessionSelectionnee is null ? null : await api.GetAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{SessionSelectionnee.Id}");
    }

    async partial void OnSessionSelectionneeChanged(SessionCaisseDto? value)
    {
        if (value is not null && Detail?.Id != value.Id)
            await ExecuterAsync(async () => Detail = await api.GetAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{value.Id}"));
    }

    [RelayCommand]
    private Task Ouvrir() => ExecuterAsync(async () =>
    {
        var s = await api.PostAsync<SessionCaisseDto>("/api/tresorerie/caisse/sessions", new OuvertureCaisseDto(Caisse?.Id, null, null));
        await ChargerSessionsAsync(s.Id);
    }, "Caisse ouverte. Le solde d'ouverture est le solde comptable du compte.");

    [RelayCommand]
    private Task AjouterOperation() => ExecuterAsync(async () =>
    {
        var montant = Nombre(MontantOperation, "Montant");
        cleOperation ??= Guid.NewGuid().ToString();
        Detail = await api.PostAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{Detail!.Id}/operations",
            new OperationCaisseDto(montant, Entree, LibelleOperation, ReferenceOperation), cleOperation);
        cleOperation = null;
        MontantOperation = ""; LibelleOperation = ""; ReferenceOperation = null;
    }, "Opération enregistrée.");

    [RelayCommand]
    private Task Cloturer() => ExecuterAsync(async () =>
    {
        var compte = Nombre(MontantCompte, "Montant compté");
        if (!Confirmer($"Clôturer la caisse avec {compte:N2} DH comptés ?\nSolde théorique : {Detail!.SoldeTheorique:N2} DH — écart : {compte - Detail.SoldeTheorique:N2} DH")) return;
        Detail = await api.PostAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{Detail.Id}/cloture", new ClotureCaisseDto(compte, CommentaireCloture));
        MontantCompte = ""; CommentaireCloture = null;
        await ChargerSessionsAsync(Detail.Id);
    }, "Caisse clôturée : vérifiez l'écart puis faites valider.");

    [RelayCommand]
    private Task Valider() => ExecuterAsync(async () =>
    {
        var id = Detail!.Id;
        if (!Confirmer($"Valider la session {Detail.Numero} ? L'écart de {Detail.Ecart:N2} DH sera enregistré en trésorerie.")) return;
        cleValidation ??= Guid.NewGuid().ToString();
        try
        {
            Detail = await api.PostAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{id}/validation", null, cleValidation);
        }
        catch (ErreurApi e) when (e.Code == "EXPLICATION_ECART")
        {
            if (Saisir($"{e.Message}\n\nExplication de l'écart :", "Explication obligatoire") is not { } motif) throw;
            cleValidation = Guid.NewGuid().ToString();
            Detail = await api.PostAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{id}/validation", new MotifDto(motif), cleValidation);
        }
        cleValidation = null;
        await ChargerSessionsAsync(id);
    }, "Session validée.");

    [RelayCommand]
    private Task Rouvrir() => ExecuterAsync(async () =>
    {
        if (Saisir($"Motif de la réouverture de {Detail!.Numero} (l'écart enregistré sera contre-passé) :") is not { } motif) return;
        Detail = await api.PostAsync<SessionCaisseDto>($"/api/tresorerie/caisse/sessions/{Detail.Id}/reouverture", new MotifDto(motif));
        await ChargerSessionsAsync(Detail.Id);
    }, "Session rouverte.");
}
