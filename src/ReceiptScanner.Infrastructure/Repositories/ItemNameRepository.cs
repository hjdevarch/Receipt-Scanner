using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.Infrastructure.Repositories;

public class ItemNameRepository : IItemNameRepository
{
    private readonly ReceiptScannerDbContext _context;
    private readonly DbSet<ItemName> _dbSet;

    public ItemNameRepository(ReceiptScannerDbContext context)
    {
        _context = context;
        _dbSet = context.Set<ItemName>();
    }

    public async Task<ItemName?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<ItemName?> GetByNameAsync(string name)
    {
        return await _dbSet
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Name == name);
    }

    public async Task<IEnumerable<ItemName>> GetAllAsync()
    {
        return await _dbSet
            .Include(i => i.Category)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<ItemName>> GetUncategorizedAsync()
    {
        return await _dbSet
            .Where(i => i.CategoryId == null)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<ItemName> AddAsync(ItemName itemName)
    {
        await _dbSet.AddAsync(itemName);
        await _context.SaveChangesAsync();
        return itemName;
    }

    public async Task<ItemName> UpdateAsync(ItemName itemName)
    {
        _dbSet.Update(itemName);
        await _context.SaveChangesAsync();
        return itemName;
    }

    public async Task DeleteAsync(int id)
    {
        var itemName = await GetByIdAsync(id);
        if (itemName != null)
        {
            _dbSet.Remove(itemName);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string name)
    {
        return await _dbSet.AnyAsync(i => i.Name == name);
    }
}
