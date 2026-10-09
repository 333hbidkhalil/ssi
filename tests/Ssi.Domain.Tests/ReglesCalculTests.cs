using Ssi.Domain.Calculs;
using Ssi.Domain.Commun;
using Ssi.Contracts;

namespace Ssi.Domain.Tests;

/// <summary>Exemples chiffrés de docs/04-regles-calcul.md (un test par règle).</summary>
public class CalculsIndexTests
{
    [Fact(DisplayName = "R-01 volume = index fin − index début")]
    public void R01_Volume() => Assert.Equal(780.25m, CalculsIndex.Volume(125_430.50m, 126_210.75m));

    [Fact(DisplayName = "R-01 index fin inférieur refusé")]
    public void R01_IndexInferieur() =>
        Assert.Throws<RegleMetierException>(() => CalculsIndex.Volume(126_210.75m, 125_430.50m));

    [Fact(DisplayName = "R-01 passage par le maximum du totalisateur")]
    public void R01_PassageMaximum() => Assert.Equal(150.50m, CalculsIndex.VolumeAvecPassageMaximum(999_900.00m, 50.50m, 1_000_000.00m));

    [Fact(DisplayName = "R-02/R-03 montants TTC, HT, TVA")]
    public void R02_R03_Montants()
    {
        var m = CalculsIndex.Montants(780.25m, 12.85m, 0.10m);
        Assert.Equal(10_026.21m, m.Ttc);
        Assert.Equal(9_114.74m, m.Ht);
        Assert.Equal(911.47m, m.Tva);
        Assert.Equal(m.Ttc, m.Ht + m.Tva);
    }

    [Fact(DisplayName = "R-02 aucune erreur de flottant binaire")]
    public void R02_PasDeFlottant()
    {
        // 0,1 + 0,2 en double donne 0,30000000000000004 ; en decimal le calcul est exact.
        Assert.Equal(0.3m, CalculsIndex.Volume(0.1m, 0.4m));
        Assert.Equal(3.86m, CalculsIndex.Montants(0.3m, 12.85m, 0.10m).Ttc); // 3,855 → 3,86 (demi vers l'extérieur)
    }

    [Fact(DisplayName = "R-04 continuité et anomalies")]
    public void R04_Controles()
    {
        var t = new ToleranceIndex(3m);
        var a = CalculsIndex.Controler(100m, 50m, 100m, [], t);
        Assert.Contains(a, x => x.Code == "INDEX_FIN_INFERIEUR" && x.Gravite == GraviteAnomalie.Bloquant);

        var b = CalculsIndex.Controler(105m, 200m, 100m, [], t);
        Assert.Contains(b, x => x.Code == "CONTINUITE" && x.Gravite == GraviteAnomalie.Bloquant);

        var c = CalculsIndex.Controler(100m, 500m, 100m, [100m, 100m, 100m], t);
        Assert.Contains(c, x => x.Code == "VOLUME_INHABITUEL" && x.Gravite == GraviteAnomalie.Avertissement);

        var d = CalculsIndex.Controler(100m, 2100m, 100m, [100m, 100m], t);
        Assert.Contains(d, x => x.Code == "SAISIE_PROBABLE");

        var e = CalculsIndex.Controler(100m, 180m, 100m, [100m], t);
        Assert.Empty(e);

        var f = CalculsIndex.Controler(100m, 180.1234m, 100m, [], t);
        Assert.Contains(f, x => x.Code == "DECIMALES");
    }

    [Fact(DisplayName = "R-05 écart de poste")]
    public void R05_EcartPoste() => Assert.Equal(26.21m, CalculsIndex.EcartPoste(10_026.21m, [6_000m, 3_000m, 1_000m]));
}

public class CalculsStockTests
{
    [Fact(DisplayName = "R-09 stock théorique")]
    public void R09() => Assert.Equal(24_219.75m, CalculsStock.StockTheorique([10_000m, 15_000m, -780.25m]));

    [Fact(DisplayName = "R-10 écart de jaugeage")]
    public void R10()
    {
        var e = CalculsStock.Ecart(24_150m, 24_219.75m, 11.20m, 100m, 0.5m);
        Assert.Equal(-69.75m, e.EcartLitres);
        Assert.Equal(-0.288m, e.EcartPourcentage);
        Assert.Equal(-781.20m, e.ValeurIndicative);
        Assert.False(e.HorsTolerance);
        Assert.True(CalculsStock.Ecart(24_000m, 24_219.75m, 11.20m, 100m, 0.5m).HorsTolerance);
    }

