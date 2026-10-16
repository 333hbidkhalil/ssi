using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssi.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class BonCommandeClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reference_commande",
                table: "reglements_poste",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reference_commande",
                table: "reglements_poste");
        }
    }
}
