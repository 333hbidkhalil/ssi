using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ssi.Application.Commun;
using Ssi.Domain.Commun;
using Ssi.Domain.Entites;

namespace Ssi.Infrastructure.Persistance;

public class SsiDbContext(DbContextOptions<SsiDbContext> options) : DbContext(options), ISsiDb
{
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UtilisateurRole> UtilisateurRoles => Set<UtilisateurRole>();
    public DbSet<UtilisateurPermission> UtilisateurPermissions => Set<UtilisateurPermission>();
    public DbSet<JournalAudit> JournalAudit => Set<JournalAudit>();
    public DbSet<CleIdempotence> ClesIdempotence => Set<CleIdempotence>();
    public DbSet<Societe> Societes => Set<Societe>();
    public DbSet<Parametre> Parametres => Set<Parametre>();
    public DbSet<Taxe> Taxes => Set<Taxe>();
    public DbSet<TauxTaxe> TauxTaxes => Set<TauxTaxe>();
    public DbSet<SequenceDocument> SequencesDocuments => Set<SequenceDocument>();
    public DbSet<Objectif> Objectifs => Set<Objectif>();
    public DbSet<Simulation> Simulations => Set<Simulation>();
    public DbSet<Sauvegarde> Sauvegardes => Set<Sauvegarde>();
    public DbSet<FamilleProduit> FamillesProduits => Set<FamilleProduit>();
    public DbSet<Produit> Produits => Set<Produit>();
    public DbSet<PrixProduit> PrixProduits => Set<PrixProduit>();
    public DbSet<Cuve> Cuves => Set<Cuve>();
    public DbSet<Volucompteur> Volucompteurs => Set<Volucompteur>();
    public DbSet<Pistolet> Pistolets => Set<Pistolet>();
    public DbSet<PosteTravail> Postes => Set<PosteTravail>();
    public DbSet<ReleveIndex> Releves => Set<ReleveIndex>();
    public DbSet<EvenementCompteur> EvenementsCompteur => Set<EvenementCompteur>();
    public DbSet<ReglementPoste> ReglementsPoste => Set<ReglementPoste>();
    public DbSet<MouvementStock> MouvementsStock => Set<MouvementStock>();
    public DbSet<PositionStock> PositionsStock => Set<PositionStock>();
    public DbSet<Jaugeage> Jaugeages => Set<Jaugeage>();
    public DbSet<AjustementStock> AjustementsStock => Set<AjustementStock>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Fournisseur> Fournisseurs => Set<Fournisseur>();
    public DbSet<IncidentFournisseur> IncidentsFournisseur => Set<IncidentFournisseur>();
    public DbSet<Relance> Relances => Set<Relance>();
    public DbSet<PieceJointe> PiecesJointes => Set<PieceJointe>();
    public DbSet<CommandeFournisseur> CommandesFournisseur => Set<CommandeFournisseur>();
    public DbSet<LigneCommandeFournisseur> LignesCommandeFournisseur => Set<LigneCommandeFournisseur>();
    public DbSet<Reception> Receptions => Set<Reception>();
    public DbSet<LigneReception> LignesReception => Set<LigneReception>();
    public DbSet<VenteArticle> VentesArticles => Set<VenteArticle>();
    public DbSet<LigneVenteArticle> LignesVenteArticle => Set<LigneVenteArticle>();
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<LigneFacture> LignesFacture => Set<LigneFacture>();
    public DbSet<CommandeClient> CommandesClient => Set<CommandeClient>();
    public DbSet<LivraisonDirecte> LivraisonsDirectes => Set<LivraisonDirecte>();
    public DbSet<FraisDirect> FraisDirects => Set<FraisDirect>();
    public DbSet<RepartitionFrais> RepartitionsFrais => Set<RepartitionFrais>();
    public DbSet<CompteFinancier> ComptesFinanciers => Set<CompteFinancier>();
    public DbSet<MouvementFinancier> MouvementsFinanciers => Set<MouvementFinancier>();
    public DbSet<Reglement> Reglements => Set<Reglement>();
    public DbSet<AffectationReglement> AffectationsReglement => Set<AffectationReglement>();
    public DbSet<TransfertInterne> TransfertsInternes => Set<TransfertInterne>();
    public DbSet<SessionCaisse> SessionsCaisse => Set<SessionCaisse>();
    public DbSet<RapprochementBancaire> RapprochementsBancaires => Set<RapprochementBancaire>();
    public DbSet<LigneReleveBancaire> LignesReleveBancaire => Set<LigneReleveBancaire>();
    public DbSet<CategorieCharge> CategoriesCharges => Set<CategorieCharge>();
    public DbSet<CentreCout> CentresCout => Set<CentreCout>();
    public DbSet<CleRepartition> ClesRepartition => Set<CleRepartition>();
    public DbSet<CleRepartitionManuelle> ClesRepartitionManuelles => Set<CleRepartitionManuelle>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Employe> Employes => Set<Employe>();
    public DbSet<AffectationEmploye> AffectationsEmploye => Set<AffectationEmploye>();
    public DbSet<ElementRemuneration> ElementsRemuneration => Set<ElementRemuneration>();
    public DbSet<Absence> Absences => Set<Absence>();

