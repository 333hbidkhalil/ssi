using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssi.Client.Wpf.Services;
using Ssi.Contracts;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Rôle cochable dans la fiche utilisateur.</summary>
public sealed partial class RoleCoche : ObservableObject
{
    public required RoleDto Role { get; init; }
    [ObservableProperty] private bool coche;
}

/// <summary>Utilisateurs et rôles, paramètres, taux de taxe datés, journal d'audit et état des sauvegardes.</summary>
public sealed partial class AdministrationViewModel(ClientApi api, Session session) : PageViewModel
{
    public override string Titre => "Administration";

    public ObservableCollection<UtilisateurDto> Utilisateurs { get; } = [];
    public ObservableCollection<RoleCoche> Roles { get; } = [];
    public ObservableCollection<ParametreDto> Parametres { get; } = [];
    public ObservableCollection<TaxeDto> Taxes { get; } = [];
    public ObservableCollection<EntreeAuditDto> Audit { get; } = [];
    public ObservableCollection<SauvegardeDto> Sauvegardes { get; } = [];

    [ObservableProperty] private UtilisateurDto? utilisateur;
    [ObservableProperty] private string login = "";
    [ObservableProperty] private string nomComplet = "";
    [ObservableProperty] private bool actif = true;
    [ObservableProperty] private ParametreDto? parametre;
    [ObservableProperty] private string? valeurParametre;
    [ObservableProperty] private TaxeDto? taxe;
    [ObservableProperty] private string nouveauTaux = "";
    [ObservableProperty] private DateTime? debutTaux = DateTime.Today;
    [ObservableProperty] private string? filtreAction;
    [ObservableProperty] private string? filtreObjet;
    [ObservableProperty] private DateTime? auditDu = DateTime.Today.AddDays(-7);
    [ObservableProperty] private string? etatSauvegarde;
    [ObservableProperty] private bool sauvegardeEnAlerte;

    public bool GereUtilisateurs => session.A(Droits.AdminUtilisateurs);
    public bool GereParametres => session.A(Droits.AdminParametres);
    public bool VoitAudit => session.A(Droits.AuditConsulter);
    public bool VoitSauvegardes => session.A(Droits.AdminSauvegardes, Droits.AuditConsulter);

    public override Task ChargerAsync() => ExecuterAsync(async () =>
    {
        if (GereUtilisateurs)
        {
            Remplir(Utilisateurs, await api.GetAsync<List<UtilisateurDto>>("/api/admin/utilisateurs"));
            Remplir(Roles, (await api.GetAsync<List<RoleDto>>("/api/admin/roles")).Select(r => new RoleCoche { Role = r }));
        }
        if (GereParametres || VoitAudit) Remplir(Parametres, await api.GetAsync<List<ParametreDto>>("/api/admin/parametres"));
        Remplir(Taxes, await api.GetAsync<List<TaxeDto>>("/api/admin/taxes"));
        if (VoitAudit) await ChargerAuditAsync();
        if (VoitSauvegardes)
        {
            Remplir(Sauvegardes, await api.GetAsync<List<SauvegardeDto>>("/api/admin/sauvegardes"));
            var verifiee = Sauvegardes.Where(s => s.Statut == "VERIFIEE").MaxBy(s => s.Horodatage);
            SauvegardeEnAlerte = verifiee is null || verifiee.Horodatage < DateTimeOffset.Now.AddHours(-26);
            EtatSauvegarde = verifiee is null
                ? "Aucune sauvegarde vérifiée (restauration de contrôle réussie) n'est enregistrée : la protection des données n'est pas démontrée."
                : $"Dernière sauvegarde vérifiée : {verifiee.Horodatage.LocalDateTime:dd/MM/yyyy HH:mm} — {verifiee.Fichier}" + (SauvegardeEnAlerte ? " (plus de 26 h)" : "");
        }
    });

    private async Task ChargerAuditAsync()
    {
        var url = $"/api/admin/audit?du={Jour(AuditDu):yyyy-MM-dd}&taille=500";
        if (!string.IsNullOrWhiteSpace(FiltreAction)) url += $"&action={Uri.EscapeDataString(FiltreAction.Trim())}";
        if (!string.IsNullOrWhiteSpace(FiltreObjet)) url += $"&objetType={Uri.EscapeDataString(FiltreObjet.Trim())}";
        Remplir(Audit, (await api.GetAsync<Page<EntreeAuditDto>>(url)).Elements);
    }

