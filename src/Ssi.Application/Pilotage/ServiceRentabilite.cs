using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Domain.Calculs;
using Ssi.Domain.Entites;

namespace Ssi.Application.Pilotage;

/// <summary>Agrégats d'une activité sur une période, base commune du rapport de rentabilité, du seuil, des simulations et du tableau de bord.</summary>
public sealed class AgregatActivite(Activite activite)
{
    public Activite Activite { get; } = activite;
    public decimal Quantite { get; set; }
    public decimal ChiffreAffairesHt { get; set; }
    public decimal CoutDesVentes { get; set; }
    public decimal FraisDirects { get; set; }
    public decimal ChargesDirectes { get; set; }
    public decimal ChargesReparties { get; set; }
    public decimal ChargesFixes { get; set; }
    public decimal MargeBrute => ChiffreAffairesHt - CoutDesVentes;
    public decimal Contribution => MargeBrute - FraisDirects;
    public decimal Resultat => Contribution - ChargesDirectes - ChargesReparties;
}

public sealed record AgregatsPeriode(DateOnly Du, DateOnly Au, IReadOnlyDictionary<Activite, AgregatActivite> Activites, List<RepartitionChargeDto> Charges,
    List<string> Avertissements, bool Incomplet);

/// <summary>
/// Rentabilité par activité (R-31 à R-37). Sources uniques du chiffre d'affaires : relevés d'index validés, ventes d'articles validées,
/// factures client vrac validées. Les bons de crédit, règlements, factures récapitulatives et virements n'en créent jamais.
/// </summary>
public class ServiceRentabilite(ISsiDb db, IHorloge horloge, IUtilisateurCourant courant)
{
    public static string Libelle(Activite a) => a switch
    {
        Activite.GasoilStation => "Gasoil — station",
        Activite.SansPlombStation => "Sans plomb — station",
        Activite.GasoilVrac => "Gasoil — livraison directe",
        Activite.SansPlombVrac => "Sans plomb — livraison directe",
        Activite.Lubrifiants => "Lubrifiants",
        _ => "Autres articles"
    };

    private static bool EnLitres(Activite a) => a is Activite.GasoilStation or Activite.SansPlombStation or Activite.GasoilVrac or Activite.SansPlombVrac;

    private const string Methode = "CA HT = relevés d'index validés + ventes d'articles validées + factures client vrac (− avoirs). " +
        "Coût des ventes = sorties de stock au coût moyen pondéré (station, articles) ; factures fournisseur liées aux livraisons (vrac). " +
        "Contribution = marge brute − frais directs (transport vrac). Charges des centres d'activité affectées directement ; charges communes, frais bancaires " +
        "et écarts de caisse répartis par la clé du centre. Résultat opérationnel estimé, hors impôts et amortissements : ce n'est pas un résultat comptable.";

