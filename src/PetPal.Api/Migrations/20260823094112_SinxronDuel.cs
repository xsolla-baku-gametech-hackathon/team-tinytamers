using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class SinxronDuel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Duels",
                type: "timestamp with time zone",
                nullable: true);

            // Köhnə ASENXRON duellər yeni qaydaya uyğun gəlmir: onlarda dəstin
            // ortaq başlama anı yoxdur və bir uşaq artıq tək oynayıb. Belə duel
            // qalsaydı, həmin uşaq cavab verə bilməyib ilişərdi. Ona görə açıq
            // qalanlar ləğv olunur (Status 0 = WaitingOpponent → 2 = Expired).
            // Tamamlanmış duellərə və onların nəticələrinə TOXUNULMUR.
            migrationBuilder.Sql("""UPDATE "Duels" SET "Status" = 2 WHERE "Status" = 0;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Duels");
        }
    }
}