    [Fact(DisplayName = "R-11 coût moyen pondéré")]
    public void R11()
    {
        var p = CalculsStock.Entree(new PositionValorisee(100m, 50m, 5_000m), 50m, 56m);
        Assert.Equal(52m, p.Cmp);
        Assert.Equal(150m, p.Quantite);
        var (apres, valeur) = CalculsStock.Sortie(p, 30m);
        Assert.Equal(1_560m, valeur);
        Assert.Equal(52m, apres.Cmp);
        Assert.Equal(120m, apres.Quantite);
    }

    [Fact(DisplayName = "Scénario 4 : CMP après réception carburant")]
    public void Scenario4_Cmp()
    {
        var p = CalculsStock.Entree(new PositionValorisee(10_000m, 11.20m, 112_000m), 15_000m, 11.30m);
        Assert.Equal(25_000m, p.Quantite);
        Assert.Equal(11.26m, p.Cmp);
    }

    [Fact(DisplayName = "R-11 entrée sur stock nul ou négatif")]
    public void R11_StockNul() => Assert.Equal(9m, CalculsStock.Entree(new PositionValorisee(-5m, 7m, -35m), 10m, 9m).Cmp);

    [Fact(DisplayName = "R-12 jours de couverture")]
    public void R12()
    {
        Assert.Equal(10m, CalculsStock.JoursCouverture(10_000m, 7_000m, 7).Valeur);
        Assert.False(CalculsStock.JoursCouverture(10_000m, 0m, 7).Calculable);
    }

    [Fact(DisplayName = "R-13 rotation")]
    public void R13() => Assert.Equal(4m, CalculsStock.Rotation(40_000m, 9_000m, 11_000m).Valeur);
}

public class CalculsVracTests
{
    [Fact(DisplayName = "R-14 marge prévisionnelle")]
    public void R14()
    {
        var m = CalculsVrac.Prevue(20_000m, 11.50m, 11.05m, 1_500m);
        Assert.Equal(7_500m, m.Marge);
        Assert.Equal(230_000m, m.ChiffreAffairesHt);
        Assert.True(CalculsVrac.MargeAcceptable(m, 2m));
        Assert.False(CalculsVrac.MargeAcceptable(m, 5m));
        Assert.False(CalculsVrac.MargeAcceptable(CalculsVrac.Prevue(1_000m, 11m, 11.10m, 0m), 0m));
    }

    [Fact(DisplayName = "R-15 à R-17 marge réelle et provisoire")]
    public void R17()
    {
        var provisoire = CalculsVrac.Reelle(229_425m, 0m, true, 0m, false, 0m, true, 7_500m);
        Assert.True(provisoire.Provisoire);
        Assert.Contains("Facture fournisseur", provisoire.DonneesManquantes);

        var reelle = CalculsVrac.Reelle(229_425m, 0m, true, 221_000m, true, 1_600m, false, 7_500m);
        Assert.False(reelle.Provisoire);
        Assert.Equal(6_825m, reelle.Marge);
        Assert.Equal(-675m, reelle.EcartVsPrevue);
    }

    [Fact(DisplayName = "R-18 répartition au prorata avec reliquat")]
    public void R18()
    {
        var parts = CalculsVrac.RepartirAuProrata(1_000m, [1_000m, 1_000m, 1_000m]);
        Assert.Equal([333.33m, 333.33m, 333.34m], parts);
        Assert.Equal(1_000m, parts.Sum());
    }

    [Fact(DisplayName = "R-19 écart chargé / livré")]
    public void R19()
    {
        var (ecart, pct, just) = CalculsVrac.EcartQuantite(20_000m, 19_950m, 0.5m);
        Assert.Equal(-50m, ecart);
        Assert.Equal(-0.25m, pct);
        Assert.False(just);
        Assert.True(CalculsVrac.EcartQuantite(20_000m, 19_800m, 0.5m).JustificationRequise);
    }
}

public class CalculsFinanceTests
{
    [Fact(DisplayName = "R-20 ligne de facture")]
    public void R20()
    {
        var l = CalculsTaxes.Ligne(19_950m, 11.50m, 0.10m);
        Assert.Equal(229_425m, l.Ht);
        Assert.Equal(22_942.50m, l.Tva);
        Assert.Equal(252_367.50m, l.Ttc);
    }

    [Fact(DisplayName = "R-21 reste dû et état")]
    public void R21()
    {
        var reste = CalculsFinance.ResteDu(1_100m, 600m, 0m);
        Assert.Equal(500m, reste);
        Assert.Equal(EtatReglementFacture.PartiellementReglee, CalculsFinance.Etat(1_100m, reste));
        Assert.Equal(EtatReglementFacture.Soldee, CalculsFinance.Etat(1_100m, 0m));
    }

