using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssi.Client.Wpf.Services;
using Ssi.Client.Wpf.ViewModels;
using Ssi.Client.Wpf.Views;

namespace Ssi.Client.Wpf;

public partial class App : Application
{
    private ServiceProvider? services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Français (Maroc) avec l'espace comme séparateur des milliers : « 14 932,00 » plutôt que « 14.932,00 »,
        // pour qu'un point affiché ne soit jamais confondu avec la virgule décimale (le point est accepté en saisie comme décimale).
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-MA").Clone();
        culture.NumberFormat.NumberGroupSeparator = culture.NumberFormat.CurrencyGroupSeparator = culture.NumberFormat.PercentGroupSeparator = "\u202F";
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = culture;
        // Les liaisons WPF formatent avec la culture standard de la langue de l'élément (pas avec un clone modifié) :
        // fr-FR donne les mêmes formats de date et de nombre que fr-MA, avec l'espace fine comme séparateur des milliers.
        // Aucun format monétaire (C) n'est utilisé : les montants sont suffixés « DH » explicitement.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("fr-FR")));
        DispatcherUnhandledException += Inattendue;

        var config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: true).Build();
        var adresse = config["Serveur:Adresse"] ?? "https://localhost:5443";

        var s = new ServiceCollection();
        s.AddSingleton<Session>();
        s.AddHttpClient<ClientApi>(c => { c.BaseAddress = new Uri(adresse); c.Timeout = TimeSpan.FromSeconds(60); });
        s.AddSingleton<ShellViewModel>();
        foreach (var page in ShellViewModel.Menu) s.AddTransient(page.Page);
        services = s.BuildServiceProvider();

        MainWindow = new FenetrePrincipale { DataContext = services.GetRequiredService<ShellViewModel>() };
        MainWindow.Show();
    }

    private static void Inattendue(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Erreur inattendue : {e.Exception.Message}\n\nL'opération en cours n'a peut-être pas abouti : vérifiez son état avant de la relancer.",
            "SSI", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        services?.Dispose();
        base.OnExit(e);
    }
}
