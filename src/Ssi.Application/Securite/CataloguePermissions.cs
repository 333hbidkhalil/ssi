namespace Ssi.Application.Securite;

public sealed record DefinitionPermission(string Code, string Module, string Libelle, bool Sensible);

public sealed record DefinitionRole(string Code, string Libelle, IReadOnlyList<string> Permissions);

/// <summary>Catalogue des permissions atomiques (module.action) et des rôles par défaut.</summary>
public static class P
{
    // Actions standard
    public const string Consulter = "consulter", Creer = "creer", Modifier = "modifier", Valider = "valider",
        Annuler = "annuler", Exporter = "exporter", Sensible = "sensible";

    public const string AdminUtilisateurs = "admin.utilisateurs";
    public const string AdminRoles = "admin.roles";
    public const string AdminParametres = "admin.parametres";
    public const string AdminSauvegardes = "admin.sauvegardes";
    public const string AuditConsulter = "audit.consulter";

    public const string CatalogueConsulter = "catalogue.consulter", CatalogueCreer = "catalogue.creer", CatalogueModifier = "catalogue.modifier";
    public const string PrixConsulter = "prix.consulter", PrixValider = "prix.valider";

    public const string StationConsulter = "station.consulter", StationCreer = "station.creer", StationModifier = "station.modifier",
        StationValider = "station.valider", StationAnnuler = "station.annuler", StationCorrigerIndex = "station.corriger_index",
        StationRouvrir = "station.rouvrir", StationParametrer = "station.parametrer", StationExporter = "station.exporter";

    public const string StockConsulter = "stock.consulter", StockCreer = "stock.creer", StockAjuster = "stock.ajuster",
        StockApprouverAjustement = "stock.approuver_ajustement", StockSensible = "stock.sensible", StockExporter = "stock.exporter";

    public const string AchatsConsulter = "achats.consulter", AchatsCreer = "achats.creer", AchatsValider = "achats.valider",
        AchatsAnnuler = "achats.annuler", AchatsSensible = "achats.sensible";

    public const string VracConsulter = "vrac.consulter", VracCreer = "vrac.creer", VracValider = "vrac.valider",
        VracAnnuler = "vrac.annuler", VracDerogation = "vrac.derogation", VracSensible = "vrac.sensible";

    public const string VentesConsulter = "ventes.consulter", VentesCreer = "ventes.creer", VentesAnnuler = "ventes.annuler";

    public const string ClientsConsulter = "clients.consulter", ClientsCreer = "clients.creer", ClientsModifier = "clients.modifier";
    public const string FournisseursConsulter = "fournisseurs.consulter", FournisseursCreer = "fournisseurs.creer", FournisseursModifier = "fournisseurs.modifier";

    public const string FacturesConsulter = "factures.consulter", FacturesCreer = "factures.creer", FacturesValider = "factures.valider",
        FacturesAnnuler = "factures.annuler", FacturesExporter = "factures.exporter";

    public const string ReglementsConsulter = "reglements.consulter", ReglementsCreer = "reglements.creer", ReglementsAnnuler = "reglements.annuler";

    public const string CaisseConsulter = "caisse.consulter", CaisseCreer = "caisse.creer", CaisseValider = "caisse.valider", CaisseRouvrir = "caisse.rouvrir";
    public const string BanqueConsulter = "banque.consulter", BanqueCreer = "banque.creer", BanqueRapprocher = "banque.rapprocher";

    public const string ChargesConsulter = "charges.consulter", ChargesCreer = "charges.creer", ChargesAnnuler = "charges.annuler",
        ChargesParametrer = "charges.parametrer";

    public const string PersonnelConsulter = "personnel.consulter", PersonnelCreer = "personnel.creer", PersonnelModifier = "personnel.modifier",
        PersonnelSensible = "personnel.sensible";

    public const string RentabiliteConsulter = "rentabilite.consulter", RentabiliteSimuler = "rentabilite.simuler",
        RentabiliteObjectifs = "rentabilite.objectifs";
    public const string TableauBordConsulter = "tableau_bord.consulter";
    public const string RapportsConsulter = "rapports.consulter", RapportsExporter = "rapports.exporter";

