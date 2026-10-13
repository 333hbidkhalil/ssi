using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

public sealed record EntreeMenu(string Libelle, string Icone, Type Page, string[] Permissions)
{
    /// <summary>Nom lu par les lecteurs d'écran et l'automatisation (sinon la description technique de l'enregistrement).</summary>
    public override string ToString() => Libelle;
}

/// <summary>Fenêtre principale : connexion, puis menu construit d'après les permissions de l'utilisateur.</summary>
public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly IServiceProvider services;
    private readonly ClientApi api;
    private readonly Session session;

    public static readonly IReadOnlyList<EntreeMenu> Menu =
    [
        new("Tableau de bord", "", typeof(TableauBordViewModel), [Droits.TableauBord]),
        new("Postes et relevés", "", typeof(PostesViewModel), [Droits.StationConsulter]),
        new("Caisse", "", typeof(CaisseViewModel), [Droits.CaisseConsulter]),
        new("Ventes d'articles", "", typeof(VentesViewModel), [Droits.VentesConsulter]),
        new("Stocks et jaugeages", "", typeof(StocksViewModel), [Droits.StockConsulter]),
        new("Achats et réceptions", "", typeof(AchatsViewModel), [Droits.AchatsConsulter]),
        new("Livraisons directes", "", typeof(VracViewModel), [Droits.VracConsulter]),
        new("Clients et fournisseurs", "", typeof(TiersViewModel), [Droits.ClientsConsulter, Droits.FournisseursConsulter]),
        new("Factures et règlements", "", typeof(FacturesViewModel), [Droits.FacturesConsulter]),
        new("Trésorerie", "", typeof(TresorerieViewModel), [Droits.BanqueConsulter]),
        new("Personnel", "", typeof(PersonnelViewModel), [Droits.PersonnelConsulter]),
        new("Rentabilité", "", typeof(RentabiliteViewModel), [Droits.RentabiliteConsulter]),
        new("Rapports et exports", "", typeof(RapportsViewModel), [Droits.RapportsConsulter]),
        new("Paramétrage station", "", typeof(ParametrageViewModel), [Droits.StationParametrer, Droits.CatalogueModifier, Droits.AdminParametres]),
        new("Administration", "", typeof(AdministrationViewModel), [Droits.AdminUtilisateurs, Droits.AdminParametres, Droits.AuditConsulter]),
    ];

    public ObservableCollection<EntreeMenu> EntreesVisibles { get; } = [];

    [ObservableProperty] private bool connecte;
    [ObservableProperty] private bool changementMotDePasseRequis;
    [ObservableProperty] private string login = "";
    [ObservableProperty] private string? utilisateur;
    [ObservableProperty] private EntreeMenu? entreeCourante;
    [ObservableProperty] private PageViewModel? pageCourante;
    [ObservableProperty] private string serveur = "";

    public ShellViewModel(IServiceProvider services, ClientApi api, Session session)
    {
        this.services = services; this.api = api; this.session = session;
        Serveur = api.Adresse.ToString();
        session.Expiree += () => System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Deconnecter();
            Erreur = "Votre session a expiré ou vos droits ont changé : reconnectez-vous.";
        });
    }

    /// <summary>Le mot de passe est transmis par la vue (PasswordBox n'est pas liable par sécurité).</summary>
    public async Task ConnecterAsync(string motDePasse)
    {
        if (await ExecuterAsync(() => api.ConnecterAsync(Login.Trim(), motDePasse)))
        {
            Utilisateur = session.Utilisateur!.NomComplet;
            if (session.Utilisateur.DoitChangerMotDePasse) { ChangementMotDePasseRequis = true; return; }
            OuvrirSession();
        }
    }

    public async Task ChangerMotDePasseAsync(string ancien, string nouveau, string confirmation)
    {
        if (nouveau != confirmation) { Erreur = "La confirmation ne correspond pas au nouveau mot de passe."; return; }
        if (await ExecuterAsync(async () => session.Ouvrir(await api.PostAsync<SessionDto>("/api/auth/mot-de-passe", new ChangementMotDePasseDto(ancien, nouveau))),
                "Mot de passe modifié."))
        {
            ChangementMotDePasseRequis = false;
            OuvrirSession();
        }
    }

    private void OuvrirSession()
    {
        Remplir(EntreesVisibles, Menu.Where(m => session.A(m.Permissions)));
        Connecte = true;
        EntreeCourante = EntreesVisibles.FirstOrDefault();
    }

    async partial void OnEntreeCouranteChanged(EntreeMenu? value)
    {
        if (value is null) { PageCourante = null; return; }
        var page = (PageViewModel)services.GetRequiredService(value.Page);
        PageCourante = page;
        await page.ChargerAsync();
    }

    [RelayCommand]
    private async Task Actualiser()
    {
        if (PageCourante is not null) await PageCourante.ChargerAsync();
    }

    [RelayCommand]
    private void Deconnecter()
    {
        session.Fermer();
        Connecte = false; ChangementMotDePasseRequis = false;
        EntreeCourante = null; EntreesVisibles.Clear();
        Utilisateur = null;
    }
}
