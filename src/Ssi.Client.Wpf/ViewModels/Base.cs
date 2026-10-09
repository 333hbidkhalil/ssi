using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Ssi.Client.Wpf.Services;

namespace Ssi.Client.Wpf.ViewModels;

/// <summary>Codes de permission utilisés pour adapter l'affichage (le serveur reste seul juge).</summary>
public static class Droits
{
    public const string TableauBord = "tableau_bord.consulter", StationConsulter = "station.consulter", StationCreer = "station.creer",
        StationValider = "station.valider", StationRouvrir = "station.rouvrir", CaisseConsulter = "caisse.consulter", CaisseCreer = "caisse.creer",
        CaisseValider = "caisse.valider", StockConsulter = "stock.consulter", StockCreer = "stock.creer", StockAjuster = "stock.ajuster",
        StockApprouver = "stock.approuver_ajustement", VentesConsulter = "ventes.consulter", VentesCreer = "ventes.creer", VentesAnnuler = "ventes.annuler",
        ClientsConsulter = "clients.consulter", FournisseursConsulter = "fournisseurs.consulter", FacturesConsulter = "factures.consulter",
        FacturesValider = "factures.valider", ReglementsCreer = "reglements.creer", BanqueConsulter = "banque.consulter", VracConsulter = "vrac.consulter",
        VracCreer = "vrac.creer", VracValider = "vrac.valider", RentabiliteConsulter = "rentabilite.consulter", RapportsConsulter = "rapports.consulter",
        RapportsExporter = "rapports.exporter", AdminUtilisateurs = "admin.utilisateurs", AdminParametres = "admin.parametres", AuditConsulter = "audit.consulter",
        AdminSauvegardes = "admin.sauvegardes", ChargesConsulter = "charges.consulter", ChargesCreer = "charges.creer",
        ChargesAnnuler = "charges.annuler", CaisseRouvrir = "caisse.rouvrir", StationAnnuler = "station.annuler", VracAnnuler = "vrac.annuler",
        VracDerogation = "vrac.derogation", FacturesCreer = "factures.creer", FacturesAnnuler = "factures.annuler", ReglementsConsulter = "reglements.consulter",
        ReglementsAnnuler = "reglements.annuler", BanqueCreer = "banque.creer", BanqueRapprocher = "banque.rapprocher", ClientsCreer = "clients.creer",
        ClientsModifier = "clients.modifier", FournisseursCreer = "fournisseurs.creer", FournisseursModifier = "fournisseurs.modifier",
        RentabiliteSimuler = "rentabilite.simuler", AdminRoles = "admin.roles", CatalogueConsulter = "catalogue.consulter", PrixValider = "prix.valider",
        PersonnelConsulter = "personnel.consulter", PersonnelCreer = "personnel.creer", PersonnelModifier = "personnel.modifier",
        PersonnelSensible = "personnel.sensible", AchatsConsulter = "achats.consulter", AchatsCreer = "achats.creer", AchatsValider = "achats.valider",
        AchatsAnnuler = "achats.annuler", CatalogueCreer = "catalogue.creer", CatalogueModifier = "catalogue.modifier", PrixConsulter = "prix.consulter",
        StationParametrer = "station.parametrer";
}

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool occupe;
    [ObservableProperty] private string? erreur;
    [ObservableProperty] private string? information;

    /// <summary>Exécute une action serveur : indicateur d'activité, message d'erreur métier en clair, aucune exception non gérée.</summary>
    protected async Task<bool> ExecuterAsync(Func<Task> action, string? succes = null)
    {
        if (Occupe) return false;   // évite les doubles clics
        Occupe = true; Erreur = null; Information = null;
        try
        {
            await action();
            if (succes is not null) Information = succes;
            return true;
        }
        catch (ErreurApi ex)
        {
            Erreur = ex.Message;
            return false;
        }
        finally { Occupe = false; }
    }

    protected static bool Confirmer(string question, string titre = "Confirmation")
        => MessageBox.Show(question, titre, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    protected static string? Saisir(string question, string titre = "Motif")
    {
        var f = new Views.FenetreSaisie(titre, question) { Owner = Application.Current.MainWindow };
        return f.ShowDialog() == true ? f.Valeur : null;
    }

    /// <summary>Lecture d'un nombre saisi au format local (virgule décimale) ; erreur de saisie explicite sinon.</summary>
    protected static decimal Nombre(string? texte, string champ)
    {
        var c = System.Globalization.CultureInfo.CurrentCulture;
        var t = (texte ?? "").Replace(" ", "").Replace("\u00A0", "").Replace("\u202F", "");
        if (t.Contains('.') && !t.Contains(',')) t = t.Replace(".", c.NumberFormat.NumberDecimalSeparator);   // point accepté comme séparateur décimal
        return decimal.TryParse(t, System.Globalization.NumberStyles.Number, c, out var v)
            ? v : throw new ErreurApi(System.Net.HttpStatusCode.BadRequest, "Saisie", $"{champ} : nombre invalide.", null);
    }

    protected static decimal? NombreOptionnel(string? texte, string champ) => string.IsNullOrWhiteSpace(texte) ? null : Nombre(texte, champ);

    /// <summary>Ouvre un fichier téléchargé avec l'application associée (lecteur PDF, tableur).</summary>
    protected static void OuvrirFichier(string chemin)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(chemin) { UseShellExecute = true }); }
        catch (Exception) { /* pas d'application associée : le chemin est affiché à l'utilisateur */ }
    }

    protected static DateOnly Jour(DateTime? d) => DateOnly.FromDateTime(d ?? DateTime.Today);

    protected static void Remplir<T>(ObservableCollection<T> cible, IEnumerable<T> source)
    {
        cible.Clear();
        foreach (var x in source) cible.Add(x);
    }
}

/// <summary>Page de l'application : chargée à l'affichage.</summary>
public abstract class PageViewModel : ViewModelBase
{
    public abstract string Titre { get; }
    public virtual Task ChargerAsync() => Task.CompletedTask;
}
