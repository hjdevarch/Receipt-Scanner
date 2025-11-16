using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.Infrastructure.Repositories;

public class MerchantRepository : BaseRepository<Merchant>, IMerchantRepository
{
    public MerchantRepository(ReceiptScannerDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Merchant>> GetAllByUserIdAsync(string userId)
    {
        return await _dbSet
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<Merchant?> GetByNameAsync(string name, string userId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(m => m.Name.ToLower() == name.ToLower() && m.UserId == userId);
    }

    public async Task<IEnumerable<Merchant>> SearchByNameAsync(string searchTerm, string userId)
    {
        return await _dbSet
            .Where(m => m.Name.ToLower().Contains(searchTerm.ToLower()) && m.UserId == userId)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }


    public async Task<IEnumerable<Merchant>> GetAllWithReceiptTotalsAsync(string userId)
    {
        return await _dbSet
            .Include(m => m.Receipts)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }
    
    public async Task<(IEnumerable<Merchant> Merchants,int TotalCount)> GetAllWithReceiptTotalsPagedAsync(string userId,int skip,int take)
    {
        var query = _dbSet
            .Include(m => m.Receipts)
            .Where(m => m.UserId == userId);

        var totalCount = await query.CountAsync();

        var result = await query
            .OrderBy(m => m.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (result, totalCount);
    }
}