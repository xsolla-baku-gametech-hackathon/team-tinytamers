using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetBrainV2Evidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastObservedAt",
                table: "PlayerTraits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStrongEvidenceAt",
                table: "PlayerTraits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObservationCount",
                table: "PlayerTraits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositiveEvidence",
                table: "PlayerTraits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SkipEvidence",
                table: "PlayerTraits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceMask",
                table: "PlayerTraits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SupportCount",
                table: "PetMemories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Tier",
                table: "PetMemories",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastObservedAt",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "LastStrongEvidenceAt",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "ObservationCount",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "PositiveEvidence",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "SkipEvidence",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "SourceMask",
                table: "PlayerTraits");

            migrationBuilder.DropColumn(
                name: "SupportCount",
                table: "PetMemories");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "PetMemories");
        }
    }
}