    [Fact(DisplayName = "R-24 retard")]
    public void R24()
    {
        Assert.Equal(10, CalculsFinance.JoursRetard(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 10), 1m));
        Assert.Equal(0, CalculsFinance.JoursRetard(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 10), 0m));
    }

    [Fact(DisplayName = "R-27 caisse : solde théorique et écart")]
    public void R27()
    {
        var theorique = CalculsFinance.SoldeTheoriqueCaisse(2_000m, 6_000m, 500m);
        Assert.Equal(7_500m, theorique);
        Assert.Equal(-20m, CalculsFinance.EcartCaisse(7_480m, theorique));
    }

    [Fact(DisplayName = "R-30 prévision : créances échues exclues par défaut")]
    public void R30()
    {
        var auj = new DateOnly(2026, 10, 9);
        var p = CalculsFinance.Prevision(auj, 7, 10_000m, 2_000m,
        [
            new(auj.AddDays(3), 5_000m, true, "Client A"),
            new(auj.AddDays(-5), 4_000m, true, "Client B échu"),
            new(auj.AddDays(20), 9_000m, true, "Hors horizon"),
            new(auj.AddDays(2), 3_000m, false, "Fournisseur"),
        ]);
        Assert.Equal(5_000m, p.EncaissementsAttendus);
        Assert.Equal(3_000m, p.DecaissementsPrevus);
        Assert.Equal(4_000m, p.CreancesEchuesNonIncluses);
        Assert.Equal(14_000m, p.SoldePrevu);
    }
}

public class CalculsRentabiliteTests
{
    [Fact(DisplayName = "R-31/R-32 marges et ratios")]
    public void R32()
    {
        var m = CalculsRentabilite.Marges(9_114.74m, 8_738.80m, 780.25m);
        Assert.Equal(375.94m, m.MargeBrute);
        Assert.Equal(0.4818m, m.MargeUnitaire.Valeur);
        Assert.Equal(4.125m, m.TauxMarque.Valeur);
        Assert.False(CalculsRentabilite.Marges(0m, 0m, 0m).TauxMarque.Calculable);
    }

    [Fact(DisplayName = "R-34 répartition des charges communes")]
    public void R34()
    {
        var r = CalculsRentabilite.Repartir(1_000m, new Dictionary<Activite, decimal>
        {
            [Activite.GasoilStation] = 600m, [Activite.SansPlombStation] = 300m, [Activite.Lubrifiants] = 100m, [Activite.GasoilVrac] = 0m
        });
        Assert.Equal(600m, r[Activite.GasoilStation]);
        Assert.Equal(0m, r[Activite.GasoilVrac]);
        Assert.Equal(1_000m, r.Values.Sum());

        var r3 = CalculsRentabilite.Repartir(100m, new Dictionary<Activite, decimal>
            { [Activite.GasoilStation] = 1m, [Activite.SansPlombStation] = 1m, [Activite.Lubrifiants] = 1m });
        Assert.Equal(100m, r3.Values.Sum());

        Assert.Throws<RegleMetierException>(() => CalculsRentabilite.RepartirManuel(100m,
            new Dictionary<Activite, decimal> { [Activite.GasoilStation] = 60m, [Activite.Lubrifiants] = 30m }));
    }

    [Fact(DisplayName = "R-36 seuil de rentabilité")]
    public void R36()
    {
        var s = CalculsRentabilite.Seuil(60_000m, 500_000m, 470_000m, 10m);
        Assert.Equal(6m, s.TauxMcv.Valeur);
        Assert.Equal(1_000_000m, s.SeuilChiffreAffaires.Valeur);
        Assert.Equal(100_000m, s.SeuilVolume.Valeur);
        Assert.False(CalculsRentabilite.Seuil(60_000m, 500_000m, 510_000m, 10m).SeuilChiffreAffaires.Calculable);
    }

    [Fact(DisplayName = "Activités dérivées")]
    public void Activites()
    {
        Assert.Equal(Activite.GasoilVrac, CalculsRentabilite.ActiviteDe(CategorieProduit.Carburant, TypeCarburant.Gasoil, true));
        Assert.Equal(Activite.SansPlombStation, CalculsRentabilite.ActiviteDe(CategorieProduit.Carburant, TypeCarburant.SansPlomb, false));
        Assert.Equal(Activite.Lubrifiants, CalculsRentabilite.ActiviteDe(CategorieProduit.Lubrifiant, null, false));
    }
}
