using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;
using ReceiptScanner.Infrastructure.Helper;

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
        // Get max SerialId for the user
        var maxSerialId = await _dbSet
            .Where(m => m.UserId == userId)
            .MaxAsync(m => (int?)m.SerialId) ?? 0;

        // Calculate SerialId threshold based on pagination
        var serialIdThreshold = Math.Max(0, maxSerialId - RWConstants.PageSizeOffsetBySerialId);

        var query = _dbSet
            .Include(m => m.Receipts)
            .Where(m => m.UserId == userId && m.SerialId > serialIdThreshold);

        var totalCount = await _dbSet.Where(m => m.UserId == userId).CountAsync();

        var result = await query
            .OrderByDescending(m => m.SerialId)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (result, totalCount);
    }
}