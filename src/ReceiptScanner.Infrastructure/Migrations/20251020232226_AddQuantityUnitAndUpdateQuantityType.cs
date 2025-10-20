using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantityUnitAndUpdateQuantityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ReceiptItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "QuantityUnit",
                table: "ReceiptItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuantityUnit",
                table: "ReceiptItems");

            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "ReceiptItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }
    }
}
