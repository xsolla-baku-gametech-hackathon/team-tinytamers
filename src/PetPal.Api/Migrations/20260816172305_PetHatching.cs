using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PetPal.Api.Migrations
{
    /// <inheritdoc />
    public partial class PetHatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HatchedAt",
                table: "Pets",
                type: "timestamp with time zone",
                nullable: true);

            // Bu mexanika yenidir. Artıq 1-ci səviyyəni keçmiş pet-i yumurtaya
            // qaytarmaq uşağın qazandığını geri almaq olardı — onlar açılmış sayılır.
            migrationBuilder.Sql("""UPDATE "Pets" SET "HatchedAt" = NOW() WHERE "Level" > 1;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HatchedAt",
                table: "Pets");
        }
    }
}
