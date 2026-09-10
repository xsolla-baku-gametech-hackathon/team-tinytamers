using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdventureEngineV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CompletedWithAssist",
                table: "IssuedPuzzles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdventureRunStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentChapterId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CheckpointNodeId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CheckpointChapterId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CheckpointAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Variant = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    TotalPlaySeconds = table.Column<int>(type: "integer", nullable: false),
                    LastPlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PausedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VisitedNodeIds = table.Column<string>(type: "text", nullable: false),
                    CompletedChapterIds = table.Column<string>(type: "text", nullable: false),
                    WorldFlags = table.Column<string>(type: "text", nullable: false),
                    SelectedChoiceIds = table.Column<string>(type: "text", nullable: false),
                    Inventory = table.Column<string>(type: "text", nullable: false),
                    Clues = table.Column<string>(type: "text", nullable: false),
                    Objectives = table.Column<string>(type: "text", nullable: false),
                    EndingScores = table.Column<string>(type: "text", nullable: false),
                    RetryCounts = table.Column<string>(type: "text", nullable: false),
                    NpcStates = table.Column<string>(type: "text", nullable: false),
                    AppliedActionKeys = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdventureRunStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdventureRunStates_ExperienceRuns_ExperienceRunId",
                        column: x => x.ExperienceRunId,
                        principalTable: "ExperienceRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdventureRunStates_ChildProfileId_LastPlayedAt",
                table: "AdventureRunStates",
                columns: new[] { "ChildProfileId", "LastPlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdventureRunStates_ExperienceRunId",
                table: "AdventureRunStates",
                column: "ExperienceRunId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdventureRunStates");

            migrationBuilder.DropColumn(
                name: "CompletedWithAssist",
                table: "IssuedPuzzles");
        }
    }
}
