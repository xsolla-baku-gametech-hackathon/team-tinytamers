using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class LocalizationGamesAndScreenTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_Skill_Difficulty_IsActive",
                table: "Questions");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAz",
                table: "WorldZones",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAz",
                table: "WorldZones",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAz",
                table: "Missions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleAz",
                table: "Missions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSprint",
                table: "LearningSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "Questions",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "ChildProfiles",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UtcOffsetMinutes",
                table: "ChildProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAz",
                table: "Badges",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleAz",
                table: "Badges",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    StarsEarned = table.Column<int>(type: "integer", nullable: false),
                    XpEarned = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameResults_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_LanguageCode_Skill_Difficulty_IsActive",
                table: "Questions",
                columns: new[] { "LanguageCode", "Skill", "Difficulty", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_GameResults_ChildProfileId_CreatedAt",
                table: "GameResults",
                columns: new[] { "ChildProfileId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameResults");

            migrationBuilder.DropIndex(
                name: "IX_Questions_LanguageCode_Skill_Difficulty_IsActive",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "DescriptionAz",
                table: "WorldZones");

            migrationBuilder.DropColumn(
                name: "NameAz",
                table: "WorldZones");

            migrationBuilder.DropColumn(
                name: "DescriptionAz",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "TitleAz",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IsSprint",
                table: "LearningSessions");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "UtcOffsetMinutes",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "DescriptionAz",
                table: "Badges");

            migrationBuilder.DropColumn(
                name: "TitleAz",
                table: "Badges");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Skill_Difficulty_IsActive",
                table: "Questions",
                columns: new[] { "Skill", "Difficulty", "IsActive" });
        }
    }
}