    public static readonly IReadOnlyList<DefinitionPermission> Toutes =
    [
        new(AdminUtilisateurs, "Administration", "Gérer les utilisateurs", true),
        new(AdminRoles, "Administration", "Gérer les rôles et permissions", true),
        new(AdminParametres, "Administration", "Modifier les paramètres financiers et fiscaux", true),
        new(AdminSauvegardes, "Administration", "Gérer les sauvegardes", true),
        new(AuditConsulter, "Administration", "Consulter le journal d'audit", true),

        new(CatalogueConsulter, "Catalogue", "Consulter les produits", false),
        new(CatalogueCreer, "Catalogue", "Créer des produits", false),
        new(CatalogueModifier, "Catalogue", "Modifier des produits", false),
        new(PrixConsulter, "Prix", "Consulter les prix de vente", false),
        new(PrixValider, "Prix", "Valider un changement de prix", true),

        new(StationConsulter, "Station", "Consulter postes et relevés", false),
        new(StationCreer, "Station", "Ouvrir un poste, saisir les relevés et règlements", false),
        new(StationModifier, "Station", "Modifier un relevé non validé", false),
        new(StationValider, "Station", "Clôturer et valider un poste", false),
        new(StationAnnuler, "Station", "Annuler un relevé", false),
        new(StationCorrigerIndex, "Station", "Corriger un index / remise à zéro de compteur", true),
        new(StationRouvrir, "Station", "Rouvrir un poste validé", true),
        new(StationParametrer, "Station", "Paramétrer cuves, volucompteurs et pistolets", false),
        new(StationExporter, "Station", "Exporter les ventes station", false),

        new(StockConsulter, "Stocks", "Consulter les stocks", false),
        new(StockCreer, "Stocks", "Saisir jaugeages et mouvements", false),
        new(StockAjuster, "Stocks", "Demander un ajustement de stock", false),
        new(StockApprouverAjustement, "Stocks", "Approuver un ajustement de stock", true),
        new(StockSensible, "Stocks", "Voir les valeurs et coûts de stock", true),
        new(StockExporter, "Stocks", "Exporter les états de stock", false),

        new(AchatsConsulter, "Achats", "Consulter les achats", false),
        new(AchatsCreer, "Achats", "Créer commandes et réceptions", false),
        new(AchatsValider, "Achats", "Valider réceptions et factures fournisseurs", false),
        new(AchatsAnnuler, "Achats", "Annuler un document d'achat", false),
        new(AchatsSensible, "Achats", "Voir les prix d'achat", true),

        new(VracConsulter, "Vrac", "Consulter les livraisons directes", false),
        new(VracCreer, "Vrac", "Créer commandes et livraisons", false),
        new(VracValider, "Vrac", "Confirmer commandes et valider livraisons", false),
        new(VracAnnuler, "Vrac", "Annuler commandes et livraisons", false),
        new(VracDerogation, "Vrac", "Déroger au seuil de marge", true),
        new(VracSensible, "Vrac", "Voir prix d'achat et marges vrac", true),

        new(VentesConsulter, "Ventes", "Consulter les ventes d'articles", false),
        new(VentesCreer, "Ventes", "Enregistrer des ventes d'articles", false),
        new(VentesAnnuler, "Ventes", "Annuler une vente", false),

        new(ClientsConsulter, "Clients", "Consulter les clients", false),
        new(ClientsCreer, "Clients", "Créer des clients", false),
        new(ClientsModifier, "Clients", "Modifier des clients (dont plafond de crédit)", false),
        new(FournisseursConsulter, "Fournisseurs", "Consulter les fournisseurs", false),
        new(FournisseursCreer, "Fournisseurs", "Créer des fournisseurs", false),
        new(FournisseursModifier, "Fournisseurs", "Modifier des fournisseurs", false),

        new(FacturesConsulter, "Factures", "Consulter les factures", false),
        new(FacturesCreer, "Factures", "Créer des factures et avoirs", false),
        new(FacturesValider, "Factures", "Valider des factures", false),
        new(FacturesAnnuler, "Factures", "Annuler une facture", false),
        new(FacturesExporter, "Factures", "Exporter les factures", false),

        new(ReglementsConsulter, "Règlements", "Consulter les règlements", false),
        new(ReglementsCreer, "Règlements", "Enregistrer des règlements", false),
        new(ReglementsAnnuler, "Règlements", "Annuler un règlement", true),

        new(CaisseConsulter, "Caisse", "Consulter les caisses", false),
        new(CaisseCreer, "Caisse", "Ouvrir, clôturer une caisse et saisir des opérations", false),
        new(CaisseValider, "Caisse", "Valider une clôture de caisse", false),
        new(CaisseRouvrir, "Caisse", "Rouvrir une clôture de caisse", true),
        new(BanqueConsulter, "Banque", "Consulter les comptes", false),
        new(BanqueCreer, "Banque", "Enregistrer opérations bancaires et virements", false),
        new(BanqueRapprocher, "Banque", "Effectuer les rapprochements", false),

        new(ChargesConsulter, "Charges", "Consulter les charges", false),
        new(ChargesCreer, "Charges", "Enregistrer des charges", false),
        new(ChargesAnnuler, "Charges", "Annuler une charge", false),
        new(ChargesParametrer, "Charges", "Paramétrer catégories, centres et clés de répartition", true),

        new(PersonnelConsulter, "Personnel", "Consulter les fiches employés", false),
        new(PersonnelCreer, "Personnel", "Créer des employés", false),
        new(PersonnelModifier, "Personnel", "Modifier des employés", false),
        new(PersonnelSensible, "Personnel", "Voir et saisir les rémunérations", true),

        new(RentabiliteConsulter, "Rentabilité", "Consulter marges et rentabilité", true),
        new(RentabiliteSimuler, "Rentabilité", "Utiliser les simulations", true),
        new(RentabiliteObjectifs, "Rentabilité", "Définir les objectifs", true),
        new(TableauBordConsulter, "Tableau de bord", "Consulter le tableau de bord de direction", true),
        new(RapportsConsulter, "Rapports", "Consulter les rapports", false),
        new(RapportsExporter, "Rapports", "Exporter les rapports", false),
    ];

