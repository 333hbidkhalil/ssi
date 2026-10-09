using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ssi.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class Initiale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "ajustements_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuve_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantite = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    motif = table.Column<string>(type: "text", nullable: false),
                    cause = table.Column<int>(type: "integer", nullable: false),
                    jaugeage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    mouvement_id = table.Column<long>(type: "bigint", nullable: true),
                    commentaire_decision = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ajustements_stock", x => x.id);
                    table.CheckConstraint("ck_ajustement_approbateur", "valide_par IS NULL OR valide_par <> cree_par");
                });

            migrationBuilder.CreateTable(
                name: "categories_charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    fixe_par_defaut = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories_charges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cles_idempotence",
                columns: table => new
                {
                    cle = table.Column<string>(type: "text", nullable: false),
                    utilisateur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "text", nullable: false),
                    code_statut = table.Column<int>(type: "integer", nullable: false),
                    reponse = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cles_idempotence", x => new { x.cle, x.utilisateur_id });
                });

            migrationBuilder.CreateTable(
                name: "cles_repartition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    methode = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cles_repartition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    plafond_credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    nom = table.Column<string>(type: "text", nullable: false),
                    ice = table.Column<string>(type: "text", nullable: true),
                    identifiant_fiscal = table.Column<string>(type: "text", nullable: true),
                    registre_commerce = table.Column<string>(type: "text", nullable: true),
                    adresse = table.Column<string>(type: "text", nullable: true),
                    ville = table.Column<string>(type: "text", nullable: true),
                    telephone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    contact = table.Column<string>(type: "text", nullable: true),
                    delai_paiement_jours = table.Column<int>(type: "integer", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "comptes_financiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    banque = table.Column<string>(type: "text", nullable: true),
                    rib = table.Column<string>(type: "text", nullable: true),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comptes_financiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    matricule = table.Column<string>(type: "text", nullable: false),
                    nom = table.Column<string>(type: "text", nullable: false),
                    prenom = table.Column<string>(type: "text", nullable: false),
                    cin = table.Column<string>(type: "text", nullable: true),
                    fonction = table.Column<string>(type: "text", nullable: false),
                    date_embauche = table.Column<DateOnly>(type: "date", nullable: false),
                    affectation = table.Column<string>(type: "text", nullable: true),
                    salaire_base = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    telephone = table.Column<string>(type: "text", nullable: true),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "familles_produits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    categorie = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_familles_produits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fournisseurs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conditions_achat = table.Column<string>(type: "text", nullable: true),
                    produits_fournis = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    nom = table.Column<string>(type: "text", nullable: false),
                    ice = table.Column<string>(type: "text", nullable: true),
                    identifiant_fiscal = table.Column<string>(type: "text", nullable: true),
                    registre_commerce = table.Column<string>(type: "text", nullable: true),
                    adresse = table.Column<string>(type: "text", nullable: true),
                    ville = table.Column<string>(type: "text", nullable: true),
                    telephone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    contact = table.Column<string>(type: "text", nullable: true),
                    delai_paiement_jours = table.Column<int>(type: "integer", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fournisseurs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "frais_directs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    facture_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    montant_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    methode = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_frais_directs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incidents_fournisseur",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    document_ref = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incidents_fournisseur", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    utilisateur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    login = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    objet_type = table.Column<string>(type: "text", nullable: false),
                    objet_id = table.Column<string>(type: "text", nullable: true),
                    objet_ref = table.Column<string>(type: "text", nullable: true),
                    motif = table.Column<string>(type: "text", nullable: true),
                    avant = table.Column<string>(type: "jsonb", nullable: true),
                    apres = table.Column<string>(type: "jsonb", nullable: true),
                    adresse_ip = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "objectifs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    periode = table.Column<DateOnly>(type: "date", nullable: false),
                    indicateur = table.Column<string>(type: "text", nullable: false),
                    activite = table.Column<int>(type: "integer", nullable: true),
                    valeur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_objectifs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parametres",
                columns: table => new
                {
                    cle = table.Column<string>(type: "text", nullable: false),
                    valeur = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parametres", x => x.cle);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    code = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    sensible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "pieces_jointes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    objet_type = table.Column<string>(type: "text", nullable: false),
                    objet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nom_fichier = table.Column<string>(type: "text", nullable: false),
                    type_mime = table.Column<string>(type: "text", nullable: false),
                    taille = table.Column<long>(type: "bigint", nullable: false),
                    contenu = table.Column<byte[]>(type: "bytea", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pieces_jointes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "positions_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuve_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantite = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    valeur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cmp = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    dernier_mouvement_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_positions_stock", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "postes_travail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    debut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    journee_exploitation = table.Column<DateOnly>(type: "date", nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    responsable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cloture_par = table.Column<Guid>(type: "uuid", nullable: true),
                    cloture_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    montant_theorique_ttc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_reglements = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ecart = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commentaire = table.Column<string>(type: "text", nullable: true),
                    nombre_reouvertures = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_postes_travail", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rapprochements_bancaires",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_releve = table.Column<DateOnly>(type: "date", nullable: false),
                    solde_releve = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    valide = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rapprochements_bancaires", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "relances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    facture_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    moyen = table.Column<string>(type: "text", nullable: false),
                    commentaire = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    systeme = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sauvegardes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fichier = table.Column<string>(type: "text", nullable: false),
                    taille = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: true),
                    statut = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sauvegardes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sequences_documents",
                columns: table => new
                {
                    type_document = table.Column<string>(type: "text", nullable: false),
                    prefixe = table.Column<string>(type: "text", nullable: false),
                    annee = table.Column<int>(type: "integer", nullable: false),
                    prochain_numero = table.Column<long>(type: "bigint", nullable: false),
                    longueur = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sequences_documents", x => x.type_document);
                });

            migrationBuilder.CreateTable(
                name: "sessions_caisse",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ouverture = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    solde_ouverture = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cloture = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    encaissements = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    decaissements = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    solde_theorique = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    montant_compte = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ecart = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    commentaire = table.Column<string>(type: "text", nullable: true),
                    cloture_par = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre_reouvertures = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions_caisse", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "simulations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nom = table.Column<string>(type: "text", nullable: false),
                    parametres = table.Column<string>(type: "jsonb", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "societe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    raison_sociale = table.Column<string>(type: "text", nullable: false),
                    forme_juridique = table.Column<string>(type: "text", nullable: true),
                    ice = table.Column<string>(type: "text", nullable: true),
                    identifiant_fiscal = table.Column<string>(type: "text", nullable: true),
                    registre_commerce = table.Column<string>(type: "text", nullable: true),
                    patente = table.Column<string>(type: "text", nullable: true),
                    cnss = table.Column<string>(type: "text", nullable: true),
                    adresse = table.Column<string>(type: "text", nullable: true),
                    ville = table.Column<string>(type: "text", nullable: true),
                    telephone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    mentions_legales = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_societe", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_taxes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transferts_internes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compte_source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compte_destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    commission = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transferts_internes", x => x.id);
                    table.CheckConstraint("ck_transfert_comptes", "compte_source_id <> compte_destination_id AND montant > 0");
                });

            migrationBuilder.CreateTable(
                name: "utilisateurs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    login = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    nom_complet = table.Column<string>(type: "text", nullable: false),
                    hash_mot_de_passe = table.Column<string>(type: "text", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    employe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doit_changer_mot_de_passe = table.Column<bool>(type: "boolean", nullable: false),
                    echecs_connexion = table.Column<int>(type: "integer", nullable: false),
                    verrouille_jusqu_a = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    derniere_connexion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    horodatage_securite = table.Column<Guid>(type: "uuid", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utilisateurs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ventes_articles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    poste_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mode_reglement = table.Column<int>(type: "integer", nullable: false),
                    compte_id = table.Column<Guid>(type: "uuid", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    total_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_tva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_ttc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ventes_articles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "volucompteurs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_volucompteurs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "centres_cout",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    activite = table.Column<int>(type: "integer", nullable: true),
                    cle_repartition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_centres_cout", x => x.id);
                    table.ForeignKey(
                        name: "fk_centres_cout_cles_repartition_cle_repartition_id",
                        column: x => x.cle_repartition_id,
                        principalTable: "cles_repartition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cles_repartition_manuelles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activite = table.Column<int>(type: "integer", nullable: false),
                    pourcentage = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cles_repartition_manuelles", x => x.id);
                    table.ForeignKey(
                        name: "fk_cles_repartition_manuelles_cles_repartition_cle_id",
                        column: x => x.cle_id,
                        principalTable: "cles_repartition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mouvements_financiers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    compte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_operation = table.Column<DateOnly>(type: "date", nullable: false),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    reglement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transfert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_caisse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    poste_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_ref = table.Column<string>(type: "text", nullable: true),
                    mouvement_annule_id = table.Column<long>(type: "bigint", nullable: true),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mouvements_financiers", x => x.id);
                    table.CheckConstraint("ck_mf_non_nul", "montant <> 0");
                    table.ForeignKey(
                        name: "fk_mouvements_financiers_comptes_financiers_compte_id",
                        column: x => x.compte_id,
                        principalTable: "comptes_financiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reglements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sens = table.Column<int>(type: "integer", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beneficiaire = table.Column<string>(type: "text", nullable: true),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    compte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "text", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reglements", x => x.id);
                    table.CheckConstraint("ck_reglement_montant", "montant > 0");
                    table.ForeignKey(
                        name: "fk_reglements_comptes_financiers_compte_id",
                        column: x => x.compte_id,
                        principalTable: "comptes_financiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "absences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debut = table.Column<DateOnly>(type: "date", nullable: false),
                    fin = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    justifiee = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_absences", x => x.id);
                    table.ForeignKey(
                        name: "fk_absences_employes_employe_id",
                        column: x => x.employe_id,
                        principalTable: "employes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "affectations_employe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonction = table.Column<string>(type: "text", nullable: false),
                    affectation = table.Column<string>(type: "text", nullable: true),
                    debut = table.Column<DateOnly>(type: "date", nullable: false),
                    fin = table.Column<DateOnly>(type: "date", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_affectations_employe", x => x.id);
                    table.ForeignKey(
                        name: "fk_affectations_employe_employes_employe_id",
                        column: x => x.employe_id,
                        principalTable: "employes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "elements_remuneration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periode = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    commentaire = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_elements_remuneration", x => x.id);
                    table.ForeignKey(
                        name: "fk_elements_remuneration_employes_employe_id",
                        column: x => x.employe_id,
                        principalTable: "employes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "factures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sens = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    origine = table.Column<int>(type: "integer", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_tiers = table.Column<string>(type: "text", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    echeance = table.Column<DateOnly>(type: "date", nullable: false),
                    total_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_tva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_ttc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    facture_origine_id = table.Column<Guid>(type: "uuid", nullable: true),
                    commande_client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reception_id = table.Column<Guid>(type: "uuid", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    commentaire = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_factures", x => x.id);
                    table.CheckConstraint("ck_facture_tiers", "(sens = 1 AND client_id IS NOT NULL AND fournisseur_id IS NULL) OR (sens = 2 AND fournisseur_id IS NOT NULL AND client_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_factures_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_factures_fournisseurs_fournisseur_id",
                        column: x => x.fournisseur_id,
                        principalTable: "fournisseurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    commande_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    numero_bl = table.Column<string>(type: "text", nullable: true),
                    transporteur = table.Column<string>(type: "text", nullable: true),
                    vehicule = table.Column<string>(type: "text", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_receptions_fournisseurs_fournisseur_id",
                        column: x => x.fournisseur_id,
                        principalTable: "fournisseurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reglements_poste",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    volume = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    reference = table.Column<string>(type: "text", nullable: true),
                    operateur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    facture_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reglements_poste", x => x.id);
                    table.CheckConstraint("ck_reglement_poste_credit", "mode <> 5 OR client_id IS NOT NULL");
                    table.CheckConstraint("ck_reglement_poste_montant", "montant > 0");
                    table.ForeignKey(
                        name: "fk_reglements_poste_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reglements_poste_postes_travail_poste_id",
                        column: x => x.poste_id,
                        principalTable: "postes_travail",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lignes_releve_bancaire",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rapprochement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    libelle = table.Column<string>(type: "text", nullable: false),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    mouvement_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_releve_bancaire", x => x.id);
                    table.ForeignKey(
                        name: "fk_lignes_releve_bancaire_rapprochements_bancaires_rapprocheme",
                        column: x => x.rapprochement_id,
                        principalTable: "rapprochements_bancaires",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_code = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_code });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_code",
                        column: x => x.permission_code,
                        principalTable: "permissions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "produits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    designation = table.Column<string>(type: "text", nullable: false),
                    famille_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unite = table.Column<string>(type: "text", nullable: false),
                    marque = table.Column<string>(type: "text", nullable: true),
                    conditionnement = table.Column<string>(type: "text", nullable: true),
                    taxe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    methode_valorisation = table.Column<int>(type: "integer", nullable: false),
                    seuil_alerte = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    type_carburant = table.Column<int>(type: "integer", nullable: true),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produits", x => x.id);
                    table.ForeignKey(
                        name: "fk_produits_familles_produits_famille_id",
                        column: x => x.famille_id,
                        principalTable: "familles_produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produits_taxes_taxe_id",
                        column: x => x.taxe_id,
                        principalTable: "taxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "taux_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    taxe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taux = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    date_debut = table.Column<DateOnly>(type: "date", nullable: false),
                    date_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    commentaire = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_taux_taxes", x => x.id);
                    table.ForeignKey(
                        name: "fk_taux_taxes_taxes_taxe_id",
                        column: x => x.taxe_id,
                        principalTable: "taxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "utilisateur_permissions",
                columns: table => new
                {
                    utilisateur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_code = table.Column<string>(type: "text", nullable: false),
                    accorde = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utilisateur_permissions", x => new { x.utilisateur_id, x.permission_code });
                    table.ForeignKey(
                        name: "fk_utilisateur_permissions_permissions_permission_code",
                        column: x => x.permission_code,
                        principalTable: "permissions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_utilisateur_permissions_utilisateurs_utilisateur_id",
                        column: x => x.utilisateur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "utilisateur_roles",
                columns: table => new
                {
                    utilisateur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utilisateur_roles", x => new { x.utilisateur_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_utilisateur_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_utilisateur_roles_utilisateurs_utilisateur_id",
                        column: x => x.utilisateur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beneficiaire = table.Column<string>(type: "text", nullable: true),
                    categorie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    centre_cout_id = table.Column<Guid>(type: "uuid", nullable: false),
                    montant_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_tva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_ttc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fixe = table.Column<bool>(type: "boolean", nullable: false),
                    echeance = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    reference_piece = table.Column<string>(type: "text", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charges", x => x.id);
                    table.CheckConstraint("ck_charge_montants", "montant_ht + montant_tva = montant_ttc AND montant_ttc > 0");
                    table.ForeignKey(
                        name: "fk_charges_categories_charges_categorie_id",
                        column: x => x.categorie_id,
                        principalTable: "categories_charges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_charges_centres_cout_centre_cout_id",
                        column: x => x.centre_cout_id,
                        principalTable: "centres_cout",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "commandes_client",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    volume = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    prix_vente_ht = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    date_prevue = table.Column<DateOnly>(type: "date", nullable: true),
                    lieu_livraison = table.Column<string>(type: "text", nullable: true),
                    conditions_reglement = table.Column<string>(type: "text", nullable: true),
                    transporteur = table.Column<string>(type: "text", nullable: true),
                    fournisseur_prevu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    marge_prevue_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    prix_achat_prevu_ht = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    frais_prevus_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    derogation_par = table.Column<Guid>(type: "uuid", nullable: true),
                    derogation_motif = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commandes_client", x => x.id);
                    table.CheckConstraint("ck_cc_volume", "volume > 0");
                    table.ForeignKey(
                        name: "fk_commandes_client_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_commandes_client_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cuves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacite = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    seuil_minimal = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    tolerance_ecart_litres = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    tolerance_ecart_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    observations = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuves", x => x.id);
                    table.CheckConstraint("ck_cuve_capacite", "capacite > 0");
                    table.ForeignKey(
                        name: "fk_cuves_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lignes_facture",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    facture_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    designation = table.Column<string>(type: "text", nullable: false),
                    quantite = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    prix_unitaire_ht = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    taux_tva = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    montant_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_tva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_ttc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    livraison_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reception_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_facture", x => x.id);
                    table.ForeignKey(
                        name: "fk_lignes_facture_factures_facture_id",
                        column: x => x.facture_id,
                        principalTable: "factures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lignes_facture_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lignes_reception",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reception_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuve_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantite_commandee = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    quantite_chargee = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    quantite_recue = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    quantite_acceptee = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    prix_unitaire_ht = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    frais_attribues_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    justification_ecart = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_reception", x => x.id);
                    table.CheckConstraint("ck_lr_acceptee", "quantite_acceptee <= quantite_recue AND quantite_acceptee >= 0");
                    table.ForeignKey(
                        name: "fk_lignes_reception_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lignes_reception_receptions_reception_id",
                        column: x => x.reception_id,
                        principalTable: "receptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lignes_vente_article",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantite = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    prix_unitaire_ttc = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    taux_tva = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    montant_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_tva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_ttc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cout_unitaire = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_vente_article", x => x.id);
                    table.ForeignKey(
                        name: "fk_lignes_vente_article_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lignes_vente_article_ventes_articles_vente_id",
                        column: x => x.vente_id,
                        principalTable: "ventes_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prix_produits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    prix = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    debut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motif = table.Column<string>(type: "text", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prix_produits", x => x.id);
                    table.CheckConstraint("ck_prix_positif", "prix > 0");
                    table.ForeignKey(
                        name: "fk_prix_produits_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "affectations_reglement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reglement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    facture_id = table.Column<Guid>(type: "uuid", nullable: true),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    montant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_affectations_reglement", x => x.id);
                    table.CheckConstraint("ck_affectation_cible", "(facture_id IS NULL) <> (charge_id IS NULL)");
                    table.CheckConstraint("ck_affectation_montant", "montant > 0");
                    table.ForeignKey(
                        name: "fk_affectations_reglement_charges_charge_id",
                        column: x => x.charge_id,
                        principalTable: "charges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_affectations_reglement_factures_facture_id",
                        column: x => x.facture_id,
                        principalTable: "factures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_affectations_reglement_reglements_reglement_id",
                        column: x => x.reglement_id,
                        principalTable: "reglements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commandes_fournisseur",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    date_prevue = table.Column<DateOnly>(type: "date", nullable: true),
                    destination = table.Column<int>(type: "integer", nullable: false),
                    commande_client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_fournisseur = table.Column<string>(type: "text", nullable: true),
                    conditions = table.Column<string>(type: "text", nullable: true),
                    frais_prevus_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    confirmee = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commandes_fournisseur", x => x.id);
                    table.ForeignKey(
                        name: "fk_commandes_fournisseur_commandes_client_commande_client_id",
                        column: x => x.commande_client_id,
                        principalTable: "commandes_client",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_commandes_fournisseur_fournisseurs_fournisseur_id",
                        column: x => x.fournisseur_id,
                        principalTable: "fournisseurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "livraisons_directes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    commande_client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commande_fournisseur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fournisseur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    quantite_commandee = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    quantite_chargee = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    quantite_livree = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    numero_bl_fournisseur = table.Column<string>(type: "text", nullable: true),
                    numero_reception_client = table.Column<string>(type: "text", nullable: true),
                    vehicule = table.Column<string>(type: "text", nullable: true),
                    transporteur = table.Column<string>(type: "text", nullable: true),
                    chauffeur = table.Column<string>(type: "text", nullable: true),
                    prix_achat_ht = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    prix_vente_ht = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    justification_ecart = table.Column<string>(type: "text", nullable: true),
                    reserves = table.Column<string>(type: "text", nullable: true),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    numero = table.Column<string>(type: "text", nullable: false),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    annule_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    annule_par = table.Column<Guid>(type: "uuid", nullable: true),
                    motif_annulation = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_livraisons_directes", x => x.id);
                    table.CheckConstraint("ck_ld_quantites", "quantite_chargee >= 0 AND quantite_livree >= 0");
                    table.ForeignKey(
                        name: "fk_livraisons_directes_commandes_client_commande_client_id",
                        column: x => x.commande_client_id,
                        principalTable: "commandes_client",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_livraisons_directes_fournisseurs_fournisseur_id",
                        column: x => x.fournisseur_id,
                        principalTable: "fournisseurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jaugeages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuve_id = table.Column<Guid>(type: "uuid", nullable: false),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    volume_mesure = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    hauteur_cm = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    temperature_c = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    methode = table.Column<int>(type: "integer", nullable: false),
                    stock_theorique = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    ecart_litres = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    ecart_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    valeur_indicative = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    hors_tolerance = table.Column<bool>(type: "boolean", nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    cause = table.Column<int>(type: "integer", nullable: false),
                    explication = table.Column<string>(type: "text", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jaugeages", x => x.id);
                    table.ForeignKey(
                        name: "fk_jaugeages_cuves_cuve_id",
                        column: x => x.cuve_id,
                        principalTable: "cuves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mouvements_stock",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuve_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    quantite = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    cout_unitaire = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    valeur = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    quantite_apres = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    cmp_apres = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: false),
                    document_type = table.Column<string>(type: "text", nullable: true),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_ref = table.Column<string>(type: "text", nullable: true),
                    motif = table.Column<string>(type: "text", nullable: true),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mouvements_stock", x => x.id);
                    table.CheckConstraint("ck_mouvement_non_nul", "quantite <> 0 OR (type IN (10, 11) AND valeur <> 0)");
                    table.ForeignKey(
                        name: "fk_mouvements_stock_cuves_cuve_id",
                        column: x => x.cuve_id,
                        principalTable: "cuves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mouvements_stock_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pistolets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    volucompteur_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuve_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index_maximum = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    index_initial_cycle = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    cycle = table.Column<int>(type: "integer", nullable: false),
                    actif = table.Column<bool>(type: "boolean", nullable: false),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pistolets", x => x.id);
                    table.ForeignKey(
                        name: "fk_pistolets_cuves_cuve_id",
                        column: x => x.cuve_id,
                        principalTable: "cuves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pistolets_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pistolets_volucompteurs_volucompteur_id",
                        column: x => x.volucompteur_id,
                        principalTable: "volucompteurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lignes_commande_fournisseur",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    commande_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuve_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantite = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    prix_unitaire_ht = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lignes_commande_fournisseur", x => x.id);
                    table.CheckConstraint("ck_lcf_quantite", "quantite > 0");
                    table.ForeignKey(
                        name: "fk_lignes_commande_fournisseur_commandes_fournisseur_commande_",
                        column: x => x.commande_id,
                        principalTable: "commandes_fournisseur",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lignes_commande_fournisseur_produits_produit_id",
                        column: x => x.produit_id,
                        principalTable: "produits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "repartitions_frais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    frais_id = table.Column<Guid>(type: "uuid", nullable: false),
                    livraison_id = table.Column<Guid>(type: "uuid", nullable: false),
                    montant_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repartitions_frais", x => x.id);
                    table.ForeignKey(
                        name: "fk_repartitions_frais_frais_directs_frais_id",
                        column: x => x.frais_id,
                        principalTable: "frais_directs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_repartitions_frais_livraisons_directes_livraison_id",
                        column: x => x.livraison_id,
                        principalTable: "livraisons_directes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evenements_compteur",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pistolet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    ancien_index = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    nouvel_index = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    motif = table.Column<string>(type: "text", nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    valide_par = table.Column<Guid>(type: "uuid", nullable: true),
                    valide_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evenements_compteur", x => x.id);
                    table.ForeignKey(
                        name: "fk_evenements_compteur_pistolets_pistolet_id",
                        column: x => x.pistolet_id,
                        principalTable: "pistolets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "releves_index",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poste_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pistolet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle = table.Column<int>(type: "integer", nullable: false),
                    operateur_id = table.Column<Guid>(type: "uuid", nullable: true),
                    index_debut = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    index_fin = table.Column<decimal>(type: "numeric(15,3)", precision: 15, scale: 3, nullable: false),
                    horodatage = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prix_unitaire_ttc = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    prix_produit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    taux_tva = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    volume = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    montant_ttc = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    montant_ht = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    statut = table.Column<int>(type: "integer", nullable: false),
                    anomalies = table.Column<string>(type: "jsonb", nullable: true),
                    commentaire = table.Column<string>(type: "text", nullable: true),
                    motif_correction = table.Column<string>(type: "text", nullable: true),
                    cout_unitaire = table.Column<decimal>(type: "numeric(14,6)", precision: 14, scale: 6, nullable: true),
                    cree_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cree_par = table.Column<Guid>(type: "uuid", nullable: true),
                    modifie_le = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modifie_par = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_releves_index", x => x.id);
                    table.CheckConstraint("ck_releve_index", "index_fin >= index_debut");
                    table.ForeignKey(
                        name: "fk_releves_index_employes_operateur_id",
                        column: x => x.operateur_id,
                        principalTable: "employes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_releves_index_pistolets_pistolet_id",
                        column: x => x.pistolet_id,
                        principalTable: "pistolets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_releves_index_postes_travail_poste_id",
                        column: x => x.poste_id,
                        principalTable: "postes_travail",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_absences_employe_id",
                table: "absences",
                column: "employe_id");

            migrationBuilder.CreateIndex(
                name: "ix_affectations_employe_employe_id",
                table: "affectations_employe",
                column: "employe_id");

            migrationBuilder.CreateIndex(
                name: "ix_affectations_reglement_charge_id",
                table: "affectations_reglement",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_affectations_reglement_facture_id",
                table: "affectations_reglement",
                column: "facture_id");

            migrationBuilder.CreateIndex(
                name: "ix_affectations_reglement_reglement_id",
                table: "affectations_reglement",
                column: "reglement_id");

            migrationBuilder.CreateIndex(
                name: "ix_ajustements_stock_numero",
                table: "ajustements_stock",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_charges_code",
                table: "categories_charges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_centres_cout_cle_repartition_id",
                table: "centres_cout",
                column: "cle_repartition_id");

            migrationBuilder.CreateIndex(
                name: "ix_centres_cout_code",
                table: "centres_cout",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charges_categorie_id",
                table: "charges",
                column: "categorie_id");

            migrationBuilder.CreateIndex(
                name: "ix_charges_centre_cout_id",
                table: "charges",
                column: "centre_cout_id");

            migrationBuilder.CreateIndex(
                name: "ix_charges_numero",
                table: "charges",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cles_repartition_code",
                table: "cles_repartition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cles_repartition_manuelles_cle_id",
                table: "cles_repartition_manuelles",
                column: "cle_id");

            migrationBuilder.CreateIndex(
                name: "ix_clients_code",
                table: "clients",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_commandes_client_client_id",
                table: "commandes_client",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_commandes_client_numero",
                table: "commandes_client",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_commandes_client_produit_id",
                table: "commandes_client",
                column: "produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_commandes_fournisseur_commande_client_id",
                table: "commandes_fournisseur",
                column: "commande_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_commandes_fournisseur_fournisseur_id",
                table: "commandes_fournisseur",
                column: "fournisseur_id");

            migrationBuilder.CreateIndex(
                name: "ix_commandes_fournisseur_numero",
                table: "commandes_fournisseur",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comptes_financiers_code",
                table: "comptes_financiers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cuves_code",
                table: "cuves",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cuves_produit_id",
                table: "cuves",
                column: "produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_elements_remuneration_employe_id",
                table: "elements_remuneration",
                column: "employe_id");

            migrationBuilder.CreateIndex(
                name: "ix_employes_matricule",
                table: "employes",
                column: "matricule",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evenements_compteur_pistolet_id",
                table: "evenements_compteur",
                column: "pistolet_id");

            migrationBuilder.CreateIndex(
                name: "ix_factures_client_id",
                table: "factures",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_factures_numero",
                table: "factures",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_facture_fournisseur_numero",
                table: "factures",
                columns: new[] { "fournisseur_id", "numero_tiers" },
                unique: true,
                filter: "sens = 2 AND statut <> 3 AND numero_tiers IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_familles_produits_code",
                table: "familles_produits",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fournisseurs_code",
                table: "fournisseurs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_jaugeages_cuve_id",
                table: "jaugeages",
                column: "cuve_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_audit_horodatage",
                table: "journal_audit",
                column: "horodatage");

            migrationBuilder.CreateIndex(
                name: "ix_journal_audit_objet_type_objet_id",
                table: "journal_audit",
                columns: new[] { "objet_type", "objet_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lignes_commande_fournisseur_commande_id",
                table: "lignes_commande_fournisseur",
                column: "commande_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_commande_fournisseur_produit_id",
                table: "lignes_commande_fournisseur",
                column: "produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_facture_facture_id",
                table: "lignes_facture",
                column: "facture_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_facture_produit_id",
                table: "lignes_facture",
                column: "produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_reception_produit_id",
                table: "lignes_reception",
                column: "produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_reception_reception_id",
                table: "lignes_reception",
                column: "reception_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_releve_bancaire_mouvement_id",
                table: "lignes_releve_bancaire",
                column: "mouvement_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lignes_releve_bancaire_rapprochement_id",
                table: "lignes_releve_bancaire",
                column: "rapprochement_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_vente_article_produit_id",
                table: "lignes_vente_article",
                column: "produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_lignes_vente_article_vente_id",
                table: "lignes_vente_article",
                column: "vente_id");

            migrationBuilder.CreateIndex(
                name: "ix_livraisons_directes_commande_client_id",
                table: "livraisons_directes",
                column: "commande_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_livraisons_directes_fournisseur_id",
                table: "livraisons_directes",
                column: "fournisseur_id");

            migrationBuilder.CreateIndex(
                name: "ix_livraisons_directes_numero",
                table: "livraisons_directes",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_financiers_compte_id_date_operation",
                table: "mouvements_financiers",
                columns: new[] { "compte_id", "date_operation" });

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_financiers_reglement_id",
                table: "mouvements_financiers",
                column: "reglement_id");

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_cuve_id",
                table: "mouvements_stock",
                column: "cuve_id");

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_document_type_document_id",
                table: "mouvements_stock",
                columns: new[] { "document_type", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_mouvements_stock_produit_id_cuve_id_horodatage",
                table: "mouvements_stock",
                columns: new[] { "produit_id", "cuve_id", "horodatage" });

            migrationBuilder.CreateIndex(
                name: "ix_objectifs_periode_indicateur_activite",
                table: "objectifs",
                columns: new[] { "periode", "indicateur", "activite" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_pieces_jointes_objet_type_objet_id",
                table: "pieces_jointes",
                columns: new[] { "objet_type", "objet_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pistolets_cuve_id",
                table: "pistolets",
                column: "cuve_id");

            migrationBuilder.CreateIndex(
                name: "ix_pistolets_produit_id",
                table: "pistolets",
                column: "produit_id");

            migrationBuilder.CreateIndex(
                name: "ix_pistolets_volucompteur_id_code",
                table: "pistolets",
                columns: new[] { "volucompteur_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_positions_stock_produit_id_cuve_id",
                table: "positions_stock",
                columns: new[] { "produit_id", "cuve_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_postes_travail_numero",
                table: "postes_travail",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_poste_unique_ouvert",
                table: "postes_travail",
                column: "statut",
                unique: true,
                filter: "statut = 1");

            migrationBuilder.CreateIndex(
                name: "ix_prix_produits_produit_id_type_debut",
                table: "prix_produits",
                columns: new[] { "produit_id", "type", "debut" });

            migrationBuilder.CreateIndex(
                name: "ix_produits_code",
                table: "produits",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produits_famille_id",
                table: "produits",
                column: "famille_id");

            migrationBuilder.CreateIndex(
                name: "ix_produits_taxe_id",
                table: "produits",
                column: "taxe_id");

            migrationBuilder.CreateIndex(
                name: "ix_receptions_fournisseur_id",
                table: "receptions",
                column: "fournisseur_id");

            migrationBuilder.CreateIndex(
                name: "ix_receptions_numero",
                table: "receptions",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reglements_compte_id",
                table: "reglements",
                column: "compte_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglements_numero",
                table: "reglements",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reglements_poste_client_id",
                table: "reglements_poste",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_reglements_poste_poste_id",
                table: "reglements_poste",
                column: "poste_id");

            migrationBuilder.CreateIndex(
                name: "ix_releves_index_operateur_id",
                table: "releves_index",
                column: "operateur_id");

            migrationBuilder.CreateIndex(
                name: "ix_releves_index_pistolet_id",
                table: "releves_index",
                column: "pistolet_id");

            migrationBuilder.CreateIndex(
                name: "ux_releve_poste_pistolet",
                table: "releves_index",
                columns: new[] { "poste_id", "pistolet_id" },
                unique: true,
                filter: "statut <> 3");

            migrationBuilder.CreateIndex(
                name: "ix_repartitions_frais_frais_id",
                table: "repartitions_frais",
                column: "frais_id");

            migrationBuilder.CreateIndex(
                name: "ix_repartitions_frais_livraison_id",
                table: "repartitions_frais",
                column: "livraison_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_code",
                table: "role_permissions",
                column: "permission_code");

            migrationBuilder.CreateIndex(
                name: "ix_roles_code",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessions_caisse_numero",
                table: "sessions_caisse",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_session_caisse_ouverte",
                table: "sessions_caisse",
                column: "compte_id",
                unique: true,
                filter: "statut = 1");

            migrationBuilder.CreateIndex(
                name: "ix_taux_taxes_taxe_id",
                table: "taux_taxes",
                column: "taxe_id");

            migrationBuilder.CreateIndex(
                name: "ix_taxes_code",
                table: "taxes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transferts_internes_numero",
                table: "transferts_internes",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_utilisateur_permissions_permission_code",
                table: "utilisateur_permissions",
                column: "permission_code");

            migrationBuilder.CreateIndex(
                name: "ix_utilisateur_roles_role_id",
                table: "utilisateur_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_utilisateurs_login",
                table: "utilisateurs",
                column: "login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventes_articles_numero",
                table: "ventes_articles",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_volucompteurs_code",
                table: "volucompteurs",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "absences");

            migrationBuilder.DropTable(
                name: "affectations_employe");

            migrationBuilder.DropTable(
                name: "affectations_reglement");

            migrationBuilder.DropTable(
                name: "ajustements_stock");

            migrationBuilder.DropTable(
                name: "cles_idempotence");

            migrationBuilder.DropTable(
                name: "cles_repartition_manuelles");

            migrationBuilder.DropTable(
                name: "elements_remuneration");

            migrationBuilder.DropTable(
                name: "evenements_compteur");

            migrationBuilder.DropTable(
                name: "incidents_fournisseur");

            migrationBuilder.DropTable(
                name: "jaugeages");

            migrationBuilder.DropTable(
                name: "journal_audit");

            migrationBuilder.DropTable(
                name: "lignes_commande_fournisseur");

            migrationBuilder.DropTable(
                name: "lignes_facture");

            migrationBuilder.DropTable(
                name: "lignes_reception");

            migrationBuilder.DropTable(
                name: "lignes_releve_bancaire");

            migrationBuilder.DropTable(
                name: "lignes_vente_article");

            migrationBuilder.DropTable(
                name: "mouvements_financiers");

            migrationBuilder.DropTable(
                name: "mouvements_stock");

            migrationBuilder.DropTable(
                name: "objectifs");

            migrationBuilder.DropTable(
                name: "parametres");

            migrationBuilder.DropTable(
                name: "pieces_jointes");

            migrationBuilder.DropTable(
                name: "positions_stock");

            migrationBuilder.DropTable(
                name: "prix_produits");

            migrationBuilder.DropTable(
                name: "reglements_poste");

            migrationBuilder.DropTable(
                name: "relances");

            migrationBuilder.DropTable(
                name: "releves_index");

            migrationBuilder.DropTable(
                name: "repartitions_frais");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "sauvegardes");

            migrationBuilder.DropTable(
                name: "sequences_documents");

            migrationBuilder.DropTable(
                name: "sessions_caisse");

            migrationBuilder.DropTable(
                name: "simulations");

            migrationBuilder.DropTable(
                name: "societe");

            migrationBuilder.DropTable(
                name: "taux_taxes");

            migrationBuilder.DropTable(
                name: "transferts_internes");

            migrationBuilder.DropTable(
                name: "utilisateur_permissions");

            migrationBuilder.DropTable(
                name: "utilisateur_roles");

            migrationBuilder.DropTable(
                name: "charges");

            migrationBuilder.DropTable(
                name: "reglements");

            migrationBuilder.DropTable(
                name: "commandes_fournisseur");

            migrationBuilder.DropTable(
                name: "factures");

            migrationBuilder.DropTable(
                name: "receptions");

            migrationBuilder.DropTable(
                name: "rapprochements_bancaires");

            migrationBuilder.DropTable(
                name: "ventes_articles");

            migrationBuilder.DropTable(
                name: "employes");

            migrationBuilder.DropTable(
                name: "pistolets");

            migrationBuilder.DropTable(
                name: "postes_travail");

            migrationBuilder.DropTable(
                name: "frais_directs");

            migrationBuilder.DropTable(
                name: "livraisons_directes");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "utilisateurs");

            migrationBuilder.DropTable(
                name: "categories_charges");

            migrationBuilder.DropTable(
                name: "centres_cout");

            migrationBuilder.DropTable(
                name: "comptes_financiers");

            migrationBuilder.DropTable(
                name: "cuves");

            migrationBuilder.DropTable(
                name: "volucompteurs");

            migrationBuilder.DropTable(
                name: "commandes_client");

            migrationBuilder.DropTable(
                name: "fournisseurs");

            migrationBuilder.DropTable(
                name: "cles_repartition");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "produits");

            migrationBuilder.DropTable(
                name: "familles_produits");

            migrationBuilder.DropTable(
                name: "taxes");
        }
    }
}
