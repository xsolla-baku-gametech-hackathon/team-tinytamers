using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrainPersonalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContinuityFit",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FilteredCandidates",
                table: "RecommendationDecisions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "RecommendationDecisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "MasteryChallengeFit",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MechanicFit",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaceFit",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProfileConfidence",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RepetitionPenalty",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RewardFit",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Slot",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SupportFit",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TopicFit",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalScore",
                table: "RecommendationDecisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "WasExploration",
                table: "RecommendationDecisions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "WhyReasons",
                table: "RecommendationDecisions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExposureCount",
                table: "PlayerTraits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModelVersion",
                table: "PlayerTraits",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "NegativeEvidence",
                table: "PlayerTraits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContentPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RepeatCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentPreferences_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MechanicMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mechanic = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EstimatedLevel = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Successes = table.Column<int>(type: "integer", nullable: false),
                    AssistedSuccesses = table.Column<int>(type: "integer", nullable: false),
                    RecentTrend = table.Column<int>(type: "integer", nullable: false),
                    LastPracticedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModelVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MechanicMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MechanicMasteries_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonalizationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonalizationEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AiNarrativeEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SurpriseEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SessionLength = table.Column<int>(type: "integer", nullable: false),
                    SessionLengthSource = table.Column<int>(type: "integer", nullable: false),
                    Pace = table.Column<int>(type: "integer", nullable: false),
                    PaceSource = table.Column<int>(type: "integer", nullable: false),
                    NoveltyTolerance = table.Column<int>(type: "integer", nullable: false),
                    NoveltyToleranceSource = table.Column<int>(type: "integer", nullable: false),
                    HintStyle = table.Column<int>(type: "integer", nullable: false),
                    HintStyleSource = table.Column<int>(type: "integer", nullable: false),
                    HintTiming = table.Column<int>(type: "integer", nullable: false),
                    HintTimingSource = table.Column<int>(type: "integer", nullable: false),
                    ReducedOptions = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraResponseTime = table.Column<bool>(type: "boolean", nullable: false),
                    DemonstrationFirst = table.Column<bool>(type: "boolean", nullable: false),
                    ReducedMotion = table.Column<bool>(type: "boolean", nullable: false),
                    LargeText = table.Column<bool>(type: "boolean", nullable: false),
                    HighContrast = table.Column<bool>(type: "boolean", nullable: false),
                    IconWithText = table.Column<bool>(type: "boolean", nullable: false),
                    Narration = table.Column<bool>(type: "boolean", nullable: false),
                    Subtitles = table.Column<bool>(type: "boolean", nullable: false),
                    ReadingLevel = table.Column<int>(type: "integer", nullable: false),
                    ReadingLevelSource = table.Column<int>(type: "integer", nullable: false),
                    RewardPreference = table.Column<int>(type: "integer", nullable: false),
                    RewardPreferenceSource = table.Column<int>(type: "integer", nullable: false),
                    OnboardingCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OnboardingSkippedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalizationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalizationSettings_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationDecisions_GroupId",
                table: "RecommendationDecisions",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentPreferences_ChildProfileId_Scope_Key",
                table: "ContentPreferences",
                columns: new[] { "ChildProfileId", "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MechanicMasteries_ChildProfileId_Mechanic",
                table: "MechanicMasteries",
                columns: new[] { "ChildProfileId", "Mechanic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalizationSettings_ChildProfileId",
                table: "PersonalizationSettings",
                column: "ChildProfileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentPreferences");

            migrationBuilder.DropTable(
                name: "MechanicMasteries");

            migrationBuilder.DropTable(
                name: "PersonalizationSettings");

            migrationBuilder.DropIndex(
                name: "IX_RecommendationDecisions_GroupId",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "ContinuityFit",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "FilteredCandidates",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "MasteryChallengeFit",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "MechanicFit",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "PaceFit",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "ProfileConfidence",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "RepetitionPenalty",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "RewardFit",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "Slot",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "SupportFit",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "TopicFit",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "TotalScore",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "WasExploration",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "WhyReasons",
                table: "RecommendationDecisions");

            migrationBuilder.DropColumn(
                name: "ExposureCount",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "ModelVersion",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "NegativeEvidence",
                table: "PlayerTraits");
        }
    }
}
