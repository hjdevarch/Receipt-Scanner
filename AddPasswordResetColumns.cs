using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

// Simple console app to manually add the password reset columns
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

var optionsBuilder = new DbContextOptionsBuilder<ReceiptScannerDbContext>();
optionsBuilder.UseSqlServer(connectionString);

using var context = new ReceiptScannerDbContext(optionsBuilder.Options);

try
{
    // Check if columns exist first
    var checkColumnsSql = @"
        SELECT COUNT(*) 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'AspNetUsers' 
        AND COLUMN_NAME IN ('PasswordResetToken', 'PasswordResetTokenExpiryTime')";
    
    var existingColumnsCount = await context.Database.ExecuteSqlRawAsync($"SELECT @@VERSION"); // Test connection first
    
    // Add the missing columns
    var addColumnsSql = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'PasswordResetToken')
        BEGIN
            ALTER TABLE [AspNetUsers] ADD [PasswordResetToken] nvarchar(max) NULL
        END
        
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'PasswordResetTokenExpiryTime')
        BEGIN
            ALTER TABLE [AspNetUsers] ADD [PasswordResetTokenExpiryTime] datetime2 NULL
        END";
    
    await context.Database.ExecuteSqlRawAsync(addColumnsSql);
    
    Console.WriteLine("Password reset columns added successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}