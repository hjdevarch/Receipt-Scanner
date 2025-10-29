using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ReceiptScanner.Infrastructure.Data;
using System;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("src/ReceiptScanner.API/appsettings.json")
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");

var options = new DbContextOptionsBuilder<ReceiptScannerDbContext>()
    .UseSqlServer(connectionString)
    .Options;

try
{
    using var context = new ReceiptScannerDbContext(options);
    
    Console.WriteLine("Testing database connection...");
    
    // Test if we can connect to the database
    var canConnect = await context.Database.CanConnectAsync();
    Console.WriteLine($"Can connect: {canConnect}");
    
    // Check if tables exist
    var tableExists = await context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Receipts'") > 0;
    Console.WriteLine($"Receipts table exists: {tableExists}");
    
    // Try to get count of receipts
    var receiptCount = await context.Receipts.CountAsync();
    Console.WriteLine($"Receipt count: {receiptCount}");
    
    if (receiptCount > 0)
    {
        Console.WriteLine("Attempting to fetch first receipt...");
        
        // Try to get first receipt without includes to see if the base data is the issue
        var firstReceiptNoIncludes = await context.Receipts.FirstOrDefaultAsync();
        Console.WriteLine($"First receipt without includes: {firstReceiptNoIncludes?.Id}");
        
        // Now try with includes to see where the error occurs
        Console.WriteLine("Attempting to fetch first receipt with includes...");
        var firstReceiptWithIncludes = await context.Receipts
            .Include(r => r.Merchant)
            .Include(r => r.Items)
            .FirstOrDefaultAsync();
        Console.WriteLine($"First receipt with includes: {firstReceiptWithIncludes?.Id}");
    }
    
    Console.WriteLine("Test completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
    }
}