    private static readonly Dictionary<Type, string> Tables = new()
    {
        [typeof(Utilisateur)] = "utilisateurs", [typeof(Role)] = "roles", [typeof(Permission)] = "permissions",
        [typeof(RolePermission)] = "role_permissions", [typeof(UtilisateurRole)] = "utilisateur_roles",
        [typeof(UtilisateurPermission)] = "utilisateur_permissions", [typeof(JournalAudit)] = "journal_audit",
        [typeof(CleIdempotence)] = "cles_idempotence", [typeof(Societe)] = "societe", [typeof(Parametre)] = "parametres",
        [typeof(Taxe)] = "taxes", [typeof(TauxTaxe)] = "taux_taxes", [typeof(SequenceDocument)] = "sequences_documents",
        [typeof(Objectif)] = "objectifs", [typeof(Simulation)] = "simulations", [typeof(Sauvegarde)] = "sauvegardes",
        [typeof(FamilleProduit)] = "familles_produits", [typeof(Produit)] = "produits", [typeof(PrixProduit)] = "prix_produits",
        [typeof(Cuve)] = "cuves", [typeof(Volucompteur)] = "volucompteurs", [typeof(Pistolet)] = "pistolets",
        [typeof(PosteTravail)] = "postes_travail", [typeof(ReleveIndex)] = "releves_index",
        [typeof(EvenementCompteur)] = "evenements_compteur", [typeof(ReglementPoste)] = "reglements_poste",
        [typeof(MouvementStock)] = "mouvements_stock", [typeof(PositionStock)] = "positions_stock",
        [typeof(Jaugeage)] = "jaugeages", [typeof(AjustementStock)] = "ajustements_stock",
        [typeof(Client)] = "clients", [typeof(Fournisseur)] = "fournisseurs", [typeof(IncidentFournisseur)] = "incidents_fournisseur",
        [typeof(Relance)] = "relances", [typeof(PieceJointe)] = "pieces_jointes",
        [typeof(CommandeFournisseur)] = "commandes_fournisseur", [typeof(LigneCommandeFournisseur)] = "lignes_commande_fournisseur",
        [typeof(Reception)] = "receptions", [typeof(LigneReception)] = "lignes_reception",
        [typeof(VenteArticle)] = "ventes_articles", [typeof(LigneVenteArticle)] = "lignes_vente_article",
        [typeof(Facture)] = "factures", [typeof(LigneFacture)] = "lignes_facture",
        [typeof(CommandeClient)] = "commandes_client", [typeof(LivraisonDirecte)] = "livraisons_directes",
        [typeof(FraisDirect)] = "frais_directs", [typeof(RepartitionFrais)] = "repartitions_frais",
        [typeof(CompteFinancier)] = "comptes_financiers", [typeof(MouvementFinancier)] = "mouvements_financiers",
        [typeof(Reglement)] = "reglements", [typeof(AffectationReglement)] = "affectations_reglement",
        [typeof(TransfertInterne)] = "transferts_internes", [typeof(SessionCaisse)] = "sessions_caisse",
        [typeof(RapprochementBancaire)] = "rapprochements_bancaires", [typeof(LigneReleveBancaire)] = "lignes_releve_bancaire",
        [typeof(CategorieCharge)] = "categories_charges", [typeof(CentreCout)] = "centres_cout",
        [typeof(CleRepartition)] = "cles_repartition", [typeof(CleRepartitionManuelle)] = "cles_repartition_manuelles",
        [typeof(Charge)] = "charges", [typeof(Employe)] = "employes", [typeof(AffectationEmploye)] = "affectations_employe",
        [typeof(ElementRemuneration)] = "elements_remuneration", [typeof(Absence)] = "absences",
    };