    [RelayCommand] private Task RechercherAudit() => ExecuterAsync(ChargerAuditAsync);

    partial void OnUtilisateurChanged(UtilisateurDto? value)
    {
        Login = value?.Login ?? ""; NomComplet = value?.NomComplet ?? ""; Actif = value?.Actif ?? true;
        foreach (var r in Roles) r.Coche = value?.Roles.Contains(r.Role.Code) == true;
    }

    partial void OnParametreChanged(ParametreDto? value) => ValeurParametre = value?.Valeur;

    [RelayCommand]
    private void NouvelUtilisateur() { Utilisateur = null; OnUtilisateurChanged(null); }

    [RelayCommand]
    private Task EnregistrerUtilisateur() => ExecuterAsync(async () =>
    {
        var roles = Roles.Where(r => r.Coche).Select(r => r.Role.Code).ToList();
        if (Utilisateur is null)
        {
            if (Saisir($"Mot de passe provisoire de {Login} (l'utilisateur devra le changer à la première connexion) :", "Mot de passe provisoire") is not { } mdp) return;
            var u = await api.PostAsync<UtilisateurDto>("/api/admin/utilisateurs", new CreationUtilisateurDto(Login.Trim(), NomComplet.Trim(), mdp, roles, null));
            await ChargerAsync();
            Utilisateur = Utilisateurs.FirstOrDefault(x => x.Id == u.Id);
            Information = $"Utilisateur {u.Login} créé.";
        }
        else
        {
            if (Saisir($"Motif de la modification de {Utilisateur.Login} (rôles : {string.Join(", ", roles)}) :") is not { } motif) return;
            var id = Utilisateur.Id;
            await api.PutAsync<UtilisateurDto>($"/api/admin/utilisateurs/{id}", new ModificationUtilisateurDto(NomComplet.Trim(), Actif, Utilisateur.EmployeId, roles,
                Utilisateur.PermissionsIndividuelles, motif));
            await ChargerAsync();
            Utilisateur = Utilisateurs.FirstOrDefault(x => x.Id == id);
            Information = "Utilisateur modifié ; ses droits s'appliquent dès sa prochaine requête.";
        }
    });

    [RelayCommand]
    private Task ReinitialiserMotDePasse() => ExecuterAsync(async () =>
    {
        if (Utilisateur is null || Saisir($"Nouveau mot de passe provisoire pour {Utilisateur.Login} :", "Réinitialisation") is not { } mdp
            || Saisir("Motif de la réinitialisation :") is not { } motif) return;
        await api.PostAsync($"/api/admin/utilisateurs/{Utilisateur.Id}/mot-de-passe", new ReinitialisationMotDePasseDto(mdp, motif));
    }, "Mot de passe réinitialisé : changement obligatoire à la prochaine connexion.");

    [RelayCommand]
    private Task ModifierParametre() => ExecuterAsync(async () =>
    {
        if (Parametre is null || ValeurParametre is null) return;
        if (Saisir($"Motif du changement de « {Parametre.Description} » : {Parametre.Valeur} → {ValeurParametre}") is not { } motif) return;
        await api.PutAsync<ParametreDto>($"/api/admin/parametres/{Uri.EscapeDataString(Parametre.Cle)}", new ModificationParametreDto(ValeurParametre, motif));
        await ChargerAsync();
    }, "Paramètre modifié (tracé dans le journal d'audit).");

    [RelayCommand]
    private Task AjouterTaux() => ExecuterAsync(async () =>
    {
        if (Taxe is null) return;
        var taux = Nombre(NouveauTaux, "Taux (%)");   // saisi en pourcentage, stocké en fraction (20 % → 0,20)
        if (Saisir($"Nouveau taux {Taxe.Libelle} : {taux:N2} % à partir du {Jour(DebutTaux):dd/MM/yyyy}.\nRéférence légale ou motif :") is not { } motif) return;
        await api.PostAsync<TaxeDto>($"/api/admin/taxes/{Taxe.Id}/taux", new NouveauTauxDto(taux / 100m, Jour(DebutTaux), motif));
        NouveauTaux = "";
        await ChargerAsync();
    }, "Taux enregistré. Les documents antérieurs conservent leur taux d'origine.");
}
