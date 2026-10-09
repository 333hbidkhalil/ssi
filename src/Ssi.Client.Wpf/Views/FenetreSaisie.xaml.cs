using System.Windows;

namespace Ssi.Client.Wpf.Views;

/// <summary>Saisie d'un motif ou d'une valeur (motif obligatoire des corrections, annulations, dérogations).</summary>
public partial class FenetreSaisie : Window
{
    public string Valeur => Reponse.Text.Trim();

    public FenetreSaisie(string titre, string question)
    {
        InitializeComponent();
        Title = titre;
        Question.Text = question;
        Loaded += (_, _) => Reponse.Focus();
    }

    private void Valider_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Reponse.Text)) { MessageBox.Show("La saisie est obligatoire.", Title); return; }
        DialogResult = true;
    }
}
