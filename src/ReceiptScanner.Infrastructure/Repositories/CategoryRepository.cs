using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ReceiptScannerDbContext _context;

    public CategoryRepository(ReceiptScannerDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Category>> GetAllByUserIdAsync(string userId)
    {
        return await _context.Categories
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(Guid id, string userId)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
    }

    public async Task<Category?> GetByNameAsync(string name, string userId)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.Name == name && c.UserId == userId);
    }

    public async Task<Category> AddAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
        await _context.SaveChangesAsync();
        return category;
    }

    public async Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, string userId)
    {
        var category = await GetByIdAsync(id, userId);
        if (category != null)
        {
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(Guid id, string userId)
    {
        return await _context.Categories
            .AnyAsync(c => c.Id == id && c.UserId == userId);
    }
}
