using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptSummaryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add composite index on UserId and ReceiptDate for optimized summary queries
            migrationBuilder.CreateIndex(
                name: "IX_Receipts_UserId_ReceiptDate",
                table: "Receipts",
                columns: new[] { "UserId", "ReceiptDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_UserId_ReceiptDate",
                table: "Receipts");
        }
    }
}
