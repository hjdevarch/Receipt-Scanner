using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReceiptScannerDbContext>
{
    public ReceiptScannerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReceiptScannerDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ReceiptScannerDB_Design;Trusted_Connection=true;MultipleActiveResultSets=true");

        return new ReceiptScannerDbContext(optionsBuilder.Options);
    }
}