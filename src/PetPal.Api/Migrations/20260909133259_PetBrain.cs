using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bond",
                table: "Pets",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.CreateTable(
                name: "BehaviorEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviorEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BehaviorEvents_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExperienceRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ExperienceType = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentStage = table.Column<int>(type: "integer", nullable: false),
                    Choices = table.Column<string>(type: "text", nullable: false),
                    HintsUsed = table.Column<int>(type: "integer", nullable: false),
                    Mistakes = table.Column<int>(type: "integer", nullable: false),
                    ScorePercent = table.Column<int>(type: "integer", nullable: false),
                    PuzzleSeed = table.Column<int>(type: "integer", nullable: false),
                    Assisted = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardApplied = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperienceRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExperienceRuns_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PetMemories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FactKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ValueKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Importance = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PetMemories_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerTraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerTraits_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorEvents_ChildProfileId_IdempotencyKey",
                table: "BehaviorEvents",
                columns: new[] { "ChildProfileId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BehaviorEvents_ChildProfileId_OccurredAt",
                table: "BehaviorEvents",
                columns: new[] { "ChildProfileId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRuns_ChildProfileId_StartedAt",
                table: "ExperienceRuns",
                columns: new[] { "ChildProfileId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceRuns_ChildProfileId_TemplateKey_Status",
                table: "ExperienceRuns",
                columns: new[] { "ChildProfileId", "TemplateKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PetMemories_ChildProfileId_CreatedAt",
                table: "PetMemories",
                columns: new[] { "ChildProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PetMemories_ChildProfileId_Kind_FactKey_ValueKey",
                table: "PetMemories",
                columns: new[] { "ChildProfileId", "Kind", "FactKey", "ValueKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTraits_ChildProfileId_Category_Key",
                table: "PlayerTraits",
                columns: new[] { "ChildProfileId", "Category", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BehaviorEvents");

            migrationBuilder.DropTable(
                name: "ExperienceRuns");

            migrationBuilder.DropTable(
                name: "PetMemories");

            migrationBuilder.DropTable(
                name: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "Bond",
                table: "Pets");
        }
    }
}
