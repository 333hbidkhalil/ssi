namespace Ssi.Domain.Calculs;


public sealed record EcheanceTresorerie(DateOnly Date, decimal Montant, bool Encaissement, string Libelle);

public sealed record PrevisionTresorerie(
    int Horizon, decimal DisponibleInitial, decimal FondsEnTransit, decimal EncaissementsAttendus,
    decimal DecaissementsPrevus, decimal SoldePrevu, decimal CreancesEchuesNonIncluses, string Hypotheses);

/// <summary>Règles R-21 à R-30 : créances, dettes, caisse, trésorerie.</summary>
public static class CalculsFinance
{
    /// <summary>R-21 : reste dû d'une facture.</summary>
    public static decimal ResteDu(decimal totalTtc, decimal affectations, decimal avoirsImputes) => totalTtc - affectations - avoirsImputes;

    public static EtatReglementFacture Etat(decimal totalTtc, decimal resteDu)
        => resteDu <= 0 ? EtatReglementFacture.Soldee : resteDu < totalTtc ? EtatReglementFacture.PartiellementReglee : EtatReglementFacture.Ouverte;

    /// <summary>R-24 : jours de retard (0 si non échue ou soldée).</summary>
    public static int JoursRetard(DateOnly echeance, DateOnly aujourdhui, decimal resteDu)
        => resteDu > 0 && echeance < aujourdhui ? aujourdhui.DayNumber - echeance.DayNumber : 0;

    /// <summary>R-27 : solde théorique de caisse.</summary>
    public static decimal SoldeTheoriqueCaisse(decimal soldeOuverture, decimal encaissements, decimal decaissements)
        => soldeOuverture + encaissements - decaissements;

    /// <summary>R-27 : écart de caisse = compté − théorique.</summary>
    public static decimal EcartCaisse(decimal montantCompte, decimal soldeTheorique) => montantCompte - soldeTheorique;

    /// <summary>R-30 : prévision de trésorerie à J+horizon.</summary>
    public static PrevisionTresorerie Prevision(
        DateOnly aujourdhui, int horizon, decimal disponible, decimal fondsEnTransit,
        IEnumerable<EcheanceTresorerie> echeances, decimal tauxRecouvrement = 1m, bool inclureCreancesEchues = false)
    {
        var limite = aujourdhui.AddDays(horizon);
        decimal enc = 0, dec = 0, echuesExclues = 0;
        foreach (var e in echeances)
        {
            if (e.Date > limite) continue;
            if (e.Encaissement)
            {
                if (e.Date < aujourdhui && !inclureCreancesEchues) { echuesExclues += e.Montant; continue; }
                enc += Arrondi.Montant(e.Montant * tauxRecouvrement);
            }
            else dec += e.Montant;
        }
        var hyp = $"Taux de recouvrement supposé {tauxRecouvrement:P0} ; créances échues {(inclureCreancesEchues ? "incluses" : "exclues")} ; dettes échues incluses (à payer immédiatement).";
        return new PrevisionTresorerie(horizon, disponible, fondsEnTransit, enc, dec, disponible + fondsEnTransit + enc - dec, echuesExclues, hyp);
    }
}
