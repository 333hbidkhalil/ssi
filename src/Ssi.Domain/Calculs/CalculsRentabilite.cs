using Ssi.Domain.Commun;

namespace Ssi.Domain.Calculs;

public sealed record IndicateursMarge(decimal MargeBrute, Ratio MargeUnitaire, Ratio TauxMarque, Ratio TauxMarge);

public sealed record SeuilRentabilite(Ratio TauxMcv, Ratio SeuilChiffreAffaires, Ratio SeuilVolume);

/// <summary>Règles R-31 à R-36 : marges, répartition des charges, seuil de rentabilité.</summary>
public static class CalculsRentabilite
{
    /// <summary>R-31, R-32.</summary>
    public static IndicateursMarge Marges(decimal chiffreAffairesNetHt, decimal coutDesVentes, decimal quantite)
    {
        var mb = chiffreAffairesNetHt - coutDesVentes;
        return new IndicateursMarge(
            mb,
            quantite > 0 ? Ratio.De(Math.Round(mb / quantite, 4, MidpointRounding.AwayFromZero)) : Ratio.NonCalculable("Quantité nulle."),
            chiffreAffairesNetHt > 0 ? Ratio.De(Arrondi.Pourcentage(mb / chiffreAffairesNetHt * 100)) : Ratio.NonCalculable("Chiffre d'affaires nul."),
            coutDesVentes > 0 ? Ratio.De(Arrondi.Pourcentage(mb / coutDesVentes * 100)) : Ratio.NonCalculable("Coût des ventes nul."));
    }

    /// <summary>R-34 : répartit un montant selon des bases par activité (CA, volume, marge, coût direct).
    /// Les bases négatives ou nulles ne reçoivent rien ; le reliquat d'arrondi va à la plus grande base.</summary>
    public static IReadOnlyDictionary<Activite, decimal> Repartir(decimal montant, IReadOnlyDictionary<Activite, decimal> bases)
    {
        var positives = bases.Where(b => b.Value > 0).ToList();
        var resultat = bases.Keys.ToDictionary(k => k, _ => 0m);
        if (positives.Count == 0 || montant == 0) return resultat;
        var total = positives.Sum(b => b.Value);
        foreach (var (act, b) in positives) resultat[act] = Arrondi.Montant(montant * b / total);
        var reliquat = montant - resultat.Values.Sum();
        var plusGrande = positives.MaxBy(b => b.Value).Key;
        resultat[plusGrande] += reliquat;
        return resultat;
    }

    /// <summary>R-34 (clé manuelle) : pourcentages dont la somme doit faire 100.</summary>
    public static IReadOnlyDictionary<Activite, decimal> RepartirManuel(decimal montant, IReadOnlyDictionary<Activite, decimal> pourcentages)
    {
        if (pourcentages.Values.Sum() != 100m)
            throw new RegleMetierException("La somme des pourcentages de la clé de répartition doit être égale à 100.");
        return Repartir(montant, pourcentages);
    }

    /// <summary>R-36 : seuil de rentabilité d'une activité homogène.</summary>
    public static SeuilRentabilite Seuil(decimal chargesFixes, decimal chiffreAffaires, decimal coutsVariables, decimal? prixMoyenHt)
    {
        if (chiffreAffaires <= 0)
        {
            var nc = Ratio.NonCalculable("Chiffre d'affaires nul.");
            return new SeuilRentabilite(nc, nc, nc);
        }
        var taux = (chiffreAffaires - coutsVariables) / chiffreAffaires;
        if (taux <= 0)
        {
            var na = Ratio.NonCalculable("Marge sur coûts variables négative ou nulle : seuil non atteignable.");
            return new SeuilRentabilite(Ratio.De(Arrondi.Pourcentage(taux * 100)), na, na);
        }
        var seuil = Arrondi.Montant(chargesFixes / taux);
        var vol = prixMoyenHt is > 0 ? Ratio.De(Arrondi.Volume(seuil / prixMoyenHt.Value)) : Ratio.NonCalculable("Prix moyen inconnu.");
        return new SeuilRentabilite(Ratio.De(Arrondi.Pourcentage(taux * 100)), Ratio.De(seuil), vol);
    }

    /// <summary>Activité de rentabilité dérivée de la catégorie, du type de carburant et du canal.</summary>
    public static Activite ActiviteDe(CategorieProduit categorie, TypeCarburant? carburant, bool vrac) => categorie switch
    {
        CategorieProduit.Carburant when carburant == TypeCarburant.Gasoil => vrac ? Activite.GasoilVrac : Activite.GasoilStation,
        CategorieProduit.Carburant when carburant == TypeCarburant.SansPlomb => vrac ? Activite.SansPlombVrac : Activite.SansPlombStation,
        CategorieProduit.Lubrifiant => Activite.Lubrifiants,
        _ => Activite.Autres
    };
}
