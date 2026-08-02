using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenealogyToAnimals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "birthing_id",
                schema: "livestock",
                table: "animals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "father_animal_id",
                schema: "livestock",
                table: "animals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "father_straw_id",
                schema: "livestock",
                table: "animals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "mother_id",
                schema: "livestock",
                table: "animals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_animals_father_animal_id",
                schema: "livestock",
                table: "animals",
                column: "father_animal_id");

            migrationBuilder.CreateIndex(
                name: "i_x_animals_father_straw_id",
                schema: "livestock",
                table: "animals",
                column: "father_straw_id");

            migrationBuilder.CreateIndex(
                name: "i_x_animals_mother_id",
                schema: "livestock",
                table: "animals",
                column: "mother_id");

            migrationBuilder.AddForeignKey(
                name: "f_k_animals_animals_father_animal_id",
                schema: "livestock",
                table: "animals",
                column: "father_animal_id",
                principalSchema: "livestock",
                principalTable: "animals",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "f_k_animals_animals_mother_id",
                schema: "livestock",
                table: "animals",
                column: "mother_id",
                principalSchema: "livestock",
                principalTable: "animals",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "f_k_animals_animals_father_animal_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropForeignKey(
                name: "f_k_animals_animals_mother_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropIndex(
                name: "i_x_animals_father_animal_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropIndex(
                name: "i_x_animals_father_straw_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropIndex(
                name: "i_x_animals_mother_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropColumn(
                name: "birthing_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropColumn(
                name: "father_animal_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropColumn(
                name: "father_straw_id",
                schema: "livestock",
                table: "animals");

            migrationBuilder.DropColumn(
                name: "mother_id",
                schema: "livestock",
                table: "animals");
        }
    }
}