    public async Task<AgregatsPeriode> AgregerAsync(DateOnly du, DateOnly au, CancellationToken ct)
    {
        if (au < du) throw new ValidationException("La date de fin précède la date de début.");
        if (au.DayNumber - du.DayNumber > 3660) throw new ValidationException("Période trop longue (10 ans maximum).");
        var acts = Enum.GetValues<Activite>().ToDictionary(a => a, a => new AgregatActivite(a));
        var avert = new List<string>();
        var incomplet = false;
        var produits = await db.Produits.AsNoTracking().Include(p => p.Famille).ToDictionaryAsync(p => p.Id, ct);
        Activite ActiviteProduit(Guid id, bool vrac) => produits.TryGetValue(id, out var p) ? CalculsRentabilite.ActiviteDe(p.Famille!.Categorie, p.TypeCarburant, vrac) : Activite.Autres;

        // Station : relevés des postes validés dont la journée d'exploitation est dans la période
        var postes = await db.Postes.AsNoTracking().Where(p => p.Statut == StatutPoste.Valide && p.JourneeExploitation >= du && p.JourneeExploitation <= au).Select(p => p.Id).ToListAsync(ct);
        var releves = await db.Releves.AsNoTracking().Where(r => postes.Contains(r.PosteId) && r.Statut == StatutReleve.Valide)
            .GroupBy(r => r.Pistolet!.ProduitId).Select(g => new { ProduitId = g.Key, Volume = g.Sum(r => r.Volume), Ht = g.Sum(r => r.MontantHt) }).ToListAsync(ct);
        var cpvPostes = await db.MouvementsStock.AsNoTracking()
            .Where(m => m.DocumentType == "POSTE" && m.DocumentId != null && postes.Contains(m.DocumentId.Value) && (m.Type == TypeMouvementStock.Distribution || m.Type == TypeMouvementStock.ContrePassation))
            .GroupBy(m => m.ProduitId).Select(g => new { g.Key, V = -g.Sum(m => m.Valeur) }).ToDictionaryAsync(x => x.Key, x => x.V, ct);
        foreach (var r in releves)
        {
            var a = acts[ActiviteProduit(r.ProduitId, false)];
            a.Quantite += r.Volume; a.ChiffreAffairesHt += r.Ht; a.CoutDesVentes += cpvPostes.GetValueOrDefault(r.ProduitId);
        }
        var nonValides = await db.Postes.CountAsync(p => p.Statut != StatutPoste.Valide && p.JourneeExploitation >= du && p.JourneeExploitation <= au, ct);
        if (nonValides > 0) { incomplet = true; avert.Add($"{nonValides} poste(s) de la période ne sont pas validés : leurs ventes ne sont pas incluses."); }

        // Articles : ventes validées de la période
        var (debut, fin) = (horloge.DebutJournee(du), horloge.DebutJournee(au.AddDays(1)));
        var ventes = await db.VentesArticles.AsNoTracking().Where(v => v.Statut == StatutDocument.Valide && v.Horodatage >= debut && v.Horodatage < fin).Select(v => v.Id).ToListAsync(ct);
        var lignesVente = await db.LignesVenteArticle.AsNoTracking().Where(l => ventes.Contains(l.VenteId))
            .GroupBy(l => l.ProduitId).Select(g => new { g.Key, Q = g.Sum(l => l.Quantite), Ht = g.Sum(l => l.MontantHt) }).ToListAsync(ct);
        var cpvVentes = await db.MouvementsStock.AsNoTracking()
            .Where(m => m.DocumentType == "VenteArticle" && m.DocumentId != null && ventes.Contains(m.DocumentId.Value))
            .GroupBy(m => m.ProduitId).Select(g => new { g.Key, V = -g.Sum(m => m.Valeur) }).ToDictionaryAsync(x => x.Key, x => x.V, ct);
        foreach (var l in lignesVente)
        {
            var a = acts[ActiviteProduit(l.Key, false)];
            a.Quantite += l.Q; a.ChiffreAffairesHt += l.Ht; a.CoutDesVentes += cpvVentes.GetValueOrDefault(l.Key);
        }

        // Vrac : factures client de la période, coûts rattachés aux mêmes livraisons (rapprochement des produits et des charges)
        var facturesVrac = await db.Factures.AsNoTracking().Include(f => f.Lignes)
            .Where(f => f.Sens == SensFacture.Client && f.Origine == OrigineFacture.Vrac && f.Statut == StatutDocument.Valide && f.Date >= du && f.Date <= au).ToListAsync(ct);
        var produitCommande = await db.CommandesClient.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.ProduitId, ct);
        var livraisonsFacturees = new HashSet<Guid>();
        foreach (var f in facturesVrac)
            foreach (var l in f.Lignes)
            {
                var pid = l.ProduitId ?? (f.CommandeClientId is { } cc ? produitCommande.GetValueOrDefault(cc) : Guid.Empty);
                var a = acts[ActiviteProduit(pid, true)];
                if (f.Type == TypeFacture.Facture)
                {
                    a.Quantite += l.Quantite; a.ChiffreAffairesHt += l.MontantHt;
                    if (l.LivraisonId is { } lid) livraisonsFacturees.Add(lid);
                }
                else a.ChiffreAffairesHt -= l.MontantHt;
            }
        if (livraisonsFacturees.Count > 0)
        {
            var livs = await db.LivraisonsDirectes.AsNoTracking().Where(l => livraisonsFacturees.Contains(l.Id)).ToListAsync(ct);
            var lignesFournisseur = await db.LignesFacture.AsNoTracking().Where(l => l.LivraisonId != null && livraisonsFacturees.Contains(l.LivraisonId.Value))
                .Join(db.Factures.AsNoTracking().Where(f => f.Sens == SensFacture.Fournisseur && f.Type == TypeFacture.Facture && f.Origine == OrigineFacture.AchatVrac && f.Statut == StatutDocument.Valide),
                    l => l.FactureId, f => f.Id, (l, f) => new { l.LivraisonId, l.MontantHt, FactureId = f.Id }).ToListAsync(ct);
            var idsFF = lignesFournisseur.Select(x => x.FactureId).Distinct().ToList();
            var avoirsF = await db.Factures.AsNoTracking().Where(a => a.Type == TypeFacture.Avoir && a.Statut == StatutDocument.Valide && a.FactureOrigineId != null && idsFF.Contains(a.FactureOrigineId.Value))
                .GroupBy(a => a.FactureOrigineId!.Value).Select(g => new { g.Key, Ht = g.Sum(a => a.TotalHt) }).ToDictionaryAsync(x => x.Key, x => x.Ht, ct);
            var frais = await db.RepartitionsFrais.AsNoTracking().Where(r => livraisonsFacturees.Contains(r.LivraisonId)).GroupBy(r => r.LivraisonId)
                .Select(g => new { g.Key, M = g.Sum(r => r.MontantHt) }).ToDictionaryAsync(x => x.Key, x => x.M, ct);
            var estimees = 0;
            foreach (var l in livs)
            {
                var a = acts[ActiviteProduit(l.ProduitId, true)];
                var couts = lignesFournisseur.Where(x => x.LivraisonId == l.Id).ToList();
                if (couts.Count > 0) a.CoutDesVentes += couts.Sum(x => x.MontantHt);
                else { a.CoutDesVentes += Arrondi.Montant(l.QuantiteChargee * l.PrixAchatHt); estimees++; }
                a.FraisDirects += frais.GetValueOrDefault(l.Id);
            }
            foreach (var (fid, ht) in avoirsF)
            {
                var liv = livs.First(l => lignesFournisseur.Any(x => x.FactureId == fid && x.LivraisonId == l.Id));
                acts[ActiviteProduit(liv.ProduitId, true)].CoutDesVentes -= ht;
            }
            if (estimees > 0)
            {
                incomplet = true;
                avert.Add($"{estimees} livraison(s) directe(s) sans facture fournisseur : coût estimé au prix d'achat de la commande (marge provisoire).");
            }
        }

