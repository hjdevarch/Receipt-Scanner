using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.Infrastructure.Repositories;

public class ReceiptRepository : BaseRepository<Receipt>, IReceiptRepository
{
    public ReceiptRepository(ReceiptScannerDbContext context) : base(context)
    {
    }

    public override async Task<Receipt?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public override async Task<IEnumerable<Receipt>> GetAllAsync()
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Receipt>> GetByMerchantIdAsync(Guid merchantId)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items)
            .Where(r => r.MerchantId == merchantId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Receipt>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items)
            .Where(r => r.ReceiptDate >= startDate && r.ReceiptDate <= endDate)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();
    }

    public async Task<Receipt?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Receipt>> GetByStatusAsync(ReceiptStatus status)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items)
            .Where(r => r.Status == status)
            .ToListAsync();
    }
}