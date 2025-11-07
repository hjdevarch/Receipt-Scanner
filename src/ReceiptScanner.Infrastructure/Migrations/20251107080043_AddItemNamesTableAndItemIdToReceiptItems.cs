using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemNamesTableAndItemIdToReceiptItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemId",
                table: "ReceiptItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ItemNames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemNames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemNames_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ItemId",
                table: "ReceiptItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemNames_CategoryId",
                table: "ItemNames",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemNames_Name",
                table: "ItemNames",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItems_ItemNames_ItemId",
                table: "ReceiptItems",
                column: "ItemId",
                principalTable: "ItemNames",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItems_ItemNames_ItemId",
                table: "ReceiptItems");

            migrationBuilder.DropTable(
                name: "ItemNames");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_ItemId",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "ReceiptItems");
        }
    }
}
