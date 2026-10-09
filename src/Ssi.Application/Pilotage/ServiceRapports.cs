using Microsoft.EntityFrameworkCore;
using Ssi.Application.Charges;
using Ssi.Application.Commun;
using Ssi.Application.Finance;
using Ssi.Application.Securite;
using Ssi.Application.Stocks;

namespace Ssi.Application.Pilotage;

public sealed record FiltreRapport(DateOnly? Du, DateOnly? Au, Guid? ProduitId, Guid? CompteId);

/// <summary>
/// Rapports tabulaires. R-40 : l'écran et l'export (CSV, Excel, PDF) consomment le même <see cref="RapportDto"/>, produit par une seule requête
/// paramétrée : les lignes et les totaux exportés sont donc identiques à ceux affichés avec les mêmes filtres.
/// </summary>
public class ServiceRapports(ISsiDb db, IHorloge horloge, IUtilisateurCourant courant, ServiceRentabilite rentabilite, ServiceTresorerie tresorerie,
    ServiceStocks stocks, ServiceCharges charges)
{
    public static readonly IReadOnlyDictionary<string, string> Catalogue = new Dictionary<string, string>
    {
        ["ventes"] = "Ventes détaillées (station, articles, livraisons directes)",
        ["rentabilite"] = "Rentabilité par activité",
        ["creances"] = "Créances clients ouvertes",
        ["dettes"] = "Dettes fournisseurs et charges à payer",
        ["stocks"] = "État des cuves",
        ["charges"] = "Charges d'exploitation",
        ["tresorerie"] = "Mouvements d'un compte financier",
    };

    private static ColonneRapportDto T(string cle, string libelle) => new(cle, libelle, "texte");
    private static ColonneRapportDto M(string cle, string libelle) => new(cle, libelle, "montant");
    private static ColonneRapportDto Q(string cle, string libelle) => new(cle, libelle, "quantite");
    private static ColonneRapportDto D(string cle, string libelle) => new(cle, libelle, "date");

    public async Task<RapportDto> GenererAsync(string code, FiltreRapport f, CancellationToken ct)
    {
        var aujourdhui = horloge.Aujourdhui;
        var du = f.Du ?? new DateOnly(aujourdhui.Year, aujourdhui.Month, 1);
        var au = f.Au ?? aujourdhui;
        var filtres = $"Du {du:dd/MM/yyyy} au {au:dd/MM/yyyy}";
        List<ColonneRapportDto> colonnes;
        var lignes = new List<IReadOnlyDictionary<string, object?>>();
        switch (code)
        {
            case "ventes":
            {
                Garde.Permission(courant, P.RapportsConsulter);
                colonnes = [D("date", "Journée"), T("canal", "Canal"), T("piece", "Pièce"), T("produit", "Produit"), Q("quantite", "Quantité"),
                    M("ht", "Montant HT"), M("tva", "TVA"), M("ttc", "Montant TTC")];
                if (f.ProduitId is { } pf) filtres += $" — produit {await db.Produits.Where(p => p.Id == pf).Select(p => p.Designation).FirstOrDefaultAsync(ct)}";
                var releves = await db.Releves.AsNoTracking().Where(r => r.Statut == StatutReleve.Valide && r.Poste!.Statut == StatutPoste.Valide
                        && r.Poste.JourneeExploitation >= du && r.Poste.JourneeExploitation <= au && (f.ProduitId == null || r.Pistolet!.ProduitId == f.ProduitId))
                    .Select(r => new { r.Poste!.JourneeExploitation, r.Poste.Numero, Produit = r.Pistolet!.Produit!.Designation, Pistolet = r.Pistolet.Code, r.Volume, r.MontantHt, r.MontantTtc })
                    .ToListAsync(ct);
                lignes.AddRange(releves.Select(r => Ligne(("date", r.JourneeExploitation), ("canal", "Station"), ("piece", $"{r.Numero} / {r.Pistolet}"), ("produit", r.Produit),
                    ("quantite", r.Volume), ("ht", r.MontantHt), ("tva", r.MontantTtc - r.MontantHt), ("ttc", r.MontantTtc))));
                var (debut, fin) = (horloge.DebutJournee(du), horloge.DebutJournee(au.AddDays(1)));
                var ventes = await db.LignesVenteArticle.AsNoTracking()
                    .Join(db.VentesArticles.AsNoTracking().Where(v => v.Statut == StatutDocument.Valide && v.Horodatage >= debut && v.Horodatage < fin), l => l.VenteId, v => v.Id,
                        (l, v) => new { v.Horodatage, v.Numero, Produit = l.Produit!.Designation, l.ProduitId, l.Quantite, l.MontantHt, l.MontantTva, l.MontantTtc })
                    .Where(x => f.ProduitId == null || x.ProduitId == f.ProduitId).ToListAsync(ct);
                lignes.AddRange(ventes.Select(v => Ligne(("date", horloge.JourneeDe(v.Horodatage)), ("canal", "Articles"), ("piece", v.Numero), ("produit", v.Produit),
                    ("quantite", v.Quantite), ("ht", v.MontantHt), ("tva", v.MontantTva), ("ttc", v.MontantTtc))));
                var vrac = await db.LignesFacture.AsNoTracking()
                    .Join(db.Factures.AsNoTracking().Where(x => x.Sens == SensFacture.Client && x.Origine == OrigineFacture.Vrac && x.Statut == StatutDocument.Valide && x.Date >= du && x.Date <= au),
                        l => l.FactureId, x => x.Id, (l, x) => new { x.Date, x.Numero, x.Type, l.Designation, l.ProduitId, l.Quantite, l.MontantHt, l.MontantTva, l.MontantTtc })
                    .Where(x => f.ProduitId == null || x.ProduitId == f.ProduitId).ToListAsync(ct);
                lignes.AddRange(vrac.Select(v =>
                {
                    var s = v.Type == TypeFacture.Avoir ? -1 : 1;
                    return Ligne(("date", v.Date), ("canal", "Livraison directe"), ("piece", v.Numero), ("produit", v.Designation), ("quantite", s * v.Quantite),
                        ("ht", s * v.MontantHt), ("tva", s * v.MontantTva), ("ttc", s * v.MontantTtc));
                }));
                lignes = lignes.OrderBy(l => (DateOnly)l["date"]!).ThenBy(l => (string)l["canal"]!).ThenBy(l => (string)l["piece"]!).ToList();
                break;
            }
            case "rentabilite":
            {
                var r = await rentabilite.RentabiliteAsync(du, au, ct);
                colonnes = [T("activite", "Activité"), Q("quantite", "Quantité"), T("unite", "Unité"), M("ca", "CA HT"), M("cpv", "Coût des ventes"), M("mb", "Marge brute"),
                    T("tauxMarque", "Taux de marque %"), M("frais", "Frais directs"), M("contribution", "Contribution"), M("charges", "Charges attribuées"), M("resultat", "Résultat estimé")];
                lignes.AddRange(r.Activites.Select(a => Ligne(("activite", a.Libelle), ("quantite", a.Quantite), ("unite", a.Unite), ("ca", a.ChiffreAffairesHt),
                    ("cpv", a.CoutDesVentes), ("mb", a.MargeBrute), ("tauxMarque", a.TauxMarque.Valeur?.ToString("N2") ?? "n.c."), ("frais", a.FraisDirects),
                    ("contribution", a.Contribution), ("charges", a.ChargesDirectes + a.ChargesReparties), ("resultat", a.ResultatEstime))));
                if (r.Incomplet) filtres += " — RÉSULTAT INCOMPLET : " + string.Join(" ", r.Avertissements);
                break;
            }
            case "creances":
            case "dettes":
            {
                Garde.Permission(courant, code == "creances" ? P.ClientsConsulter : P.FournisseursConsulter);
                var e = code == "creances" ? await tresorerie.CreancesAsync(ct) : await tresorerie.DettesAsync(ct);
                filtres = $"Situation au {aujourdhui:dd/MM/yyyy}";
                colonnes = [T("tiers", "Tiers"), T("piece", "Pièce"), T("type", "Type"), D("date", "Date"), D("echeance", "Échéance"), M("montant", "Montant TTC"),
                    M("reste", "Reste dû"), Q("retard", "Jours de retard")];
                lignes.AddRange(e.Select(x => Ligne(("tiers", x.Tiers), ("piece", x.Piece), ("type", x.Type), ("date", x.Date), ("echeance", x.Echeance), ("montant", x.Montant),
                    ("reste", x.ResteDu), ("retard", (decimal)x.JoursRetard))));
                break;
            }
            case "stocks":
            {
                Garde.Permission(courant, P.StockConsulter);
                filtres = $"Situation au {horloge.Maintenant:dd/MM/yyyy HH:mm}";
                colonnes = [T("cuve", "Cuve"), T("produit", "Produit"), Q("capacite", "Capacité L"), Q("stock", "Stock théorique L"), Q("remplissage", "Remplissage %"),
                    Q("couverture", "Jours de couverture"), M("valeur", "Valeur au CMP"), Q("ecart", "Dernier écart de jaugeage L")];
                lignes.AddRange((await stocks.EtatCuvesAsync(ct)).Select(c => Ligne(("cuve", c.Code), ("produit", c.Produit), ("capacite", c.Capacite), ("stock", c.StockTheorique),
                    ("remplissage", c.TauxRemplissagePct), ("couverture", c.JoursCouverture), ("valeur", c.Valeur), ("ecart", c.DernierEcartLitres))));
                break;
            }
            case "charges":
            {
                Garde.Permission(courant, P.ChargesConsulter);
                colonnes = [D("date", "Date"), T("piece", "Pièce"), T("tiers", "Tiers"), T("categorie", "Catégorie"), T("centre", "Centre"), M("ht", "HT"), M("tva", "TVA"),
                    M("ttc", "TTC"), M("reste", "Reste dû"), T("statut", "Statut")];
                lignes.AddRange((await charges.ChargesAsync(du, au, null, null, false, ct)).Where(c => c.Statut == StatutDocument.Valide).OrderBy(c => c.Date)
                    .Select(c => Ligne(("date", c.Date), ("piece", c.Numero), ("tiers", c.Tiers), ("categorie", c.Categorie), ("centre", c.CentreCout), ("ht", c.MontantHt),
                        ("tva", c.MontantTva), ("ttc", c.MontantTtc), ("reste", c.ResteDu), ("statut", c.Fixe ? "Fixe" : "Variable"))));
                break;
            }
            case "tresorerie":
            {
                if (f.CompteId is null) throw new ValidationException("Choisissez le compte financier.");
                Garde.Permission(courant, P.BanqueConsulter);
                var compte = await db.ComptesFinanciers.AsNoTracking().Where(c => c.Id == f.CompteId).Select(c => c.Code + " — " + c.Libelle).FirstOrDefaultAsync(ct)
                             ?? throw new NonTrouveException("Compte introuvable.");
                filtres += $" — compte {compte}";
                var ouverture = await db.MouvementsFinanciers.Where(m => m.CompteId == f.CompteId && m.DateOperation < du).SumAsync(m => m.Montant, ct);
                colonnes = [D("date", "Date"), T("libelle", "Libellé"), T("piece", "Pièce"), M("entree", "Entrée"), M("sortie", "Sortie"), M("solde", "Solde")];
                var solde = ouverture;
                lignes.Add(Ligne(("date", du), ("libelle", "Solde d'ouverture"), ("piece", null), ("entree", 0m), ("sortie", 0m), ("solde", ouverture)));
                foreach (var m in await db.MouvementsFinanciers.AsNoTracking().Where(m => m.CompteId == f.CompteId && m.DateOperation >= du && m.DateOperation <= au)
                             .OrderBy(m => m.DateOperation).ThenBy(m => m.Id).ToListAsync(ct))
                {
                    solde += m.Montant;
                    lignes.Add(Ligne(("date", m.DateOperation), ("libelle", m.Libelle), ("piece", m.DocumentRef), ("entree", m.Montant > 0 ? m.Montant : 0m),
                        ("sortie", m.Montant < 0 ? -m.Montant : 0m), ("solde", solde)));
                }
                var totaux = new Dictionary<string, decimal>
                {
                    ["entree"] = lignes.Sum(l => (decimal)l["entree"]!), ["sortie"] = lignes.Sum(l => (decimal)l["sortie"]!), ["solde"] = solde
                };
                return new RapportDto(code, Catalogue[code], filtres, colonnes, lignes, totaux, horloge.Maintenant, courant.Login ?? "?");
            }
            default:
                throw new NonTrouveException($"Rapport « {code} » inconnu.");
        }
        // Totaux calculés sur les lignes mêmes du rapport (aucune seconde requête).
        var tot = colonnes.Where(c => c.Type == "montant" || (c.Type == "quantite" && code is "ventes" or "rentabilite"))
            .ToDictionary(c => c.Cle, c => lignes.Sum(l => l[c.Cle] is decimal d ? d : 0m));
        return new RapportDto(code, Catalogue[code], filtres, colonnes, lignes, tot, horloge.Maintenant, courant.Login ?? "?");
    }

    private static Dictionary<string, object?> Ligne(params (string Cle, object? Valeur)[] valeurs) => valeurs.ToDictionary(v => v.Cle, v => v.Valeur);
}
