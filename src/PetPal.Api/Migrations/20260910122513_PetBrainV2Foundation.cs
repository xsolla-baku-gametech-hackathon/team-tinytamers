using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrainV2Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefinitionVersion",
                table: "ExperienceRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Personality",
                table: "ChildProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PersonalityChangedAt",
                table: "ChildProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TraitDailyGains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    TraitKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DayKey = table.Column<int>(type: "integer", nullable: false),
                    Gained = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraitDailyGains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TraitDailyGains_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Qismən unikal indeks mövcud məlumatda POZULA bilər: köhnə kodda
            // "əvvəlcə yoxla, sonra əlavə et" yarışı bir uşağa iki açıq macəra
            // verə bilirdi. Ona görə indeksdən ƏVVƏL hər uşaqda ən son
            // başlayandan başqa açıq run "yarımçıq" olaraq bağlanır.
            //
            // Mükafat və tamamlama məlumatı TOXUNULMUR: yalnız Active (0)
            // sətirlər Abandoned (2) olur, tamamlanmışlara isə heç nə edilmir.
            migrationBuilder.Sql(
                """
                UPDATE "ExperienceRuns" AS r
                SET "Status" = 2,
                    "CompletedAt" = COALESCE(r."CompletedAt", r."StartedAt")
                WHERE r."Status" = 0
                  AND r."Id" <> (
                      SELECT newest."Id"
                      FROM "ExperienceRuns" AS newest
                      WHERE newest."ChildProfileId" = r."ChildProfileId"
                        AND newest."Status" = 0
                      ORDER BY newest."StartedAt" DESC, newest."Id" DESC
                      LIMIT 1
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRuns_ChildProfileId_Active",
                table: "ExperienceRuns",
                column: "ChildProfileId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TraitDailyGains_ChildProfileId_Category_TraitKey_DayKey",
                table: "TraitDailyGains",
                columns: new[] { "ChildProfileId", "Category", "TraitKey", "DayKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TraitDailyGains");

            migrationBuilder.DropIndex(
                name: "IX_ExperienceRuns_ChildProfileId_Active",
                table: "ExperienceRuns");

            migrationBuilder.DropColumn(
                name: "DefinitionVersion",
                table: "ExperienceRuns");

            migrationBuilder.DropColumn(
                name: "Personality",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "PersonalityChangedAt",
                table: "ChildProfiles");
        }
    }
}
