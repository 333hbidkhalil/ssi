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

/// <summary>Libellé français d'une valeur d'énumération de l'API (« Especes » → « Espèces ») ; toute autre valeur est rendue telle quelle.</summary>
public sealed class LibelleEnum : IValueConverter
{
    private static readonly Dictionary<string, string> Libelles = new()
    {
        // Règlements, documents, statuts
        ["Especes"] = "Espèces", ["Cheque"] = "Chèque", ["Credit"] = "Crédit", ["Effet"] = "Effet de commerce",
        ["Cloture"] = "Clôturé", ["Valide"] = "Validé", ["Annule"] = "Annulé", ["Saisi"] = "Saisi",
        ["Confirmee"] = "Confirmée", ["EnCours"] = "En cours", ["Terminee"] = "Terminée", ["Annulee"] = "Annulée",
        ["Demande"] = "Demandé", ["Approuve"] = "Approuvé", ["Rejete"] = "Rejeté",
        ["Explique"] = "Expliqué", ["Regularise"] = "Régularisé", ["Planifiee"] = "Planifiée", ["Validee"] = "Validée",
        ["Ouverte"] = "Ouverte", ["Cloturee"] = "Clôturée", ["PartiellementReglee"] = "Partiellement réglée", ["Soldee"] = "Soldée",
        // Compteurs, produits, mesures
        ["RemiseAZero"] = "Remise à zéro", ["SansPlomb"] = "Sans plomb", ["CoutMoyenPondere"] = "Coût moyen pondéré",
        ["AchatReference"] = "Achat de référence", ["JaugeManuelle"] = "Jauge manuelle", ["BaremeHauteur"] = "Barème hauteur",
        ["NonDeterminee"] = "Non déterminée", ["AnomalieMesure"] = "Anomalie de mesure", ["Temperature"] = "Température",
        ["ErreurSaisie"] = "Erreur de saisie", ["PerteReelle"] = "Perte réelle",
        // Tiers, achats, factures
        ["LivraisonDirecte"] = "Livraison directe", ["BonsStation"] = "Bons station", ["AchatStock"] = "Achat stock",
        ["AchatVrac"] = "Achat vrac", ["Decaissement"] = "Décaissement",
        // Trésorerie, rentabilité, personnel
        ["AttenteCarte"] = "Carte (TPE) à encaisser", ["ChequesAEncaisser"] = "Chèques à encaisser",
        ["ChiffreAffaires"] = "Chiffre d'affaires", ["MargeBrute"] = "Marge brute", ["CoutDirect"] = "Coût direct",
        ["GasoilStation"] = "Gasoil — station", ["SansPlombStation"] = "Sans plomb — station", ["GasoilVrac"] = "Gasoil — vrac",
        ["SansPlombVrac"] = "Sans plomb — vrac", ["ChargeSociale"] = "Charge sociale",
        // Mouvements
        ["StockInitial"] = "Stock initial", ["Reception"] = "Réception", ["VenteArticle"] = "Vente d'article",
        ["RetourClient"] = "Retour client", ["RetourFournisseur"] = "Retour fournisseur", ["TransfertEntrant"] = "Transfert entrant",
        ["TransfertSortant"] = "Transfert sortant", ["ContrePassation"] = "Contre-passation", ["AjustementValeur"] = "Ajustement de valeur",
        ["SoldeInitial"] = "Solde initial", ["EncaissementVenteStation"] = "Encaissement ventes station",
        ["EncaissementVenteArticle"] = "Encaissement vente d'article", ["EncaissementClient"] = "Encaissement client",
        ["PaiementFournisseur"] = "Paiement fournisseur", ["PaiementCharge"] = "Paiement de charge", ["TransfertInterne"] = "Transfert interne",
        ["FraisBancaires"] = "Frais bancaires", ["EcartCaisse"] = "Écart de caisse",
    };

    public static string De(object? valeur) => valeur is Enum e && Libelles.TryGetValue(e.ToString(), out var l) ? l : valeur?.ToString() ?? "";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is Enum ? De(value) : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
