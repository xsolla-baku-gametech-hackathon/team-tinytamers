using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrainRecapVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdventureRecaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecapSpecHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SpecVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderJobId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Model = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PromptTemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    PromptHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssetKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: false),
                    EstimatedCredits = table.Column<int>(type: "integer", nullable: false),
                    RealizedCredits = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdventureRecaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdventureRecaps_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdventureRecaps_ChildProfileId_RequestedAt",
                table: "AdventureRecaps",
                columns: new[] { "ChildProfileId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdventureRecaps_ExperienceRunId",
                table: "AdventureRecaps",
                column: "ExperienceRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AdventureRecaps_RecapSpecHash",
                table: "AdventureRecaps",
                column: "RecapSpecHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdventureRecaps");
        }
    }
}
