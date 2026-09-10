using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrainV2StoryGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentNodeId",
                table: "ExperienceRuns",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DecisionId",
                table: "ExperienceRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndingKey",
                table: "ExperienceRuns",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoryFlags",
                table: "ExperienceRuns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RecommendationDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContextHash = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CandidateKeys = table.Column<string>(type: "text", nullable: false),
                    SelectedTemplateKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FitScore = table.Column<int>(type: "integer", nullable: false),
                    NoveltyScore = table.Column<int>(type: "integer", nullable: false),
                    SurpriseScore = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Feedback = table.Column<int>(type: "integer", nullable: false),
                    FeedbackAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecommendationDecisions_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RunStageOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StageOrdinal = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SelectedOptionKeys = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    HintsUsed = table.Column<int>(type: "integer", nullable: false),
                    EffectKeys = table.Column<string>(type: "text", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunStageOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RunStageOutcomes_ExperienceRuns_ExperienceRunId",
                        column: x => x.ExperienceRunId,
                        principalTable: "ExperienceRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationDecisions_ChildProfileId_CreatedAt",
                table: "RecommendationDecisions",
                columns: new[] { "ChildProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RunStageOutcomes_ExperienceRunId_NodeId",
                table: "RunStageOutcomes",
                columns: new[] { "ExperienceRunId", "NodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RunStageOutcomes_ExperienceRunId_StageOrdinal",
                table: "RunStageOutcomes",
                columns: new[] { "ExperienceRunId", "StageOrdinal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecommendationDecisions");

            migrationBuilder.DropTable(
                name: "RunStageOutcomes");

            migrationBuilder.DropColumn(
                name: "CurrentNodeId",
                table: "ExperienceRuns");

            migrationBuilder.DropColumn(
                name: "DecisionId",
                table: "ExperienceRuns");

            migrationBuilder.DropColumn(
                name: "EndingKey",
                table: "ExperienceRuns");

            migrationBuilder.DropColumn(
                name: "StoryFlags",
                table: "ExperienceRuns");
        }
    }
}
