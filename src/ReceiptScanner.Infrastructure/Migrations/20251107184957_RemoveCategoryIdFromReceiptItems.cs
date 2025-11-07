using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCategoryIdFromReceiptItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_Categories_CategoryId",
                table: "ReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_CategoryId",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ReceiptItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "ReceiptItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_CategoryId",
                table: "ReceiptItems",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_Categories_CategoryId",
                table: "ReceiptItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
