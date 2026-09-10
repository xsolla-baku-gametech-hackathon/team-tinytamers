using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class BilikArenasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ArenaEnabled",
                table: "ChildProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ArenaFriendsOnly",
                table: "ChildProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ArenaRating",
                table: "ChildProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.CreateTable(
                name: "Duels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Skill = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorArenaRating = table.Column<int>(type: "integer", nullable: false),
                    FriendsOnly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Duels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Duels_ChildProfiles_CreatedByChildProfileId",
                        column: x => x.CreatedByChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DuelEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DuelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    TotalMilliseconds = table.Column<int>(type: "integer", nullable: false),
                    ArenaRatingBefore = table.Column<int>(type: "integer", nullable: false),
                    ArenaRatingAfter = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    StarsEarned = table.Column<int>(type: "integer", nullable: false),
                    ResultSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelEntries_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DuelEntries_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DuelQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DuelId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelQuestions_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DuelQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DuelAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DuelEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    ChosenIndex = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    ElapsedMs = table.Column<int>(type: "integer", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelAnswers_DuelEntries_DuelEntryId",
                        column: x => x.DuelEntryId,
                        principalTable: "DuelEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DuelAnswers_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DuelAnswers_DuelEntryId_QuestionId",
                table: "DuelAnswers",
                columns: new[] { "DuelEntryId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuelAnswers_QuestionId",
                table: "DuelAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_DuelEntries_ChildProfileId_StartedAt",
                table: "DuelEntries",
                columns: new[] { "ChildProfileId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DuelEntries_DuelId_ChildProfileId",
                table: "DuelEntries",
                columns: new[] { "DuelId", "ChildProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuelQuestions_DuelId_Order",
                table: "DuelQuestions",
                columns: new[] { "DuelId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuelQuestions_QuestionId",
                table: "DuelQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_CreatedByChildProfileId",
                table: "Duels",
                column: "CreatedByChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_Status_ExpiresAt_CreatedAt",
                table: "Duels",
                columns: new[] { "Status", "ExpiresAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuelAnswers");

            migrationBuilder.DropTable(
                name: "DuelQuestions");

            migrationBuilder.DropTable(
                name: "DuelEntries");

            migrationBuilder.DropTable(
                name: "Duels");

            migrationBuilder.DropColumn(
                name: "ArenaEnabled",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "ArenaFriendsOnly",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "ArenaRating",
                table: "ChildProfiles");
        }
    }
}
