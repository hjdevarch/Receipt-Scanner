using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultCurrencyName = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DefaultCurrencySymbol = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            // Seed default settings with GBP currency
            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "DefaultCurrencyName", "DefaultCurrencySymbol", "CreatedAt" },
                values: new object[] { Guid.NewGuid(), "GBP", "£", DateTime.UtcNow });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
