using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class SocialConsentAndPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionAz",
                table: "TeamMissions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleAz",
                table: "TeamMissions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "TeamMissionMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByChildProfileId",
                table: "Friendships",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "Friendships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Friendships",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetChildProfileId",
                table: "Duels",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Duels_TargetChildProfileId",
                table: "Duels",
                column: "TargetChildProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_ChildProfiles_TargetChildProfileId",
                table: "Duels",
                column: "TargetChildProfileId",
                principalTable: "ChildProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // MÖVCUD MƏLUMAT. Yeni sütunların standart dəyəri 0-dır, yəni
            // Pending / Invited. Toxunulmasa, artıq qurulmuş hər dostluq
            // «valideyn təsdiqi gözləyir» vəziyyətinə düşərdi və işləyən
            // komanda missiyalarında töhfələr sayılmağı dayandırardı —
            // razılıq qaydası yalnız BUNDAN SONRAKI əlaqələrə aiddir.
            migrationBuilder.Sql("""
                UPDATE "Friendships"
                SET "Status" = 1,
                    "RequestedByChildProfileId" = "ChildProfileId",
                    "RespondedAt" = "CreatedAt";
                """);

            migrationBuilder.Sql("""UPDATE "TeamMissionMembers" SET "Status" = 1;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Duels_ChildProfiles_TargetChildProfileId",
                table: "Duels");

            migrationBuilder.DropIndex(
                name: "IX_Duels_TargetChildProfileId",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "DescriptionAz",
                table: "TeamMissions");

            migrationBuilder.DropColumn(
                name: "TitleAz",
                table: "TeamMissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TeamMissionMembers");

            migrationBuilder.DropColumn(
                name: "RequestedByChildProfileId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "TargetChildProfileId",
                table: "Duels");
        }
    }
}
