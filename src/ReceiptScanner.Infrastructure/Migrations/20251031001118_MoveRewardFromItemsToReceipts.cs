using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveRewardFromItemsToReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reward",
                table: "ReceiptItems");

            migrationBuilder.AddColumn<decimal>(
                name: "Reward",
                table: "Receipts",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reward",
                table: "Receipts");

            migrationBuilder.AddColumn<decimal>(
                name: "Reward",
                table: "ReceiptItems",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