        // Charges d'exploitation
        var cles = await db.ClesRepartition.AsNoTracking().Include(c => c.Pourcentages).ToDictionaryAsync(c => c.Id, ct);
        var charges = await db.Charges.AsNoTracking().Include(c => c.CentreCout).Include(c => c.Categorie)
            .Where(c => c.Statut == StatutDocument.Valide && c.Date >= du && c.Date <= au).ToListAsync(ct);
        var reparties = new List<RepartitionChargeDto>();

        IReadOnlyDictionary<Activite, decimal> Bases(CleRepartition? cle) => (cle?.Methode ?? MethodeCleRepartition.ChiffreAffaires) switch
        {
            MethodeCleRepartition.Volume => acts.ToDictionary(x => x.Key, x => EnLitres(x.Key) ? x.Value.Quantite : 0m),
            MethodeCleRepartition.MargeBrute => acts.ToDictionary(x => x.Key, x => x.Value.MargeBrute),
            MethodeCleRepartition.CoutDirect => acts.ToDictionary(x => x.Key, x => x.Value.CoutDesVentes + x.Value.FraisDirects),
            MethodeCleRepartition.Manuelle => cle!.Pourcentages.ToDictionary(p => p.Activite, p => p.Pourcentage),
            _ => acts.ToDictionary(x => x.Key, x => x.Value.ChiffreAffairesHt)
        };

        void Affecter(string source, string centre, Activite? activite, CleRepartition? cle, decimal montant, bool fixe)
        {
            if (montant == 0) return;
            Dictionary<Activite, decimal> parts;
            if (activite is { } act) parts = new() { [act] = montant };
            else
            {
                var bases = Bases(cle);
                if (bases.Values.All(b => b <= 0)) bases = acts.ToDictionary(x => x.Key, x => x.Value.ChiffreAffairesHt);
                if (bases.Values.All(b => b <= 0))
                {
                    avert.Add($"« {source} » ({montant:N2} DH) : aucune activité sur la période pour la répartir ; montant affecté à « Autres ».");
                    parts = new() { [Activite.Autres] = montant };
                }
                else parts = (cle?.Methode == MethodeCleRepartition.Manuelle ? CalculsRentabilite.RepartirManuel(montant, bases) : CalculsRentabilite.Repartir(montant, bases))
                    .Where(p => p.Value != 0).ToDictionary(p => p.Key, p => p.Value);
            }
            foreach (var (a, m) in parts)
            {
                if (activite is null) acts[a].ChargesReparties += m; else acts[a].ChargesDirectes += m;
                if (fixe) acts[a].ChargesFixes += m;
            }
            reparties.Add(new RepartitionChargeDto(source, centre, activite is null ? cle?.Libelle ?? "Prorata du CA" : "Affectation directe", montant, parts));
        }

