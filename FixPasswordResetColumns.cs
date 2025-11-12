using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

Console.WriteLine("Adding password reset columns to database...");

try
{
    // Read configuration from the API project
    var apiProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "ReceiptScanner.API");
    
    var configuration = new ConfigurationBuilder()
        .SetBasePath(apiProjectPath)
        .AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Development.json", optional: true)
        .Build();

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("Error: Could not find connection string");
        Console.WriteLine($"Looked in: {apiProjectPath}");
        return;
    }

    Console.WriteLine("Found connection string, connecting to database...");

    var optionsBuilder = new DbContextOptionsBuilder<ReceiptScannerDbContext>();
    optionsBuilder.UseSqlServer(connectionString);

    using var context = new ReceiptScannerDbContext(optionsBuilder.Options);

    // Test connection
    await context.Database.CanConnectAsync();
    Console.WriteLine("Database connection successful!");

    // Execute the SQL to add columns
    var sqlCommands = new[]
    {
        "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'PasswordResetToken') ALTER TABLE [AspNetUsers] ADD [PasswordResetToken] nvarchar(max) NULL;",
        "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AspNetUsers' AND COLUMN_NAME = 'PasswordResetTokenExpiryTime') ALTER TABLE [AspNetUsers] ADD [PasswordResetTokenExpiryTime] datetime2 NULL;"
    };

    foreach (var sql in sqlCommands)
    {
        await context.Database.ExecuteSqlRawAsync(sql);
        Console.WriteLine("Executed SQL command successfully");
    }

    // Update migration history
    var migrationSql = "IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20251112142357_AddPasswordResetTokenFields') INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20251112142357_AddPasswordResetTokenFields', N'9.0.10');";
    await context.Database.ExecuteSqlRawAsync(migrationSql);
    
    Console.WriteLine("Password reset columns added successfully!");
    Console.WriteLine("Migration history updated!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
    }
}