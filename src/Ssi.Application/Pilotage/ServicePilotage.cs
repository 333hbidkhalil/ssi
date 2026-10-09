using Microsoft.EntityFrameworkCore;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Securite;
using Ssi.Application.Stocks;
using Ssi.Domain.Entites;

namespace Ssi.Application.Pilotage;

/// <summary>
/// Tableau de bord, alertes et contrôles de cohérence. Les alertes décrivent des faits vérifiables (écart, retard, seuil) :
/// elles ne qualifient jamais une anomalie de fraude ou de vol.
/// </summary>
public class ServicePilotage(ISsiDb db, IHorloge horloge, IUtilisateurCourant courant, Parametres parametres, ServiceRentabilite rentabilite,
    ServiceStocks stocks, ServiceTresorerie tresorerie, Soldes soldes)
{
    public async Task<TableauBordDto> TableauBordAsync(CancellationToken ct)
    {
        var aujourdhui = horloge.Aujourdhui;
        var debutMois = new DateOnly(aujourdhui.Year, aujourdhui.Month, 1);
        var voirMarges = courant.APermission(P.RentabiliteConsulter);
        var jour = await IndicateursAsync(aujourdhui, aujourdhui, voirMarges, ct);
        var mois = await IndicateursAsync(debutMois, aujourdhui, voirMarges, ct);
        var voirTresorerie = courant.APermission(P.BanqueConsulter) || courant.APermission(P.TableauBordConsulter);
        var situation = await tresorerie.SituationAsync(ct);
        var evolution = await EvolutionAsync(aujourdhui.AddDays(-29), aujourdhui, ct);
        var postesOuverts = await db.Postes.CountAsync(p => p.Statut == StatutPoste.Ouvert, ct);
        var postesAValider = await db.Postes.CountAsync(p => p.Statut == StatutPoste.Cloture, ct);
        return new TableauBordDto(horloge.Maintenant, jour, mois, situation.Disponible, situation.EnAttente, situation.CreancesClients + situation.BonsNonFactures,
            situation.DettesFournisseurs + situation.ChargesAPayer, situation.Previsions.FirstOrDefault(p => p.Horizon == 30)?.TresorerieProjetee,
            await stocks.EtatCuvesAsync(ct), evolution, await AlertesAsync(ct), postesOuverts, postesAValider, !voirMarges);
    }

    private async Task<IndicateurPeriodeDto> IndicateursAsync(DateOnly du, DateOnly au, bool voirMarges, CancellationToken ct)
    {
        var ag = await rentabilite.AgregerAsync(du, au, ct);
        var a = ag.Activites;
        return new IndicateurPeriodeDto(a.Values.Sum(x => x.ChiffreAffairesHt), voirMarges ? a.Values.Sum(x => x.MargeBrute) : 0,
            a[Activite.GasoilStation].Quantite, a[Activite.SansPlombStation].Quantite, a[Activite.GasoilVrac].Quantite + a[Activite.SansPlombVrac].Quantite,
            a[Activite.Lubrifiants].ChiffreAffairesHt + a[Activite.Autres].ChiffreAffairesHt);
    }

    /// <summary>CA HT et volumes carburant par jour (relevés validés, ventes d'articles, factures vrac).</summary>
    private async Task<List<PointJourDto>> EvolutionAsync(DateOnly du, DateOnly au, CancellationToken ct)
    {
        var station = await db.Releves.AsNoTracking().Where(r => r.Statut == StatutReleve.Valide && r.Poste!.Statut == StatutPoste.Valide
                && r.Poste.JourneeExploitation >= du && r.Poste.JourneeExploitation <= au)
            .GroupBy(r => r.Poste!.JourneeExploitation).Select(g => new { Jour = g.Key, Ht = g.Sum(r => r.MontantHt), V = g.Sum(r => r.Volume) }).ToListAsync(ct);
        var debut = horloge.DebutJournee(du);
        var ventes = (await db.VentesArticles.AsNoTracking().Where(v => v.Statut == StatutDocument.Valide && v.Horodatage >= debut).Select(v => new { v.Horodatage, v.TotalHt }).ToListAsync(ct))
            .GroupBy(v => horloge.JourneeDe(v.Horodatage)).ToDictionary(g => g.Key, g => g.Sum(v => v.TotalHt));
        var vrac = await db.Factures.AsNoTracking().Where(f => f.Sens == SensFacture.Client && f.Origine == OrigineFacture.Vrac && f.Statut == StatutDocument.Valide && f.Date >= du && f.Date <= au)
            .GroupBy(f => f.Date).Select(g => new { Jour = g.Key, Ht = g.Sum(f => f.Type == TypeFacture.Facture ? f.TotalHt : -f.TotalHt) }).ToDictionaryAsync(x => x.Jour, x => x.Ht, ct);
        var res = new List<PointJourDto>();
        for (var d = du; d <= au; d = d.AddDays(1))
        {
            var s = station.FirstOrDefault(x => x.Jour == d);
            res.Add(new PointJourDto(d, (s?.Ht ?? 0) + ventes.GetValueOrDefault(d) + vrac.GetValueOrDefault(d), s?.V ?? 0));
        }
        return res;
    }

    public async Task<IReadOnlyList<AlerteDto>> AlertesAsync(CancellationToken ct)
    {
        var a = new List<AlerteDto>();
        var maintenant = horloge.Maintenant;
        var aujourdhui = horloge.Aujourdhui;

        // Stocks
        var joursRupture = await parametres.DecimalAsync(Parametres.JoursRuptureStock, 3m, ct);
        foreach (var c in await stocks.EtatCuvesAsync(ct))
        {
            if (c.StockTheorique < 0)
                a.Add(new("STOCK_NEGATIF", GraviteAlerte.Critique, "Stocks", $"Cuve {c.Code} : stock théorique négatif ({c.StockTheorique:N0} L). Vérifiez les réceptions et les relevés.", "Cuve", c.CuveId));
            else if (c.JoursCouverture is { } j && j < joursRupture)
                a.Add(new("RUPTURE_PROCHE", GraviteAlerte.Critique, "Stocks", $"Cuve {c.Code} : {j:N1} jour(s) de couverture au rythme des 7 derniers jours.", "Cuve", c.CuveId));
            else if (c.SousSeuil)
                a.Add(new("STOCK_BAS", GraviteAlerte.Attention, "Stocks", $"Cuve {c.Code} sous le seuil minimal ({c.StockTheorique:N0} L / {c.SeuilMinimal:N0} L).", "Cuve", c.CuveId));
        }
        foreach (var p in (await stocks.ArticlesAsync(ct)).Where(p => p.SousSeuil))
            a.Add(new("ARTICLE_BAS", GraviteAlerte.Info, "Stocks", $"{p.Designation} : {p.Quantite:N0} en stock (seuil {p.SeuilAlerte:N0}).", "Produit", p.ProduitId));
        foreach (var j in await db.Jaugeages.AsNoTracking().Include(x => x.Cuve).Where(x => x.HorsTolerance && x.Statut == StatutJaugeage.Saisi).ToListAsync(ct))
            a.Add(new("ECART_JAUGEAGE", GraviteAlerte.Attention, "Stocks", $"Cuve {j.Cuve!.Code} : écart de jaugeage de {j.EcartLitres:N0} L hors tolérance, non expliqué.", "Jaugeage", j.Id));
        var ajustements = await db.AjustementsStock.CountAsync(x => x.Statut == StatutDemande.Demande, ct);
        if (ajustements > 0) a.Add(new("AJUSTEMENTS_EN_ATTENTE", GraviteAlerte.Info, "Stocks", $"{ajustements} demande(s) d'ajustement de stock en attente d'autorisation.", null, null));

        // Station
        var seuilPoste = await parametres.DecimalAsync(Parametres.SeuilEcartPoste, 50m, ct);
        var hier = maintenant.AddHours(-24);
        foreach (var p in await db.Postes.AsNoTracking().Where(p => p.Statut != StatutPoste.Valide).ToListAsync(ct))
        {
            if (p.Statut == StatutPoste.Ouvert && p.Debut < hier)
                a.Add(new("POSTE_OUVERT_LONGTEMPS", GraviteAlerte.Attention, "Station", $"Poste {p.Numero} ouvert depuis plus de 24 h.", "PosteTravail", p.Id));
            if (p.Statut == StatutPoste.Cloture && p.ClotureLe < hier)
                a.Add(new("POSTE_NON_VALIDE", GraviteAlerte.Attention, "Station", $"Poste {p.Numero} clôturé mais non validé depuis plus de 24 h : ses ventes ne sont pas comptabilisées.", "PosteTravail", p.Id));
            if (p.Statut == StatutPoste.Cloture && Math.Abs(p.Ecart) > seuilPoste)
                a.Add(new("ECART_POSTE", GraviteAlerte.Attention, "Station", $"Poste {p.Numero} : écart de {p.Ecart:N2} DH entre ventes théoriques et règlements, à expliquer.", "PosteTravail", p.Id));
        }
        var evenements = await db.EvenementsCompteur.CountAsync(e => e.Statut == StatutDemande.Demande, ct);
        if (evenements > 0) a.Add(new("EVENEMENT_COMPTEUR", GraviteAlerte.Attention, "Station", $"{evenements} remise(s) à zéro ou remplacement de compteur à approuver.", null, null));

        // Caisse
        var seuilCaisse = await parametres.DecimalAsync(Parametres.SeuilEcartCaisse, 50m, ct);
        foreach (var s in await db.SessionsCaisse.AsNoTracking().Where(s => s.Statut != StatutSessionCaisse.Validee).ToListAsync(ct))
        {
            if (s.Statut == StatutSessionCaisse.Ouverte && s.Ouverture < hier)
                a.Add(new("CAISSE_OUVERTE_LONGTEMPS", GraviteAlerte.Info, "Caisse", $"Session de caisse {s.Numero} ouverte depuis plus de 24 h.", "SessionCaisse", s.Id));
            if (s.Statut == StatutSessionCaisse.Cloturee && s.Ecart is { } e && Math.Abs(e) > seuilCaisse)
                a.Add(new("ECART_CAISSE", GraviteAlerte.Attention, "Caisse", $"Session {s.Numero} : écart de caisse de {e:N2} DH à expliquer avant validation.", "SessionCaisse", s.Id));
        }

        // Clients, fournisseurs, trésorerie
        var creances = await tresorerie.CreancesAsync(ct);
        var echues = creances.Where(c => c.JoursRetard > 0).ToList();
        if (echues.Count > 0)
            a.Add(new("CREANCES_ECHUES", echues.Any(c => c.JoursRetard > 60) ? GraviteAlerte.Critique : GraviteAlerte.Attention, "Clients",
                $"{echues.Count} facture(s) client échue(s) pour {echues.Sum(c => c.ResteDu):N2} DH (retard maximal {echues.Max(c => c.JoursRetard)} jours).", null, null));
        foreach (var c in await db.Clients.AsNoTracking().Where(c => c.Actif && c.PlafondCredit > 0).ToListAsync(ct))
        {
            var encours = await soldes.CreanceClientAsync(c.Id, ct) + await soldes.BonsNonFacturesAsync(c.Id, ct);
            if (encours > c.PlafondCredit)
                a.Add(new("PLAFOND_DEPASSE", GraviteAlerte.Attention, "Clients", $"{c.Nom} : encours {encours:N2} DH supérieur au plafond de {c.PlafondCredit:N2} DH.", "Client", c.Id));
        }
        var joursEcheance = (int)await parametres.DecimalAsync(Parametres.JoursAlerteEcheance, 7m, ct);
        var dettes = await tresorerie.DettesAsync(ct);
        var proches = dettes.Where(d => d.Echeance <= aujourdhui.AddDays(joursEcheance)).ToList();
        if (proches.Count > 0)
            a.Add(new("ECHEANCES_FOURNISSEURS", proches.Any(d => d.JoursRetard > 0) ? GraviteAlerte.Attention : GraviteAlerte.Info, "Fournisseurs",
                $"{proches.Count} échéance(s) fournisseur ou charge à {joursEcheance} jours ou en retard : {proches.Sum(d => d.ResteDu):N2} DH.", null, null));
        var situation = await tresorerie.SituationAsync(ct);
        var j30 = situation.Previsions.FirstOrDefault(p => p.Horizon == 30);
        if (j30 is not null && j30.TresorerieProjetee < 0)
            a.Add(new("TRESORERIE_TENDUE", GraviteAlerte.Critique, "Trésorerie", $"Trésorerie prévisionnelle à 30 jours négative ({j30.TresorerieProjetee:N2} DH).", null, null));

        // Vrac
        var nonFacturees = await db.LivraisonsDirectes.AsNoTracking().Where(l => l.Statut == StatutLivraison.Validee
            && !db.LignesFacture.Any(x => x.LivraisonId == l.Id && db.Factures.Any(f => f.Id == x.FactureId && f.Sens == SensFacture.Client && f.Type == TypeFacture.Facture && f.Statut != StatutDocument.Annule)))
            .CountAsync(ct);
        if (nonFacturees > 0) a.Add(new("VRAC_NON_FACTURE", GraviteAlerte.Attention, "Vrac", $"{nonFacturees} livraison(s) directe(s) validée(s) non facturée(s) au client.", null, null));

        // Tendances : ventes du jour vs moyenne des 7 jours précédents, charges du mois vs mois précédent
        var baisse = await parametres.DecimalAsync(Parametres.BaisseVentesPct, 20m, ct);
        var ev = await EvolutionAsync(aujourdhui.AddDays(-8), aujourdhui.AddDays(-1), ct);
        var veille = ev.Last().VolumeCarburant;
        var moyenne = ev.Take(7).Average(p => p.VolumeCarburant);
        if (moyenne > 0 && veille < moyenne * (1 - baisse / 100))
            a.Add(new("BAISSE_VENTES", GraviteAlerte.Info, "Station", $"Volume carburant d'hier ({veille:N0} L) inférieur de plus de {baisse:N0} % à la moyenne des 7 jours précédents ({moyenne:N0} L).", null, null));
        var hausse = await parametres.DecimalAsync(Parametres.HausseChargesPct, 20m, ct);
        var debutMois = new DateOnly(aujourdhui.Year, aujourdhui.Month, 1);
        var moisCourant = await db.Charges.Where(c => c.Statut == StatutDocument.Valide && c.Date >= debutMois).SumAsync(c => c.MontantHt, ct);
        var moisPrecedent = await db.Charges.Where(c => c.Statut == StatutDocument.Valide && c.Date >= debutMois.AddMonths(-1) && c.Date < debutMois).SumAsync(c => c.MontantHt, ct);
        if (moisPrecedent > 0 && moisCourant > moisPrecedent * (1 + hausse / 100))
            a.Add(new("HAUSSE_CHARGES", GraviteAlerte.Info, "Charges", $"Charges du mois ({moisCourant:N2} DH) supérieures de plus de {hausse:N0} % au mois précédent ({moisPrecedent:N2} DH).", null, null));

        // Sauvegarde : seule une sauvegarde dont la restauration de contrôle a réussi compte
        var derniere = await db.Sauvegardes.Where(s => s.Statut == "VERIFIEE").MaxAsync(s => (DateTimeOffset?)s.Horodatage, ct);
        if (derniere is null || derniere < maintenant.AddHours(-26))
            a.Add(new("SAUVEGARDE", GraviteAlerte.Critique, "Administration", derniere is null
                ? "Aucune sauvegarde vérifiée n'est enregistrée : les données ne sont pas protégées contre une panne du serveur."
                : $"Dernière sauvegarde vérifiée le {derniere.Value.ToLocalTime():dd/MM/yyyy HH:mm} : plus de 26 heures.", null, null));

        return a.OrderByDescending(x => x.Gravite).ThenBy(x => x.Module).ToList();
    }

    /// <summary>R-38 et R-39 : contrôles de cohérence recalculés depuis les mouvements et les pièces.</summary>
    public async Task<IReadOnlyList<ResultatControleDto>> ControlesAsync(CancellationToken ct)
    {
        var res = new List<ResultatControleDto>();
        var stock = await stocks.ControlerAsync(ct);
        var ko = stock.Where(s => !s.Coherent).ToList();
        res.Add(new("STOCK_POSITIONS", "Positions de stock = somme des mouvements (R-38)", ko.Count == 0, ko.Count,
            ko.Select(s => $"{s.Libelle} — {s.Objet} : position {s.ValeurEnregistree}, mouvements {s.ValeurRecalculee}").ToList()));

        var reglements = await db.Reglements.AsNoTracking().Where(r => r.Affectations.Sum(a => a.Montant) > r.Montant)
            .Select(r => r.Numero).ToListAsync(ct);
        res.Add(new("REGLEMENTS_AFFECTATIONS", "Affectations d'un règlement ≤ son montant (R-39)", reglements.Count == 0, reglements.Count, reglements));

        var factures = await db.Factures.AsNoTracking().Where(f => f.Statut == StatutDocument.Valide && f.Type == TypeFacture.Facture).Select(f => f.Id).ToListAsync(ct);
        var surReglees = (await soldes.FacturesAsync(factures, ct)).Values.Where(s => s.ResteDu < 0).ToList();
        var numeros = await db.Factures.AsNoTracking().Where(f => surReglees.Select(s => s.FactureId).Contains(f.Id)).Select(f => f.Numero).ToListAsync(ct);
        res.Add(new("FACTURES_SUR_REGLEES", "Règlements + avoirs d'une facture ≤ son TTC (R-39)", surReglees.Count == 0, surReglees.Count, numeros));

        var postes = await db.Postes.AsNoTracking().Where(p => p.Statut == StatutPoste.Valide).Select(p => new { p.Id, p.Numero }).ToListAsync(ct);
        var ids = postes.Select(p => p.Id).ToList();
        var volumes = await db.Releves.AsNoTracking().Where(r => ids.Contains(r.PosteId) && r.Statut == StatutReleve.Valide)
            .GroupBy(r => r.PosteId).Select(g => new { g.Key, V = g.Sum(r => r.Volume) }).ToDictionaryAsync(x => x.Key, x => x.V, ct);
        var sorties = await db.MouvementsStock.AsNoTracking().Where(m => m.DocumentType == "POSTE" && m.DocumentId != null && ids.Contains(m.DocumentId.Value))
            .GroupBy(m => m.DocumentId!.Value).Select(g => new { g.Key, V = -g.Sum(m => m.Quantite) }).ToDictionaryAsync(x => x.Key, x => x.V, ct);
        var encaisse = await db.ReglementsPoste.AsNoTracking().Where(r => ids.Contains(r.PosteId) && r.Mode != ModeReglement.Credit)
            .GroupBy(r => r.PosteId).Select(g => new { g.Key, M = g.Sum(r => r.Montant) }).ToDictionaryAsync(x => x.Key, x => x.M, ct);
        var tresoPostes = await db.MouvementsFinanciers.AsNoTracking().Where(m => m.PosteId != null && ids.Contains(m.PosteId.Value))
            .GroupBy(m => m.PosteId!.Value).Select(g => new { g.Key, M = g.Sum(m => m.Montant) }).ToDictionaryAsync(x => x.Key, x => x.M, ct);
        var ecartsStock = postes.Where(p => volumes.GetValueOrDefault(p.Id) != sorties.GetValueOrDefault(p.Id))
            .Select(p => $"{p.Numero} : relevés {volumes.GetValueOrDefault(p.Id):N3} L, sorties de stock {sorties.GetValueOrDefault(p.Id):N3} L").ToList();
        res.Add(new("POSTES_STOCK", "Volumes des postes validés = sorties de stock", ecartsStock.Count == 0, ecartsStock.Count, ecartsStock));
        var ecartsTreso = postes.Where(p => encaisse.GetValueOrDefault(p.Id) != tresoPostes.GetValueOrDefault(p.Id))
            .Select(p => $"{p.Numero} : règlements {encaisse.GetValueOrDefault(p.Id):N2}, mouvements {tresoPostes.GetValueOrDefault(p.Id):N2}").ToList();
        res.Add(new("POSTES_TRESORERIE", "Règlements (hors crédit) des postes validés = mouvements de trésorerie", ecartsTreso.Count == 0, ecartsTreso.Count, ecartsTreso));

        var doubles = await db.MouvementsFinanciers.AsNoTracking().Where(m => m.MouvementAnnuleId != null).GroupBy(m => m.MouvementAnnuleId)
            .Where(g => g.Count() > 1).Select(g => $"Mouvement {g.Key} contre-passé {g.Count()} fois").ToListAsync(ct);
        res.Add(new("CONTRE_PASSATIONS", "Chaque mouvement n'est contre-passé qu'une fois", doubles.Count == 0, doubles.Count, doubles));

        var bons = await db.ReglementsPoste.AsNoTracking().Where(r => r.FactureId != null && !db.Factures.Any(f => f.Id == r.FactureId && f.Statut == StatutDocument.Valide))
            .Select(r => r.Id.ToString()).ToListAsync(ct);
        res.Add(new("BONS_FACTURES", "Bons de crédit facturés rattachés à une facture validée", bons.Count == 0, bons.Count, bons));
        return res;
    }
}
