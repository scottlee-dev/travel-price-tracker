using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CancunScraper.Migrations
{
    /// <inheritdoc />
    public partial class FixCheckInDateTypo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChecInDate",
                table: "HotelPrices",
                newName: "CheckInDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CheckInDate",
                table: "HotelPrices",
                newName: "ChecInDate");
        }
    }
}
