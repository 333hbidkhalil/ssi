using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssi.Infrastructure.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class SaisieJournee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "poste_id",
                table: "charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_charges_poste_id",
                table: "charges",
                column: "poste_id");

            migrationBuilder.AddForeignKey(
                name: "fk_charges_postes_travail_poste_id",
                table: "charges",
                column: "poste_id",
                principalTable: "postes_travail",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_charges_postes_travail_poste_id",
                table: "charges");

            migrationBuilder.DropIndex(
                name: "ix_charges_poste_id",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "poste_id",
                table: "charges");
        }
    }
}
