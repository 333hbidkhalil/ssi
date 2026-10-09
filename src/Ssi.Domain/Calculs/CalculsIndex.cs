namespace Ssi.Domain.Calculs;

public enum GraviteAnomalie { Avertissement = 1, Bloquant = 2 }

public sealed record AnomalieIndex(string Code, string Message, GraviteAnomalie Gravite);

public sealed record MontantsTaxes(decimal Ht, decimal Tva, decimal Ttc);

/// <summary>Paramètres de tolérance pour les contrôles d'index (R-04).</summary>
public sealed record ToleranceIndex(decimal FacteurVolumeInhabituel = 3m, decimal? VolumeMaxPoste = null, int DecimalesMax = 3);

/// <summary>Règles R-01 à R-04 et R-05 : volucompteurs et écarts de poste.</summary>
public static class CalculsIndex
{
    /// <summary>R-01 : volume distribué = index final − index initial.</summary>
    public static decimal Volume(decimal indexDebut, decimal indexFin)
    {
        if (indexFin < indexDebut)
            throw new Commun.RegleMetierException(
                $"L'index final ({indexFin}) est inférieur à l'index initial ({indexDebut}). Utilisez la procédure de remise à zéro si le totalisateur a été réinitialisé.",
                "INDEX_FIN_INFERIEUR");
        return indexFin - indexDebut;
    }

    /// <summary>R-01 : volume lorsque le totalisateur est passé par son maximum (remise à zéro validée).</summary>
    public static decimal VolumeAvecPassageMaximum(decimal indexDebut, decimal indexFin, decimal indexMaximum)
    {
        if (indexDebut > indexMaximum || indexFin > indexMaximum)
            throw new Commun.RegleMetierException("Index supérieur au maximum du totalisateur.", "INDEX_HORS_MAXIMUM");
        return (indexMaximum - indexDebut) + indexFin;
    }

    /// <summary>R-02 et R-03 : montants TTC, HT et TVA d'un volume vendu à un prix TTC.</summary>
    public static MontantsTaxes Montants(decimal volume, decimal prixUnitaireTtc, decimal tauxTva)
    {
        var ttc = Arrondi.Montant(volume * prixUnitaireTtc);
        return CalculsTaxes.DepuisTtc(ttc, tauxTva);
    }

    /// <summary>R-04 : contrôles d'un relevé. Les anomalies bloquantes doivent être refusées ; les avertissements confirmés.</summary>
    public static IReadOnlyList<AnomalieIndex> Controler(
        decimal indexDebut, decimal indexFin, decimal? indexFinPrecedent,
        IReadOnlyCollection<decimal> volumesRecents, ToleranceIndex tolerance)
    {
        var anomalies = new List<AnomalieIndex>();
        if (indexFin < indexDebut)
            anomalies.Add(new("INDEX_FIN_INFERIEUR", "L'index final est inférieur à l'index initial.", GraviteAnomalie.Bloquant));
        if (indexFinPrecedent.HasValue && indexDebut != indexFinPrecedent.Value)
            anomalies.Add(new("CONTINUITE",
                $"L'index de début ({indexDebut}) ne correspond pas à l'index de fin du relevé précédent ({indexFinPrecedent}).",
                GraviteAnomalie.Bloquant));
        if (Arrondi.NombreDecimales(indexDebut) > tolerance.DecimalesMax || Arrondi.NombreDecimales(indexFin) > tolerance.DecimalesMax)
            anomalies.Add(new("DECIMALES", $"Un index comporte plus de {tolerance.DecimalesMax} décimales : erreur de saisie probable.", GraviteAnomalie.Avertissement));

        if (indexFin >= indexDebut)
        {
            var volume = indexFin - indexDebut;
            if (volume == 0)
                anomalies.Add(new("VOLUME_NUL", "Volume nul sur ce pistolet pour le poste.", GraviteAnomalie.Avertissement));
            if (volumesRecents.Count > 0)
            {
                var moyenne = volumesRecents.Average();
                if (moyenne > 0 && volume > moyenne * 10)
                    anomalies.Add(new("SAISIE_PROBABLE", $"Volume {volume} L plus de 10 fois supérieur à la moyenne récente ({Arrondi.Volume(moyenne)} L) : erreur de saisie probable.", GraviteAnomalie.Avertissement));
                else if (moyenne > 0 && volume > moyenne * tolerance.FacteurVolumeInhabituel)
                    anomalies.Add(new("VOLUME_INHABITUEL", $"Volume {volume} L supérieur à {tolerance.FacteurVolumeInhabituel} fois la moyenne récente ({Arrondi.Volume(moyenne)} L).", GraviteAnomalie.Avertissement));
            }
            if (tolerance.VolumeMaxPoste.HasValue && volume > tolerance.VolumeMaxPoste.Value)
                anomalies.Add(new("VOLUME_MAX", $"Volume {volume} L supérieur au maximum paramétré par poste ({tolerance.VolumeMaxPoste} L).", GraviteAnomalie.Avertissement));
        }
        return anomalies;
    }

    /// <summary>R-05 : écart de poste = théorique − règlements attribués (positif = manquant).</summary>
    public static decimal EcartPoste(decimal montantTheoriqueTtc, IEnumerable<decimal> reglementsAttribues)
        => montantTheoriqueTtc - reglementsAttribues.Sum();
}

/// <summary>Conversions HT / TVA / TTC (R-03, R-20).</summary>
public static class CalculsTaxes
{
    /// <summary>Décompose un TTC : HT = arrondi(TTC / (1 + taux)), TVA = TTC − HT.</summary>
    public static MontantsTaxes DepuisTtc(decimal ttc, decimal taux)
    {
        if (taux < 0) throw new ArgumentOutOfRangeException(nameof(taux));
        var ht = Arrondi.Montant(ttc / (1 + taux));
        return new MontantsTaxes(ht, ttc - ht, ttc);
    }

    /// <summary>R-20 : ligne de facture : HT = arrondi(q × PU HT), TVA = arrondi(HT × taux), TTC = HT + TVA.</summary>
    public static MontantsTaxes Ligne(decimal quantite, decimal prixUnitaireHt, decimal taux)
    {
        var ht = Arrondi.Montant(quantite * prixUnitaireHt);
        var tva = Arrondi.Montant(ht * taux);
        return new MontantsTaxes(ht, tva, ht + tva);
    }

    /// <summary>Ligne de vente au détail à prix TTC.</summary>
    public static MontantsTaxes LigneTtc(decimal quantite, decimal prixUnitaireTtc, decimal taux)
        => DepuisTtc(Arrondi.Montant(quantite * prixUnitaireTtc), taux);

    /// <summary>Prix HT unitaire équivalent à un prix TTC (pour analyses), précision 4 décimales.</summary>
    public static decimal PrixHt(decimal prixTtc, decimal taux) => Math.Round(prixTtc / (1 + taux), 4, MidpointRounding.AwayFromZero);
}