    private static string[] Lecture(params string[] modules) =>
        Toutes.Where(p => modules.Contains(p.Module) && p.Code.EndsWith(".consulter")).Select(p => p.Code).ToArray();

    private static string[] Module(params string[] modules) =>
        Toutes.Where(p => modules.Contains(p.Module)).Select(p => p.Code).ToArray();

    public static readonly IReadOnlyList<DefinitionRole> RolesParDefaut =
    [
        new("ADMIN", "Administrateur", Toutes.Select(p => p.Code).ToArray()),
        new("DIRECTEUR", "Propriétaire / directeur", Toutes.Select(p => p.Code).ToArray()),
        new("RESP_STATION", "Responsable de station",
            [.. Module("Station"), .. Lecture("Catalogue", "Prix", "Stocks", "Clients", "Caisse", "Personnel", "Ventes"),
             StockCreer, StockAjuster, VentesCreer, RapportsConsulter]),
        new("RESP_CAISSE", "Responsable de caisse",
            [.. Module("Caisse", "Règlements", "Ventes"), StationConsulter, StationValider, ClientsConsulter, FacturesConsulter,
             BanqueConsulter, BanqueCreer, CatalogueConsulter, PrixConsulter, RapportsConsulter]),
        new("POMPISTE", "Pompiste",
            [StationConsulter, StationCreer, VentesConsulter, VentesCreer, CatalogueConsulter, PrixConsulter, ClientsConsulter]),
        new("RESP_ACHATS", "Responsable des achats",
            [.. Module("Achats", "Fournisseurs"), .. Lecture("Catalogue", "Prix", "Stocks", "Vrac", "Factures"), FacturesCreer, RapportsConsulter]),
        new("RESP_STOCKS", "Responsable des stocks",
            [.. Module("Stocks", "Catalogue"), .. Lecture("Achats", "Station", "Prix"), RapportsConsulter]),
        new("RESP_COMMERCIAL", "Responsable commercial",
            [.. Module("Vrac", "Clients"), .. Lecture("Catalogue", "Prix", "Ventes", "Fournisseurs", "Règlements"),
             FacturesConsulter, FacturesCreer, FacturesValider, RapportsConsulter]),
        new("COMPTABLE", "Comptable / gestionnaire financier",
            [.. Module("Factures", "Règlements", "Banque", "Charges", "Rentabilité", "Rapports", "Tableau de bord"),
             .. Lecture("Caisse", "Clients", "Fournisseurs", "Achats", "Vrac", "Stocks", "Station", "Personnel", "Catalogue", "Prix", "Ventes"),
             PersonnelSensible, AchatsSensible, VracSensible, StockSensible]),
        new("AUDITEUR", "Auditeur (lecture seule)",
            [.. Toutes.Where(p => p.Code.EndsWith(".consulter") || p.Code.EndsWith(".exporter") || p.Code.EndsWith(".sensible")).Select(p => p.Code),
             AuditConsulter, TableauBordConsulter, RapportsConsulter]),
    ];
}
