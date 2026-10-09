using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Fiche éditable d'un client ou d'un fournisseur (champs communs).</summary>
public sealed partial class FicheTiers : ObservableObject
{
    public Guid? Id { get; set; }
    [ObservableProperty] private string code = "";
    [ObservableProperty] private string nom = "";
    [ObservableProperty] private TypeClient type = TypeClient.Comptant;
    [ObservableProperty] private string? ice;
    [ObservableProperty] private string? identifiantFiscal;
    [ObservableProperty] private string? registreCommerce;
    [ObservableProperty] private string? adresse;
    [ObservableProperty] private string? ville;
    [ObservableProperty] private string? telephone;
    [ObservableProperty] private string? email;
    [ObservableProperty] private string? contact;
    [ObservableProperty] private string delai = "30";
    [ObservableProperty] private string plafond = "0";
    [ObservableProperty] private bool actif = true;
    [ObservableProperty] private string? notes;
}

/// <summary>Clients et fournisseurs : fiches, relevés de compte, relances, évaluation et incidents fournisseurs.</summary>
public sealed partial class TiersViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Clients et fournisseurs";

    public ObservableCollection<ClientDto> Clients { get; } = [];
    public ObservableCollection<FournisseurDto> Fournisseurs { get; } = [];
    public ObservableCollection<RelanceDto> Relances { get; } = [];
    public IReadOnlyList<TypeClient> Types { get; } = Enum.GetValues<TypeClient>();

    [ObservableProperty] private string? recherche;
    [ObservableProperty] private bool ongletFournisseurs;
    [ObservableProperty] private ClientDto? clientSelectionne;
    [ObservableProperty] private FournisseurDto? fournisseurSelectionne;
    [ObservableProperty] private ReleveCompteDto? releve;
    [ObservableProperty] private EvaluationFournisseurDto? evaluation;
    [ObservableProperty] private FicheTiers? fiche;
    [ObservableProperty] private DateTime? du = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime? au = DateTime.Today;

    public bool VoitClients => session.A(Droits.ClientsConsulter);
    public bool VoitFournisseurs => session.A(Droits.FournisseursConsulter);
    public bool PeutModifier => OngletFournisseurs ? session.A(Droits.FournisseursCreer, Droits.FournisseursModifier) : session.A(Droits.ClientsCreer, Droits.ClientsModifier);
    partial void OnOngletFournisseursChanged(bool value) { OnPropertyChanged(nameof(PeutModifier)); Fiche = null; Releve = null; }

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        OngletFournisseurs = !VoitClients;
        await ChargerListesAsync();
    });

    private async Task ChargerListesAsync()
    {
        var q = string.IsNullOrWhiteSpace(Recherche) ? "" : $"?q={Uri.EscapeDataString(Recherche.Trim())}";
        if (VoitClients) Remplir(Clients, await api.GetAsync<List<ClientDto>>("/api/clients" + q));
        if (VoitFournisseurs) Remplir(Fournisseurs, await api.GetAsync<List<FournisseurDto>>("/api/fournisseurs" + q));
    }

    [RelayCommand] private Task Rechercher() => ExecuterAsync(ChargerListesAsync);

    async partial void OnClientSelectionneChanged(ClientDto? value)
    {
        if (value is null) return;
        Fiche = new FicheTiers
        {
            Id = value.Id, Code = value.Code, Nom = value.Nom, Type = value.Type, Ice = value.Ice, IdentifiantFiscal = value.IdentifiantFiscal,
            RegistreCommerce = value.RegistreCommerce, Adresse = value.Adresse, Ville = value.Ville, Telephone = value.Telephone, Email = value.Email,
            Contact = value.Contact, Delai = value.DelaiPaiementJours.ToString(), Plafond = value.PlafondCredit.ToString("N2"), Actif = value.Actif, Notes = value.Notes
        };
        await ExecuterAsync(async () =>
        {
            await ChargerReleveAsync();
            Remplir(Relances, await api.GetAsync<List<RelanceDto>>($"/api/clients/{value.Id}/relances"));
        });
    }

    async partial void OnFournisseurSelectionneChanged(FournisseurDto? value)
    {
        if (value is null) return;
        Fiche = new FicheTiers
        {
            Id = value.Id, Code = value.Code, Nom = value.Nom, Ice = value.Ice, IdentifiantFiscal = value.IdentifiantFiscal, RegistreCommerce = value.RegistreCommerce,
            Adresse = value.Adresse, Ville = value.Ville, Telephone = value.Telephone, Email = value.Email, Contact = value.Contact,
            Delai = value.DelaiPaiementJours.ToString(), Actif = value.Actif, Notes = value.Notes
        };
        await ExecuterAsync(async () =>
        {
            await ChargerReleveAsync();
            Evaluation = await api.GetAsync<EvaluationFournisseurDto>($"/api/fournisseurs/{value.Id}/evaluation");
        });
    }

    private async Task ChargerReleveAsync()
    {
        var periode = $"?du={Jour(Du):yyyy-MM-dd}&au={Jour(Au):yyyy-MM-dd}";
        Releve = OngletFournisseurs
            ? FournisseurSelectionne is { } f ? await api.GetAsync<ReleveCompteDto>($"/api/fournisseurs/{f.Id}/releve{periode}") : null
            : ClientSelectionne is { } c ? await api.GetAsync<ReleveCompteDto>($"/api/clients/{c.Id}/releve{periode}") : null;
    }

    [RelayCommand] private Task ActualiserReleve() => ExecuterAsync(ChargerReleveAsync);

    [RelayCommand]
    private void Nouveau()
    {
        ClientSelectionne = null; FournisseurSelectionne = null; Releve = null; Evaluation = null; Relances.Clear();
        Fiche = new FicheTiers();
    }

    [RelayCommand]
    private Task Enregistrer() => ExecuterAsync(async () =>
    {
        var f = Fiche!;
        var delai = (int)Nombre(f.Delai, "Délai de paiement");
        if (OngletFournisseurs)
        {
            var dto = new EnregistrementFournisseurDto(f.Code, f.Nom, f.Ice, f.IdentifiantFiscal, f.RegistreCommerce, f.Adresse, f.Ville, f.Telephone, f.Email,
                f.Contact, delai, null, null, f.Actif, f.Notes);
            var r = f.Id is { } id ? await api.PutAsync<FournisseurDto>($"/api/fournisseurs/{id}", dto) : await api.PostAsync<FournisseurDto>("/api/fournisseurs", dto);
            await ChargerListesAsync();
            FournisseurSelectionne = Fournisseurs.FirstOrDefault(x => x.Id == r.Id);
        }
        else
        {
            var dto = new EnregistrementClientDto(f.Code, f.Nom, f.Type, f.Ice, f.IdentifiantFiscal, f.RegistreCommerce, f.Adresse, f.Ville, f.Telephone, f.Email,
                f.Contact, delai, Nombre(f.Plafond, "Plafond de crédit"), f.Actif, f.Notes);
            var r = f.Id is { } id ? await api.PutAsync<ClientDto>($"/api/clients/{id}", dto) : await api.PostAsync<ClientDto>("/api/clients", dto);
            await ChargerListesAsync();
            ClientSelectionne = Clients.FirstOrDefault(x => x.Id == r.Id);
        }
    }, "Fiche enregistrée (modification tracée dans le journal d'audit).");

    [RelayCommand]
    private Task Relancer() => ExecuterAsync(async () =>
    {
        if (ClientSelectionne is null || Saisir($"Relance de {ClientSelectionne.Nom} (créance {ClientSelectionne.Creance:N2} DH).\nMoyen et commentaire (ex. « Téléphone : promesse de règlement vendredi ») :", "Relance") is not { } texte) return;
        var moyen = texte.Split(':', 2)[0].Trim();
        var commentaire = texte.Contains(':') ? texte.Split(':', 2)[1].Trim() : null;
        Remplir(Relances, await api.PostAsync<List<RelanceDto>>($"/api/clients/{ClientSelectionne.Id}/relances", new SaisieRelanceDto(null, moyen, commentaire)));
    }, "Relance enregistrée.");

    [RelayCommand]
    private Task SignalerIncident() => ExecuterAsync(async () =>
    {
        if (FournisseurSelectionne is null || Saisir($"Incident fournisseur {FournisseurSelectionne.Nom} — type : description (ex. « Retard : livraison reçue à J+2 ») :", "Incident") is not { } texte) return;
        var type = texte.Split(':', 2)[0].Trim();
        var description = texte.Contains(':') ? texte.Split(':', 2)[1].Trim() : texte;
        await api.PostAsync<IncidentFournisseurDto>($"/api/fournisseurs/{FournisseurSelectionne.Id}/incidents",
            new IncidentFournisseurDto(Guid.Empty, DateOnly.FromDateTime(DateTime.Today), type, description, null));
        Evaluation = await api.GetAsync<EvaluationFournisseurDto>($"/api/fournisseurs/{FournisseurSelectionne.Id}/evaluation");
    }, "Incident enregistré.");
}
