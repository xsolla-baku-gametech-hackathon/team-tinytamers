using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrainPuzzles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PuzzleSeed",
                table: "ExperienceRuns");

            migrationBuilder.CreateTable(
                name: "IssuedPuzzles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageIndex = table.Column<int>(type: "integer", nullable: false),
                    BlueprintKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BlueprintVersion = table.Column<int>(type: "integer", nullable: false),
                    GeneratorVersion = table.Column<int>(type: "integer", nullable: false),
                    Mechanic = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublicPayload = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PrivateSolution = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentSignature = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Assisted = table.Column<bool>(type: "boolean", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    HintsUsed = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedPuzzles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssuedPuzzles_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssuedPuzzles_ExperienceRuns_ExperienceRunId",
                        column: x => x.ExperienceRunId,
                        principalTable: "ExperienceRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedPuzzles_ChildProfileId_IssuedAt",
                table: "IssuedPuzzles",
                columns: new[] { "ChildProfileId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssuedPuzzles_ExperienceRunId_StageIndex",
                table: "IssuedPuzzles",
                columns: new[] { "ExperienceRunId", "StageIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssuedPuzzles");

            migrationBuilder.AddColumn<int>(
                name: "PuzzleSeed",
                table: "ExperienceRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
