using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToAllTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Delete all existing data from tables since we can't assign them to a user
            migrationBuilder.Sql("DELETE FROM ReceiptItems");
            migrationBuilder.Sql("DELETE FROM Receipts");
            migrationBuilder.Sql("DELETE FROM Merchants");
            migrationBuilder.Sql("DELETE FROM Settings");

            // Step 2: Add UserId columns as required
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Settings",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Receipts",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ReceiptItems",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Merchants",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_UserId",
                table: "Settings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_UserId",
                table: "Receipts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_UserId",
                table: "ReceiptItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Merchants_UserId",
                table: "Merchants",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Merchants_AspNetUsers_UserId",
                table: "Merchants",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_AspNetUsers_UserId",
                table: "ReceiptItems",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_AspNetUsers_UserId",
                table: "Receipts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Settings_AspNetUsers_UserId",
                table: "Settings",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Merchants_AspNetUsers_UserId",
                table: "Merchants");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_AspNetUsers_UserId",
                table: "ReceiptItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_AspNetUsers_UserId",
                table: "Receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Settings_AspNetUsers_UserId",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Settings_UserId",
                table: "Settings");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_UserId",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_UserId",
                table: "ReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_Merchants_UserId",
                table: "Merchants");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Merchants");
        }
    }
}
