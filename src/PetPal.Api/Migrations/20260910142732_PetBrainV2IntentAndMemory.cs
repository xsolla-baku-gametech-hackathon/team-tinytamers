using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrainV2IntentAndMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PetIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ReasonKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedEndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RelatedMemoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedMissionKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OutcomeKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PlannerVersion = table.Column<int>(type: "integer", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PetIntents_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PetIntents_ChildProfileId_Status_StartedAt",
                table: "PetIntents",
                columns: new[] { "ChildProfileId", "Status", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PetIntents");
        }
    }
}
