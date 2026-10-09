namespace Ssi.Domain.Calculs;

public sealed record PositionValorisee(decimal Quantite, decimal Cmp, decimal Valeur);

public sealed record EcartJaugeage(decimal EcartLitres, decimal? EcartPourcentage, decimal ValeurIndicative, bool HorsTolerance);

/// <summary>Règles R-09 à R-13 : stock théorique, CMP, écarts, couverture, rotation.</summary>
public static class CalculsStock
{
    /// <summary>R-09 : stock théorique = somme des quantités signées des mouvements.</summary>
    public static decimal StockTheorique(IEnumerable<decimal> quantitesSignees) => quantitesSignees.Sum();

    /// <summary>R-11 : entrée en stock au coût unitaire donné ; recalcule le coût moyen pondéré.</summary>
    public static PositionValorisee Entree(PositionValorisee avant, decimal quantite, decimal coutUnitaire)
    {
        if (quantite <= 0) throw new ArgumentOutOfRangeException(nameof(quantite), "Une entrée doit être positive.");
        var nouvelleQuantite = avant.Quantite + quantite;
        decimal cmp;
        if (avant.Quantite <= 0 || nouvelleQuantite <= 0)
            cmp = coutUnitaire;
        else
            cmp = Arrondi.CoutUnitaire((avant.Quantite * avant.Cmp + quantite * coutUnitaire) / nouvelleQuantite);
        return new PositionValorisee(nouvelleQuantite, cmp, Arrondi.Montant(nouvelleQuantite * cmp));
    }

    /// <summary>R-11 : sortie valorisée au CMP courant, sans modifier le CMP. Renvoie la position et la valeur sortie.</summary>
    public static (PositionValorisee Apres, decimal ValeurSortie) Sortie(PositionValorisee avant, decimal quantite)
    {
        if (quantite <= 0) throw new ArgumentOutOfRangeException(nameof(quantite), "Une sortie doit être positive.");
        var valeurSortie = Arrondi.Montant(quantite * avant.Cmp);
        var q = avant.Quantite - quantite;
        return (new PositionValorisee(q, avant.Cmp, Arrondi.Montant(q * avant.Cmp)), valeurSortie);
    }

    /// <summary>R-10 : écart de jaugeage.</summary>
    public static EcartJaugeage Ecart(decimal volumeMesure, decimal stockTheorique, decimal cmp, decimal toleranceLitres, decimal tolerancePct)
    {
        var ecart = volumeMesure - stockTheorique;
        decimal? pct = stockTheorique > 0 ? Arrondi.Pourcentage(ecart / stockTheorique * 100) : null;
        var hors = Math.Abs(ecart) > toleranceLitres || (pct.HasValue && Math.Abs(pct.Value) > tolerancePct);
        return new EcartJaugeage(ecart, pct, Arrondi.Montant(ecart * cmp), hors);
    }

    /// <summary>R-12 : jours de couverture = stock / moyenne journalière distribuée.</summary>
    public static Ratio JoursCouverture(decimal stock, decimal volumeDistribuePeriode, int nombreJours)
    {
        if (nombreJours <= 0 || volumeDistribuePeriode <= 0) return Ratio.NonCalculable("Aucune distribution sur la période de référence.");
        return Ratio.De(Math.Round(stock / (volumeDistribuePeriode / nombreJours), 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>R-13 : rotation = coût des ventes / stock moyen valorisé.</summary>
    public static Ratio Rotation(decimal coutDesVentes, decimal valeurDebut, decimal valeurFin)
    {
        var moyen = (valeurDebut + valeurFin) / 2;
        if (moyen <= 0) return Ratio.NonCalculable("Stock moyen nul.");
        return Ratio.De(Math.Round(coutDesVentes / moyen, 2, MidpointRounding.AwayFromZero));
    }
}