        foreach (var g in charges.GroupBy(c => new { c.CategorieId, c.CentreCoutId, c.Fixe }))
        {
            var c0 = g.First();
            var cle = c0.CentreCout!.CleRepartitionId is { } cid ? cles.GetValueOrDefault(cid) : null;
            Affecter(c0.Categorie!.Libelle, c0.CentreCout.Libelle, c0.CentreCout.Activite, cle, g.Sum(c => c.MontantHt), g.Key.Fixe);
        }
        var cleCa = cles.Values.FirstOrDefault(c => c.Methode == MethodeCleRepartition.ChiffreAffaires);
        var financiers = await db.MouvementsFinanciers.AsNoTracking().Where(m => m.DateOperation >= du && m.DateOperation <= au
                && (m.Nature == NatureMouvementFinancier.FraisBancaires || m.Nature == NatureMouvementFinancier.EcartCaisse || m.Nature == NatureMouvementFinancier.ContrePassation))
            .ToListAsync(ct);
        var annules = financiers.Where(m => m.MouvementAnnuleId.HasValue).Select(m => m.MouvementAnnuleId!.Value).ToList();
        var naturesAnnulees = await db.MouvementsFinanciers.AsNoTracking().Where(m => annules.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Nature, ct);
        NatureMouvementFinancier NatureEffective(MouvementFinancier m) => m.Nature == NatureMouvementFinancier.ContrePassation && m.MouvementAnnuleId is { } o ? naturesAnnulees.GetValueOrDefault(o) : m.Nature;
        Affecter("Frais bancaires et commissions", "Banque", null, cleCa, -financiers.Where(m => NatureEffective(m) == NatureMouvementFinancier.FraisBancaires).Sum(m => m.Montant), false);
        Affecter("Écarts de caisse (net)", "Caisse", null, cleCa, -financiers.Where(m => NatureEffective(m) == NatureMouvementFinancier.EcartCaisse).Sum(m => m.Montant), false);
        var ajustements = await db.MouvementsStock.AsNoTracking()
            .Where(m => m.Horodatage >= debut && m.Horodatage < fin && (m.Type == TypeMouvementStock.Ajustement || m.Type == TypeMouvementStock.AjustementValeur))
            .GroupBy(m => m.ProduitId).Select(g => new { g.Key, V = g.Sum(m => m.Valeur) }).ToListAsync(ct);
        foreach (var aj in ajustements) Affecter("Ajustements de stock (pertes et régularisations)", "Stock", ActiviteProduit(aj.Key, false), null, -aj.V, false);

