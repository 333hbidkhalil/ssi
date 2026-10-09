using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ssi.Client.Wpf.Views;

public sealed class InverseVisibilite : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Visible si la valeur est non nulle (non vide pour une chaîne, non nulle pour un compteur).</summary>
public sealed class NonVideVisibilite : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null || value is string s && string.IsNullOrWhiteSpace(s) || value is 0 ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Négation d'un booléen (activation d'un champ).</summary>
public sealed class Negation : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
