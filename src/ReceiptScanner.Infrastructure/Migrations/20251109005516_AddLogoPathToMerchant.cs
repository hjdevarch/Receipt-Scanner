using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLogoPathToMerchant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                table: "Merchants",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoPath",
                table: "Merchants");
        }
    }
}
