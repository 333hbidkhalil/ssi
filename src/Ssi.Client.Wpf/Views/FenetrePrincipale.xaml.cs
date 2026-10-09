using System.Windows;
using System.Windows.Input;
using Ssi.Client.Wpf.ViewModels;

namespace Ssi.Client.Wpf.Views;

public partial class FenetrePrincipale : Window
{
    public FenetrePrincipale()
    {
        InitializeComponent();
        Loaded += (_, _) => ChampLogin.Focus();
    }

    private ShellViewModel Vm => (ShellViewModel)DataContext;

    private async void Connecter_Click(object sender, RoutedEventArgs e)
    {
        await Vm.ConnecterAsync(MotDePasse.Password);
        MotDePasse.Clear();
    }

    private void MotDePasse_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Connecter_Click(sender, e);
    }

    private async void Changer_Click(object sender, RoutedEventArgs e)
    {
        await Vm.ChangerMotDePasseAsync(Ancien.Password, Nouveau.Password, Confirmation.Password);
        Ancien.Clear(); Nouveau.Clear(); Confirmation.Clear();
    }
}
