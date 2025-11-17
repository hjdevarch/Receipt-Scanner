using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSerialIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SerialId",
                table: "Receipts",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "SerialId",
                table: "ReceiptItems",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "SerialId",
                table: "Merchants",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerialId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "SerialId",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "SerialId",
                table: "Merchants");
        }
    }
}