        if (au >= horloge.Aujourdhui) { incomplet = true; avert.Add("La période n'est pas terminée : les montants évolueront."); }
        return new AgregatsPeriode(du, au, acts, reparties, avert, incomplet);
    }

    private static RatioDto R(Ratio r) => new(r.Valeur, r.Raison);

    private static LigneRentabiliteDto Ligne(Activite? a, string libelle, string unite, decimal q, decimal ca, decimal cpv, decimal frais, decimal cd, decimal cr, decimal caTotal)
    {
        var m = CalculsRentabilite.Marges(ca, cpv, unite == "mixte" ? 0 : q);
        var contribution = m.MargeBrute - frais;
        return new LigneRentabiliteDto(a ?? Activite.Autres, libelle, q, unite, ca, cpv, m.MargeBrute, R(m.MargeUnitaire), R(m.TauxMarque), R(m.TauxMarge), frais, contribution,
            cd, cr, contribution - cd - cr, caTotal > 0 ? new RatioDto(Arrondi.Pourcentage(ca / caTotal * 100), null) : new RatioDto(null, "Chiffre d'affaires total nul."));
    }

    public async Task<RentabiliteDto> RentabiliteAsync(DateOnly du, DateOnly au, CancellationToken ct)
    {
        Garde.Permission(courant, P.RentabiliteConsulter);
        var ag = await AgregerAsync(du, au, ct);
        var caTotal = ag.Activites.Values.Sum(a => a.ChiffreAffairesHt);
        var lignes = ag.Activites.Values.Where(a => a.ChiffreAffairesHt != 0 || a.CoutDesVentes != 0 || a.ChargesDirectes != 0 || a.ChargesReparties != 0)
            .OrderBy(a => a.Activite)
            .Select(a => Ligne(a.Activite, Libelle(a.Activite), EnLitres(a.Activite) ? "L" : "unité", a.Quantite, a.ChiffreAffairesHt, a.CoutDesVentes, a.FraisDirects,
                a.ChargesDirectes, a.ChargesReparties, caTotal)).ToList();
        var t = ag.Activites.Values;
        var total = Ligne(null, "Total", "mixte", 0, caTotal, t.Sum(a => a.CoutDesVentes), t.Sum(a => a.FraisDirects), t.Sum(a => a.ChargesDirectes),
            t.Sum(a => a.ChargesReparties), caTotal);
        return new RentabiliteDto(du, au, lignes, total, ag.Charges, ag.Incomplet, ag.Avertissements, Methode);
    }

    /// <summary>R-36 sur l'ensemble de la station : charges fixes / taux de marge sur coûts variables.</summary>
    public async Task<SeuilRentabiliteDto> SeuilAsync(DateOnly du, DateOnly au, CancellationToken ct)
    {
        Garde.Permission(courant, P.RentabiliteConsulter);
        var ag = await AgregerAsync(du, au, ct);
        var t = ag.Activites.Values;
        var ca = t.Sum(a => a.ChiffreAffairesHt);
        var fixes = t.Sum(a => a.ChargesFixes);
        var variables = t.Sum(a => a.CoutDesVentes + a.FraisDirects + a.ChargesDirectes + a.ChargesReparties) - fixes;
        var carburant = t.Where(a => EnLitres(a.Activite)).ToList();
        var volume = carburant.Sum(a => a.Quantite);
        decimal? prixMoyen = volume > 0 ? carburant.Sum(a => a.ChiffreAffairesHt) / volume : null;
        var s = CalculsRentabilite.Seuil(fixes, ca, variables, prixMoyen);
        var progression = s.SeuilChiffreAffaires.Valeur is > 0 ? new RatioDto(Arrondi.Pourcentage(ca / s.SeuilChiffreAffaires.Valeur.Value * 100), null)
            : new RatioDto(null, s.SeuilChiffreAffaires.Raison);
        return new SeuilRentabiliteDto(du, au, ca, variables, fixes, R(s.TauxMcv), R(s.SeuilChiffreAffaires), R(s.SeuilVolume), progression,
            "Charges fixes = charges marquées fixes. Coûts variables = coût des ventes + frais directs + charges variables. Le volume équivalent suppose " +
            "un prix moyen HT carburant constant (indicatif : les activités ne sont pas homogènes).");
    }

    /// <summary>R-37 : simulation sur une copie en mémoire ; aucune écriture en base.</summary>
    public async Task<SimulationDto> SimulerAsync(SimulationDemandeDto dto, CancellationToken ct)
    {
        Garde.Permission(courant, P.RentabiliteSimuler);
        var ag = await AgregerAsync(dto.Du, dto.Au, ct);
        var lignes = new List<LigneSimulationDto>();
        decimal contributionAvant = 0, contributionApres = 0;
        foreach (var a in ag.Activites.Values)
        {
            var h = dto.Hypotheses.FirstOrDefault(x => x.Activite == a.Activite);
            if (h is not null && h.VariationVolumePct < -100) throw new ValidationException("Une baisse de volume ne peut pas dépasser 100 %.");
            var q2 = h is null ? a.Quantite : Arrondi.Volume(a.Quantite * (1 + h.VariationVolumePct / 100));
            var facteur = a.Quantite > 0 ? q2 / a.Quantite : 1;
            var ca2 = Arrondi.Montant(a.ChiffreAffairesHt * facteur + (h?.VariationPrixVenteHt ?? 0) * q2);
            var cpv2 = Arrondi.Montant(a.CoutDesVentes * facteur + (h?.VariationPrixAchatHt ?? 0) * q2);
            var frais2 = Arrondi.Montant(a.FraisDirects * facteur);
            contributionAvant += a.Contribution;
            contributionApres += ca2 - cpv2 - frais2;
            if (a.ChiffreAffairesHt != 0 || h is not null)
                lignes.Add(new LigneSimulationDto(a.Activite, Libelle(a.Activite), a.Quantite, q2, a.MargeBrute, ca2 - cpv2, a.Contribution, ca2 - cpv2 - frais2));
        }
        var charges = ag.Activites.Values.Sum(a => a.ChargesDirectes + a.ChargesReparties);
        var fixes = ag.Activites.Values.Sum(a => a.ChargesFixes);
        var charges2 = fixes + dto.VariationChargesFixes + Arrondi.Montant((charges - fixes) * (1 + dto.VariationChargesVariablesPct / 100));
        var avant = contributionAvant - charges;
        var apres = contributionApres - charges2;
        return new SimulationDto(dto.Du, dto.Au, lignes, avant, apres, apres - avant,
            "Simulation calculée sur les agrégats de la période de référence, sans aucune écriture. Prix moyens et coûts unitaires supposés constants hors variations saisies.");
    }
}
