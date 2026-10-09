namespace Ssi.Domain.Calculs;

/// <summary>Règles d'arrondi uniques de SSI (voir docs/04-regles-calcul.md, convention).</summary>
public static class Arrondi
{
    /// <summary>Montant en dirhams : 2 décimales, demi vers l'extérieur.</summary>
    public static decimal Montant(decimal valeur) => Math.Round(valeur, 2, MidpointRounding.AwayFromZero);

    /// <summary>Volume en litres : 3 décimales.</summary>
    public static decimal Volume(decimal valeur) => Math.Round(valeur, 3, MidpointRounding.AwayFromZero);

    /// <summary>Pourcentage affiché : 3 décimales.</summary>
    public static decimal Pourcentage(decimal valeur) => Math.Round(valeur, 3, MidpointRounding.AwayFromZero);

    /// <summary>Coût unitaire moyen : 6 décimales.</summary>
    public static decimal CoutUnitaire(decimal valeur) => Math.Round(valeur, 6, MidpointRounding.AwayFromZero);

    public static int NombreDecimales(decimal valeur)
    {
        valeur = valeur / 1.000000000000000000000000000000000m; // normalise les zéros de fin
        return (decimal.GetBits(valeur)[3] >> 16) & 0xFF;
    }
}

/// <summary>Résultat d'un ratio qui peut ne pas être calculable (dénominateur nul ou négatif).</summary>
public readonly record struct Ratio(decimal? Valeur, string? Raison)
{
    public bool Calculable => Valeur.HasValue;
    public static Ratio De(decimal v) => new(v, null);
    public static Ratio NonCalculable(string raison) => new(null, raison);
}
