using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssi.Infrastructure.Persistance.Migrations
{
    /// <summary>
    /// Garanties d'intégrité portées par la base elle-même :
    /// journaux append-only, interdiction de supprimer un document validé,
    /// contraintes d'exclusion (périodes de prix, plages d'index, taux datés).
    /// </summary>
    public partial class IntegriteMetier : Migration
    {
        private static readonly string[] TablesAppendOnly = ["journal_audit", "mouvements_stock", "mouvements_financiers"];

        private static readonly string[] TablesDocuments =
        [
            "postes_travail", "factures", "reglements", "receptions", "livraisons_directes", "charges", "sessions_caisse",
            "commandes_fournisseur", "commandes_client", "ventes_articles", "transferts_internes", "ajustements_stock"
        ];

        protected override void Up(MigrationBuilder m)
        {
            m.Sql("""
                CREATE OR REPLACE FUNCTION ssi_interdire_modification() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  RAISE EXCEPTION 'SSI : la table % est en ajout seul ; utilisez une opération compensatoire.', TG_TABLE_NAME
                    USING ERRCODE = 'P0001';
                END $$;

                CREATE OR REPLACE FUNCTION ssi_interdire_suppression_validee() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF OLD.valide_le IS NOT NULL THEN
                    RAISE EXCEPTION 'SSI : un document validé (% %) ne peut pas être supprimé ; utilisez l''annulation.', TG_TABLE_NAME, OLD.numero
                      USING ERRCODE = 'P0001';
                  END IF;
                  RETURN OLD;
                END $$;

                CREATE OR REPLACE FUNCTION ssi_interdire_suppression_releve_valide() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF OLD.statut = 2 THEN
                    RAISE EXCEPTION 'SSI : un relevé validé ne peut pas être supprimé.' USING ERRCODE = 'P0001';
                  END IF;
                  RETURN OLD;
                END $$;
                """);

            foreach (var t in TablesAppendOnly)
            {
                m.Sql($"CREATE TRIGGER trg_{t}_append_only BEFORE UPDATE OR DELETE ON {t} FOR EACH ROW EXECUTE FUNCTION ssi_interdire_modification();");
                m.Sql($"CREATE TRIGGER trg_{t}_no_truncate BEFORE TRUNCATE ON {t} FOR EACH STATEMENT EXECUTE FUNCTION ssi_interdire_modification();");
            }
            foreach (var t in TablesDocuments)
                m.Sql($"CREATE TRIGGER trg_{t}_suppression BEFORE DELETE ON {t} FOR EACH ROW EXECUTE FUNCTION ssi_interdire_suppression_validee();");
            m.Sql("CREATE TRIGGER trg_releves_index_suppression BEFORE DELETE ON releves_index FOR EACH ROW EXECUTE FUNCTION ssi_interdire_suppression_releve_valide();");

            m.Sql("""
                ALTER TABLE prix_produits ADD CONSTRAINT ex_prix_periodes
                  EXCLUDE USING gist (produit_id WITH =, type WITH =, tstzrange(debut, fin) WITH &&);
                ALTER TABLE releves_index ADD CONSTRAINT ex_releve_plages_index
                  EXCLUDE USING gist (pistolet_id WITH =, cycle WITH =, numrange(index_debut, index_fin) WITH &&) WHERE (statut <> 3);
                ALTER TABLE taux_taxes ADD CONSTRAINT ex_taux_periodes
                  EXCLUDE USING gist (taxe_id WITH =, daterange(date_debut, date_fin, '[]') WITH &&);
                """);
        }

        protected override void Down(MigrationBuilder m)
        {
            m.Sql("ALTER TABLE prix_produits DROP CONSTRAINT IF EXISTS ex_prix_periodes;");
            m.Sql("ALTER TABLE releves_index DROP CONSTRAINT IF EXISTS ex_releve_plages_index;");
            m.Sql("ALTER TABLE taux_taxes DROP CONSTRAINT IF EXISTS ex_taux_periodes;");
            foreach (var t in TablesAppendOnly)
            {
                m.Sql($"DROP TRIGGER IF EXISTS trg_{t}_append_only ON {t};");
                m.Sql($"DROP TRIGGER IF EXISTS trg_{t}_no_truncate ON {t};");
            }
            foreach (var t in TablesDocuments) m.Sql($"DROP TRIGGER IF EXISTS trg_{t}_suppression ON {t};");
            m.Sql("DROP TRIGGER IF EXISTS trg_releves_index_suppression ON releves_index;");
            m.Sql("DROP FUNCTION IF EXISTS ssi_interdire_modification(); DROP FUNCTION IF EXISTS ssi_interdire_suppression_validee(); DROP FUNCTION IF EXISTS ssi_interdire_suppression_releve_valide();");
        }
    }
}
