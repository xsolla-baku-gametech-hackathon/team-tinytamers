using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetAccessoryEquipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EquippedAccessories",
                table: "Pets",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Mövcud pet-lər açılmış əşyalarının hamısını taxmış sayılır — yeniləmədən
            // sonra heç kimin peti "soyunmuş" görünməməlidir. Hər iki sütun eyni JSON
            // formatındadır, ona görə birbaşa köçürmək kifayətdir.
            migrationBuilder.Sql("""
                UPDATE "Pets"
                SET "EquippedAccessories" = "UnlockedAccessories"
                WHERE "UnlockedAccessories" IS NOT NULL AND "UnlockedAccessories" <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquippedAccessories",
                table: "Pets");
        }
    }
}