    public async Task VerrouillerAsync(string table, Guid id, CancellationToken ct = default)
    {
        if (!Tables.ContainsValue(table)) throw new ArgumentException("Table inconnue.", nameof(table));
        // Le nom de table provient d'une liste blanche ; l'identifiant est paramétré.
        await Database.ExecuteSqlRawAsync($"SELECT 1 FROM {table} WHERE id = {{0}} FOR UPDATE", [id], ct);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("btree_gist");

        foreach (var (type, table) in Tables) b.Entity(type).ToTable(table);

        // Jeton de concurrence optimiste : xmin
        foreach (var et in b.Model.GetEntityTypes().Where(t => typeof(Entite).IsAssignableFrom(t.ClrType)))
            b.Entity(et.ClrType).Property<uint>(nameof(Entite.Version)).IsRowVersion();

        // Précisions numériques (voir docs/01-architecture.md §4)
        foreach (var p in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            var n = p.Name;
            if (n.StartsWith("Index")) { p.SetPrecision(15); p.SetScale(3); }
            else if (n is "Cmp" or "CmpApres" or "CoutUnitaire") { p.SetPrecision(14); p.SetScale(6); }
            else if (n.StartsWith("Prix") || n == "Prix") { p.SetPrecision(12); p.SetScale(4); }
            else if (n.StartsWith("Taux") || n.EndsWith("Pct") || n == "Pourcentage") { p.SetPrecision(9); p.SetScale(4); }
            else if (n.StartsWith("Quantite") || n.StartsWith("Volume") || n is "Capacite" or "SeuilMinimal" or "StockTheorique"
                     or "EcartLitres" or "ToleranceEcartLitres" or "SeuilAlerte" or "HauteurCm" or "TemperatureC")
            { p.SetPrecision(14); p.SetScale(3); }
            else { p.SetPrecision(18); p.SetScale(2); }
        }

        // Sécurité
        b.Entity<Utilisateur>(e =>
        {
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Login).HasMaxLength(64);
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.UtilisateurId);
            e.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.UtilisateurId);
        });
        b.Entity<Role>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId);
        });
        b.Entity<Permission>().HasKey(x => x.Code);
        b.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionCode });
            e.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionCode);
        });
        b.Entity<UtilisateurRole>(e =>
        {
            e.HasKey(x => new { x.UtilisateurId, x.RoleId });
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId);
        });
        b.Entity<UtilisateurPermission>(e =>
        {
            e.HasKey(x => new { x.UtilisateurId, x.PermissionCode });
            e.HasOne<Permission>().WithMany().HasForeignKey(x => x.PermissionCode);
        });
        b.Entity<JournalAudit>(e =>
        {
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Avant).HasColumnType("jsonb");
            e.Property(x => x.Apres).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ObjetType, x.ObjetId });
            e.HasIndex(x => x.Horodatage);
        });
        b.Entity<CleIdempotence>(e =>
        {
            e.HasKey(x => new { x.Cle, x.UtilisateurId });
            e.Property(x => x.Reponse).HasColumnType("text"); // rejouée à l'octet près
        });

        // Paramètres
        b.Entity<Parametre>().HasKey(x => x.Cle);
        b.Entity<Taxe>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Taux).WithOne().HasForeignKey(x => x.TaxeId);
        });
        b.Entity<SequenceDocument>().HasKey(x => x.TypeDocument);
        b.Entity<Objectif>().HasIndex(x => new { x.Periode, x.Indicateur, x.Activite }).IsUnique().AreNullsDistinct(false);
        b.Entity<Simulation>().Property(x => x.Parametres).HasColumnType("jsonb");
        b.Entity<Sauvegarde>().Property(x => x.Id).UseIdentityAlwaysColumn();

        // Catalogue
        b.Entity<FamilleProduit>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Produit>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Famille).WithMany().HasForeignKey(x => x.FamilleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Taxe).WithMany().HasForeignKey(x => x.TaxeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PrixProduit>(e =>
        {
            e.HasOne<Produit>().WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ProduitId, x.Type, x.Debut });
            e.ToTable(t => t.HasCheckConstraint("ck_prix_positif", "prix > 0"));
        });

        // Station
        b.Entity<Cuve>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Produit).WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_cuve_capacite", "capacite > 0"));
        });
        b.Entity<Volucompteur>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Pistolets).WithOne(x => x.Volucompteur).HasForeignKey(x => x.VolucompteurId);
        });
        b.Entity<Pistolet>(e =>
        {
            e.HasIndex(x => new { x.VolucompteurId, x.Code }).IsUnique();
            e.HasOne(x => x.Produit).WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cuve).WithMany().HasForeignKey(x => x.CuveId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PosteTravail>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasMany(x => x.Releves).WithOne(x => x.Poste).HasForeignKey(x => x.PosteId);
            e.HasMany(x => x.Reglements).WithOne().HasForeignKey(x => x.PosteId);
            // Un seul poste ouvert à la fois
            e.HasIndex(x => x.Statut).HasFilter("statut = 1").IsUnique().HasDatabaseName("ux_poste_unique_ouvert");
        });
        b.Entity<ReleveIndex>(e =>
        {
            e.HasOne(x => x.Pistolet).WithMany().HasForeignKey(x => x.PistoletId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.PosteId, x.PistoletId }).IsUnique().HasFilter("statut <> 3").HasDatabaseName("ux_releve_poste_pistolet");
            e.HasOne<Employe>().WithMany().HasForeignKey(x => x.OperateurId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Anomalies).HasColumnType("jsonb");
            e.ToTable(t => t.HasCheckConstraint("ck_releve_index", "index_fin >= index_debut"));
        });
        b.Entity<EvenementCompteur>().HasOne<Pistolet>().WithMany().HasForeignKey(x => x.PistoletId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReglementPoste>(e =>
        {
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_reglement_poste_credit", "mode <> 5 OR client_id IS NOT NULL");
                t.HasCheckConstraint("ck_reglement_poste_montant", "montant > 0");
            });
        });

        // Stocks
        b.Entity<MouvementStock>(e =>
        {
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.HasOne<Produit>().WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Cuve>().WithMany().HasForeignKey(x => x.CuveId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ProduitId, x.CuveId, x.Horodatage });
            e.HasIndex(x => new { x.DocumentType, x.DocumentId });
            e.ToTable(t => t.HasCheckConstraint("ck_mouvement_non_nul", "quantite <> 0"));
        });
        b.Entity<PositionStock>(e =>
        {
            e.HasIndex(x => new { x.ProduitId, x.CuveId }).IsUnique().AreNullsDistinct(false);
        });
        b.Entity<Jaugeage>().HasOne(x => x.Cuve).WithMany().HasForeignKey(x => x.CuveId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AjustementStock>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_ajustement_approbateur", "valide_par IS NULL OR valide_par <> cree_par"));
        });

        // Tiers
        b.Entity<Client>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Fournisseur>().HasIndex(x => x.Code).IsUnique();
        b.Entity<PieceJointe>().HasIndex(x => new { x.ObjetType, x.ObjetId });

        // Achats / ventes
        b.Entity<CommandeFournisseur>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasOne(x => x.Fournisseur).WithMany().HasForeignKey(x => x.FournisseurId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lignes).WithOne().HasForeignKey(x => x.CommandeId);
            e.HasOne<CommandeClient>().WithMany().HasForeignKey(x => x.CommandeClientId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LigneCommandeFournisseur>(e =>
        {
            e.HasOne(x => x.Produit).WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_lcf_quantite", "quantite > 0"));
        });
        b.Entity<Reception>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasMany(x => x.Lignes).WithOne().HasForeignKey(x => x.ReceptionId);
            e.HasOne<Fournisseur>().WithMany().HasForeignKey(x => x.FournisseurId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LigneReception>(e =>
        {
            e.HasOne(x => x.Produit).WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_lr_acceptee", "quantite_acceptee <= quantite_recue AND quantite_acceptee >= 0"));
        });
        b.Entity<VenteArticle>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasMany(x => x.Lignes).WithOne().HasForeignKey(x => x.VenteId);
        });
        b.Entity<LigneVenteArticle>().HasOne(x => x.Produit).WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Facture>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasMany(x => x.Lignes).WithOne().HasForeignKey(x => x.FactureId);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Fournisseur).WithMany().HasForeignKey(x => x.FournisseurId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.FournisseurId, x.NumeroTiers }).IsUnique()
                .HasFilter("sens = 2 AND statut <> 3 AND numero_tiers IS NOT NULL").HasDatabaseName("ux_facture_fournisseur_numero");
            e.ToTable(t => t.HasCheckConstraint("ck_facture_tiers",
                "(sens = 1 AND client_id IS NOT NULL AND fournisseur_id IS NULL) OR (sens = 2 AND fournisseur_id IS NOT NULL AND client_id IS NULL)"));
        });
        b.Entity<LigneFacture>().HasOne(x => x.Produit).WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);

        // Vrac
        b.Entity<CommandeClient>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Produit).WithMany().HasForeignKey(x => x.ProduitId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_cc_volume", "volume > 0"));
        });
        b.Entity<LivraisonDirecte>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasOne<CommandeClient>().WithMany().HasForeignKey(x => x.CommandeClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Fournisseur>().WithMany().HasForeignKey(x => x.FournisseurId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_ld_quantites", "quantite_chargee >= 0 AND quantite_livree >= 0"));
        });
        b.Entity<FraisDirect>().HasMany(x => x.Repartitions).WithOne().HasForeignKey(x => x.FraisId);
        b.Entity<RepartitionFrais>().HasOne<LivraisonDirecte>().WithMany().HasForeignKey(x => x.LivraisonId).OnDelete(DeleteBehavior.Restrict);

        // Trésorerie
        b.Entity<CompteFinancier>().HasIndex(x => x.Code).IsUnique();
        b.Entity<MouvementFinancier>(e =>
        {
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.HasOne<CompteFinancier>().WithMany().HasForeignKey(x => x.CompteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CompteId, x.DateOperation });
            e.HasIndex(x => x.ReglementId);
            e.ToTable(t => t.HasCheckConstraint("ck_mf_non_nul", "montant <> 0"));
        });
        b.Entity<Reglement>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasMany(x => x.Affectations).WithOne().HasForeignKey(x => x.ReglementId);
            e.HasOne<CompteFinancier>().WithMany().HasForeignKey(x => x.CompteId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_reglement_montant", "montant > 0"));
        });
        b.Entity<AffectationReglement>(e =>
        {
            e.HasOne<Facture>().WithMany().HasForeignKey(x => x.FactureId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Charge>().WithMany().HasForeignKey(x => x.ChargeId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_affectation_cible", "(facture_id IS NULL) <> (charge_id IS NULL)");
                t.HasCheckConstraint("ck_affectation_montant", "montant > 0");
            });
        });
        b.Entity<TransfertInterne>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_transfert_comptes", "compte_source_id <> compte_destination_id AND montant > 0"));
        });
        b.Entity<SessionCaisse>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasIndex(x => x.CompteId).HasFilter("statut = 1").IsUnique().HasDatabaseName("ux_session_caisse_ouverte");
        });
        b.Entity<RapprochementBancaire>().HasMany(x => x.Lignes).WithOne().HasForeignKey(x => x.RapprochementId);
        b.Entity<LigneReleveBancaire>().HasIndex(x => x.MouvementId).IsUnique();

        // Charges et personnel
        b.Entity<CategorieCharge>().HasIndex(x => x.Code).IsUnique();
        b.Entity<CentreCout>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.CleRepartition).WithMany().HasForeignKey(x => x.CleRepartitionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CleRepartition>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasMany(x => x.Pourcentages).WithOne().HasForeignKey(x => x.CleId);
        });
        b.Entity<Charge>(e =>
        {
            e.HasIndex(x => x.Numero).IsUnique();
            e.HasOne(x => x.Categorie).WithMany().HasForeignKey(x => x.CategorieId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CentreCout).WithMany().HasForeignKey(x => x.CentreCoutId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_charge_montants", "montant_ht + montant_tva = montant_ttc AND montant_ttc > 0"));
        });
        b.Entity<Employe>(e =>
        {
            e.HasIndex(x => x.Matricule).IsUnique();
            e.Ignore(x => x.NomComplet);
        });
        b.Entity<ElementRemuneration>().HasOne<Employe>().WithMany().HasForeignKey(x => x.EmployeId);
        b.Entity<Absence>().HasOne<Employe>().WithMany().HasForeignKey(x => x.EmployeId);
        b.Entity<AffectationEmploye>().HasOne<Employe>().WithMany().HasForeignKey(x => x.EmployeId);

        b.Entity<Facture>().Ignore(x => x.GenereChiffreAffaires);
    }

    /// <summary>Renseigne automatiquement les dates de création et de modification.</summary>
    public Func<(Guid? utilisateur, DateTimeOffset maintenant)>? Contexte { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var (utilisateur, maintenant) = Contexte?.Invoke() ?? (null, DateTimeOffset.UtcNow);
        foreach (var e in ChangeTracker.Entries<Entite>())
        {
            if (e.State == EntityState.Added)
            {
                if (e.Entity.CreeLe == default) e.Entity.CreeLe = maintenant;
                e.Entity.CreePar ??= utilisateur;
            }
            else if (e.State == EntityState.Modified)
            {
                e.Entity.ModifieLe = maintenant;
                e.Entity.ModifiePar = utilisateur;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
