using Bogus;
using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Infrastructure.Data;

public class DatabaseSeeder
{
    private readonly ReceiptScannerDbContext _context;

    public DatabaseSeeder(ReceiptScannerDbContext context)
    {
        _context = context;
    }

    public async Task SeedDummyDataAsync(string userId, int receiptsCount, int maxReceiptItemsCount, string currency = "USD")
    {
        // Check if user exists
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            throw new ArgumentException($"User with ID '{userId}' does not exist in the database.");
        }

        // Clear all existing user data first (except credentials and user details)
        Console.WriteLine($"Clearing existing data for user {userId}...");
        await ClearDummyDataAsync(userId);
        Console.WriteLine($"Data cleared. Starting to seed new data...");

        var faker = new Faker();

        // Create merchants first (reuse them across receipts)
        var merchantFaker = new Faker<Merchant>()
            .CustomInstantiator(f => new Merchant(
                name: f.Company.CompanyName(),
                userId: userId,
                address: f.Address.FullAddress(),
                phoneNumber: f.Phone.PhoneNumber("###-###-####"), // Format to fit 20 char limit
                email: f.Internet.Email(),
                website: f.Internet.Url()
            ));

        var merchants = new List<Merchant>();
        for (int i = 0; i < Math.Min(20, receiptsCount / 5); i++) // Create 20 merchants or fewer
        {
            merchants.Add(merchantFaker.Generate());
        }
        await _context.Merchants.AddRangeAsync(merchants);
        await _context.SaveChangesAsync();

        // Create receipts with items
        var receiptFaker = new Faker<Receipt>()
            .CustomInstantiator(f =>
            {
                var merchant = f.PickRandom(merchants);
                var receiptDate = f.Date.Between(DateTime.Now.AddYears(-2), DateTime.Now);
                var subTotal = f.Finance.Amount(10, 500);
                var taxAmount = subTotal * 0.1m; // 10% tax
                var totalAmount = subTotal + taxAmount;
                var reward = f.Random.Bool(0.3f) ? (decimal?)f.Finance.Amount(1, 50) : null; // 30% chance of reward

                return new Receipt(
                    receiptNumber: f.Random.AlphaNumeric(10).ToUpper(),
                    receiptDate: receiptDate,
                    subTotal: subTotal,
                    taxAmount: taxAmount,
                    totalAmount: totalAmount,
                    merchantId: merchant.Id,
                    userId: userId,
                    currency: currency,
                    imagePath: null,
                    rawText: f.Lorem.Paragraph(),
                    reward: reward
                );
            });

        var categoryOptions = new[]
        {
            "Groceries", "Electronics", "Clothing", "Home & Garden", "Sports & Outdoors",
            "Books", "Toys", "Health & Beauty", "Automotive", "Office Supplies",
            "Pet Supplies", "Food & Beverage", "Entertainment", "Travel", "Services"
        };

        var itemNameFaker = new Faker<string>()
            .CustomInstantiator(f => f.Commerce.ProductName());

        Console.WriteLine($"Seeding {receiptsCount} receipts with up to {maxReceiptItemsCount} items each (Currency: {currency})...");

        // Generate all receipts first and order by date ascending
        var generatedReceipts = receiptFaker.Generate(receiptsCount)
            .OrderBy(r => r.ReceiptDate)
            .ToList();

        for (int i = 0; i < generatedReceipts.Count; i++)
        {
            var receipt = generatedReceipts[i];
            await _context.Receipts.AddAsync(receipt);
            await _context.SaveChangesAsync(); // Save to generate Receipt ID

            // Create random number of items for this receipt
            var itemCount = faker.Random.Int(1, maxReceiptItemsCount);
            var receiptItems = new List<ReceiptItem>();

            for (int j = 0; j < itemCount; j++)
            {
                var quantity = faker.Random.Decimal(1, 10);
                var unitPrice = faker.Finance.Amount(1, 100);
                var totalPrice = quantity * unitPrice;

                var item = new ReceiptItem(
                    name: faker.Commerce.ProductName(),
                    quantity: quantity,
                    unitPrice: unitPrice,
                    receiptId: receipt.Id,
                    userId: userId,
                    description: faker.Commerce.ProductDescription(),
                    category: faker.PickRandom(categoryOptions),
                    sku: faker.Random.AlphaNumeric(8).ToUpper(),
                    quantityUnit: faker.PickRandom(new[] { "pcs", "kg", "lb", "L", "gal", "box", "pack" }),
                    totalPrice: totalPrice
                );

                receiptItems.Add(item);
            }

            await _context.ReceiptItems.AddRangeAsync(receiptItems);
            await _context.SaveChangesAsync();

            // Update receipt status to Processed
            receipt.UpdateStatus(ReceiptStatus.Processed);
            await _context.SaveChangesAsync();

            // Progress indicator
            if ((i + 1) % 100 == 0)
            {
                Console.WriteLine($"Seeded {i + 1}/{receiptsCount} receipts...");
            }
        }

        Console.WriteLine($"Successfully seeded {receiptsCount} receipts with items for user {userId}");
    }

    public async Task ClearDummyDataAsync(string userId)
    {
        Console.WriteLine($"Clearing all data for user {userId} (except credentials)...");

        // Delete receipt items first (foreign key constraint)
        var receiptItems = await _context.ReceiptItems
            .Where(ri => ri.UserId == userId)
            .ToListAsync();
        _context.ReceiptItems.RemoveRange(receiptItems);
        await _context.SaveChangesAsync();
        Console.WriteLine($"  Deleted {receiptItems.Count} receipt items");

        // Delete receipts
        var receipts = await _context.Receipts
            .Where(r => r.UserId == userId)
            .ToListAsync();
        _context.Receipts.RemoveRange(receipts);
        await _context.SaveChangesAsync();
        Console.WriteLine($"  Deleted {receipts.Count} receipts");

        // Delete merchants
        var merchants = await _context.Merchants
            .Where(m => m.UserId == userId)
            .ToListAsync();
        _context.Merchants.RemoveRange(merchants);
        await _context.SaveChangesAsync();
        Console.WriteLine($"  Deleted {merchants.Count} merchants");

        // Delete categories
        var categories = await _context.Categories
            .Where(c => c.UserId == userId)
            .ToListAsync();
        _context.Categories.RemoveRange(categories);
        await _context.SaveChangesAsync();
        Console.WriteLine($"  Deleted {categories.Count} categories");

        // Delete ItemNames (no UserId, but should be cleared for fresh start)
        var itemNames = await _context.Set<ItemName>()
            .ToListAsync();
        _context.Set<ItemName>().RemoveRange(itemNames);
        await _context.SaveChangesAsync();
        Console.WriteLine($"  Deleted {itemNames.Count} item names");

        // Delete settings
        var settings = await _context.Settings
            .Where(s => s.UserId == userId)
            .ToListAsync();
        _context.Settings.RemoveRange(settings);
        await _context.SaveChangesAsync();
        Console.WriteLine($"  Deleted {settings.Count} settings entries");

        Console.WriteLine($"Cleared all data for user {userId}");
    }
}
