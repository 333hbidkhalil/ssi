namespace Ssi.Domain.Calculs;

public sealed record MargePrevue(decimal ChiffreAffairesHt, decimal CoutAchatHt, decimal FraisHt, decimal Marge, Ratio TauxMarque);

public sealed record MargeReelle(
    decimal ChiffreAffairesNetHt, decimal CoutAchatHt, decimal FraisDirectsHt, decimal CoutDirectHt,
    decimal Marge, decimal? EcartVsPrevue, bool Provisoire, IReadOnlyList<string> DonneesManquantes);

/// <summary>Règles R-14 à R-19 : livraisons directes en vrac.</summary>
public static class CalculsVrac
{
    /// <summary>R-14 : marge prévisionnelle.</summary>
    public static MargePrevue Prevue(decimal volume, decimal prixVenteHt, decimal prixAchatHt, decimal fraisPrevusHt)
    {
        var ca = Arrondi.Montant(volume * prixVenteHt);
        var cout = Arrondi.Montant(volume * prixAchatHt);
        var marge = Arrondi.Montant(volume * (prixVenteHt - prixAchatHt)) - fraisPrevusHt;
        var taux = ca > 0 ? Ratio.De(Arrondi.Pourcentage(marge / ca * 100)) : Ratio.NonCalculable("Chiffre d'affaires prévu nul.");
        return new MargePrevue(ca, cout, fraisPrevusHt, marge, taux);
    }

    /// <summary>Contrôle du seuil de marge prévisionnelle : vrai si la marge est acceptable sans dérogation.</summary>
    public static bool MargeAcceptable(MargePrevue marge, decimal seuilPct)
        => marge.Marge >= 0 && marge.TauxMarque.Valeur.HasValue && marge.TauxMarque.Valeur.Value >= seuilPct;

    /// <summary>R-15 à R-17 : marge réelle et statut provisoire.</summary>
    public static MargeReelle Reelle(
        decimal facturesClientHt, decimal avoirsClientHt, bool factureClientPresente,
        decimal facturesFournisseurHt, bool factureFournisseurPresente,
        decimal fraisDirectsHt, bool fraisAttendusManquants, decimal? margePrevue)
    {
        var manquants = new List<string>();
        if (!factureClientPresente) manquants.Add("Facture client");
        if (!factureFournisseurPresente) manquants.Add("Facture fournisseur");
        if (fraisAttendusManquants) manquants.Add("Frais de transport ou frais direct attendu");
        var ca = facturesClientHt - avoirsClientHt;
        var coutDirect = facturesFournisseurHt + fraisDirectsHt;
        var marge = ca - coutDirect;
        return new MargeReelle(ca, facturesFournisseurHt, fraisDirectsHt, coutDirect, marge,
            margePrevue.HasValue ? marge - margePrevue.Value : null, manquants.Count > 0, manquants);
    }

    /// <summary>R-18 : répartition d'un frais au prorata des volumes ; le reliquat d'arrondi va à la dernière part.</summary>
    public static IReadOnlyList<decimal> RepartirAuProrata(decimal montant, IReadOnlyList<decimal> bases)
    {
        if (bases.Count == 0) throw new ArgumentException("Aucune base de répartition.", nameof(bases));
        var total = bases.Sum();
        if (total <= 0) throw new Commun.RegleMetierException("La base de répartition totale doit être positive.");
        var parts = new decimal[bases.Count];
        decimal cumul = 0;
        for (var i = 0; i < bases.Count - 1; i++)
        {
            parts[i] = Arrondi.Montant(montant * bases[i] / total);
            cumul += parts[i];
        }
        parts[^1] = montant - cumul;
        return parts;
    }

    /// <summary>R-19 : écart de quantité livrée / chargée et nécessité d'une justification.</summary>
    public static (decimal Ecart, decimal? EcartPct, bool JustificationRequise) EcartQuantite(decimal chargee, decimal livree, decimal tolerancePct)
    {
        var ecart = livree - chargee;
        decimal? pct = chargee > 0 ? Arrondi.Pourcentage(ecart / chargee * 100) : null;
        return (ecart, pct, pct.HasValue ? Math.Abs(pct.Value) > tolerancePct : ecart != 0);
    }
}
