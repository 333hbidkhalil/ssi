using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ssi.Application.Commun;
using Ssi.Application.Securite;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Infrastructure.Persistance;

/// <summary>
/// Applique les migrations et charge les données de référence manquantes (idempotent).
/// Les taux fiscaux chargés sont des valeurs datées À VALIDER par un professionnel (hypothèse H-03).
/// </summary>
public class Initialisation(SsiDbContext db, IHachageMotDePasse hachage, ILogger<Initialisation> log)
{
    public async Task ExecuterAsync(string motDePasseAdminInitial, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await PermissionsEtRolesAsync(ct);
        await AdministrateurAsync(motDePasseAdminInitial, ct);
        await ReferentielsAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task PermissionsEtRolesAsync(CancellationToken ct)
    {
        var existantes = await db.Permissions.ToDictionaryAsync(p => p.Code, ct);
        foreach (var d in P.Toutes)
        {
            if (existantes.TryGetValue(d.Code, out var p)) { p.Libelle = d.Libelle; p.Module = d.Module; p.Sensible = d.Sensible; }
            else db.Permissions.Add(new Permission { Code = d.Code, Module = d.Module, Libelle = d.Libelle, Sensible = d.Sensible });
        }
        await db.SaveChangesAsync(ct);

        foreach (var r in P.RolesParDefaut)
        {
            var role = await db.Roles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Code == r.Code, ct);
            if (role is null)
            {
                role = new Role { Code = r.Code, Libelle = r.Libelle, Systeme = true };
                db.Roles.Add(role);
                foreach (var code in r.Permissions.Distinct())
                    role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
            }
            else if (r.Code is "ADMIN")
            {
                // L'administrateur reçoit toujours toutes les permissions, y compris les nouvelles.
                foreach (var code in r.Permissions.Distinct().Where(c => role.Permissions.All(x => x.PermissionCode != c)))
                    role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task AdministrateurAsync(string motDePasse, CancellationToken ct)
    {
        if (await db.Utilisateurs.AnyAsync(ct)) return;
        var admin = new Utilisateur
        {
            Login = "admin", NomComplet = "Administrateur SSI", HashMotDePasse = hachage.Hacher(motDePasse),
            DoitChangerMotDePasse = true
        };
        var role = await db.Roles.FirstAsync(r => r.Code == "ADMIN", ct);
        admin.Roles.Add(new UtilisateurRole { UtilisateurId = admin.Id, RoleId = role.Id });
        db.Utilisateurs.Add(admin);
        log.LogWarning("Compte 'admin' créé : changez le mot de passe initial à la première connexion.");
        await db.SaveChangesAsync(ct);
    }

    private async Task ReferentielsAsync(CancellationToken ct)
    {
        foreach (var p in Parametres.ParDefaut)
            if (!await db.Parametres.AnyAsync(x => x.Cle == p.Cle, ct))
                db.Parametres.Add(new Parametre { Cle = p.Cle, Valeur = p.Valeur, Type = p.Type, Description = p.Description });

        if (!await db.Societes.AnyAsync(ct))
            db.Societes.Add(new Societe { RaisonSociale = "À paramétrer", Ville = "", MentionsLegales = "Mentions légales à valider par un professionnel avant mise en production." });

        if (!await db.Taxes.AnyAsync(ct))
        {
            var debut = new DateOnly(2020, 1, 1);
            var tva10 = new Taxe { Code = "TVA_CARBURANT", Libelle = "TVA carburants" };
            tva10.Taux.Add(new TauxTaxe { TaxeId = tva10.Id, Taux = 0.10m, DateDebut = debut, Commentaire = "Valeur initiale À VALIDER par le comptable (H-03)" });
            var tva20 = new Taxe { Code = "TVA_NORMAL", Libelle = "TVA taux normal" };
            tva20.Taux.Add(new TauxTaxe { TaxeId = tva20.Id, Taux = 0.20m, DateDebut = debut, Commentaire = "Valeur initiale À VALIDER par le comptable (H-03)" });
            var exo = new Taxe { Code = "EXONERE", Libelle = "Exonéré / hors champ" };
            exo.Taux.Add(new TauxTaxe { TaxeId = exo.Id, Taux = 0m, DateDebut = debut });
            db.Taxes.AddRange(tva10, tva20, exo);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.FamillesProduits.AnyAsync(ct))
        {
            var carb = new FamilleProduit { Code = "CARB", Libelle = "Carburants", Categorie = CategorieProduit.Carburant };
            var lub = new FamilleProduit { Code = "LUB", Libelle = "Lubrifiants et huiles moteur", Categorie = CategorieProduit.Lubrifiant };
            var ann = new FamilleProduit { Code = "ANN", Libelle = "Produits annexes", Categorie = CategorieProduit.Annexe };
            db.FamillesProduits.AddRange(carb, lub, ann);
            var tvaCarb = await db.Taxes.FirstAsync(t => t.Code == "TVA_CARBURANT", ct);
            db.Produits.AddRange(
                new Produit { Code = "GO", Designation = "Gasoil", FamilleId = carb.Id, Unite = "L", TaxeId = tvaCarb.Id, TypeCarburant = TypeCarburant.Gasoil },
                new Produit { Code = "SP", Designation = "Essence sans plomb", FamilleId = carb.Id, Unite = "L", TaxeId = tvaCarb.Id, TypeCarburant = TypeCarburant.SansPlomb });
        }

        if (!await db.ComptesFinanciers.AnyAsync(ct))
            db.ComptesFinanciers.AddRange(
                new CompteFinancier { Code = "CAISSE-PRINC", Libelle = "Caisse principale", Type = TypeCompteFinancier.Caisse },
                new CompteFinancier { Code = "TPE", Libelle = "Paiements carte à encaisser (TPE)", Type = TypeCompteFinancier.AttenteCarte },
                new CompteFinancier { Code = "CHEQUES", Libelle = "Chèques à encaisser", Type = TypeCompteFinancier.ChequesAEncaisser });

        if (!await db.CategoriesCharges.AnyAsync(ct))
        {
            (string code, string lib, bool fixe)[] cats =
            [
                ("SALAIRES", "Salaires et charges sociales", true), ("ENERGIE", "Électricité et eau", false),
                ("ENTRETIEN", "Entretien", false), ("REPARATIONS", "Réparations", false), ("ASSURANCES", "Assurances", true),
                ("FRAIS_BANCAIRES", "Frais bancaires", false), ("TELECOM", "Télécommunications", true), ("LOYERS", "Loyers", true),
                ("FOURNITURES", "Fournitures", false), ("TRANSPORT", "Transport", false), ("IMPOTS", "Impôts et taxes", true),
                ("HONORAIRES", "Honoraires", true), ("AUTRES", "Autres charges", false),
            ];
            db.CategoriesCharges.AddRange(cats.Select(c => new CategorieCharge { Code = c.code, Libelle = c.lib, FixeParDefaut = c.fixe }));
        }

        if (!await db.ClesRepartition.AnyAsync(ct))
        {
            var cleCa = new CleRepartition { Code = "CA", Libelle = "Prorata du chiffre d'affaires HT", Methode = MethodeCleRepartition.ChiffreAffaires };
            db.ClesRepartition.AddRange(cleCa,
                new CleRepartition { Code = "VOLUME", Libelle = "Prorata des volumes vendus", Methode = MethodeCleRepartition.Volume },
                new CleRepartition { Code = "MARGE", Libelle = "Prorata de la marge brute", Methode = MethodeCleRepartition.MargeBrute },
                new CleRepartition { Code = "COUT", Libelle = "Prorata du coût direct", Methode = MethodeCleRepartition.CoutDirect });
            db.CentresCout.Add(new CentreCout { Code = "COMMUN", Libelle = "Charges communes de la station", CleRepartitionId = cleCa.Id });
            foreach (var a in Enum.GetValues<Activite>())
                db.CentresCout.Add(new CentreCout { Code = a.ToString().ToUpperInvariant(), Libelle = $"Activité {a}", Activite = a });
        }
    }
}
