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
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public override async Task<IEnumerable<Receipt>> GetAllAsync()
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Receipt>> GetAllByUserIdAsync(string userId)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt)).ThenInclude(ri => ri.Item).ThenInclude(g => g!.Category)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Receipt> Items, int TotalCount)> GetAllByUserIdPagedAsync(string userId, int skip, int take)
    {
        var query = _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt)).ThenInclude(ri => ri.Item).ThenInclude(g => g!.Category)
            .Where(r => r.UserId == userId);

        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Receipt>> GetByMerchantIdAsync(Guid merchantId, string userId)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .Where(r => r.MerchantId == merchantId && r.UserId == userId)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Receipt> Items, int TotalCount)> GetByMerchantIdPagedAsync(Guid merchantId, string userId, int skip, int take)
    {
        var query = _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt)).ThenInclude(ri => ri.Item).ThenInclude(g => g!.Category)
            .Where(r => r.MerchantId == merchantId && r.UserId == userId);

        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<Receipt>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, string userId)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .Where(r => r.ReceiptDate >= startDate && r.ReceiptDate <= endDate && r.UserId == userId)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Receipt> Items, int TotalCount)> GetByDateRangePagedAsync(DateTime startDate, DateTime endDate, string userId, int skip, int take)
    {
        var query = _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt)).ThenInclude(ri => ri.Item).ThenInclude(g => g!.Category)
            .Where(r => r.ReceiptDate >= startDate && r.ReceiptDate <= endDate && r.UserId == userId);

        var totalCount = await query.CountAsync();
        
        var items = await query
            .OrderByDescending(r => r.ReceiptDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Receipt?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Receipt>> GetByStatusAsync(ReceiptStatus status, string userId)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .Where(r => r.Status == status && r.UserId == userId)
            .ToListAsync();
    }

    public async Task DeleteReceiptItemsAsync(Guid receiptId)
    {
        // Detach any tracked items for this receipt so EF Core does not try to update them later
        var trackedItems = _context.ChangeTracker
            .Entries<ReceiptItem>()
            .Where(e => e.Entity.ReceiptId == receiptId)
            .ToList();

        foreach (var entry in trackedItems)
        {
            entry.State = EntityState.Detached;
        }

        // Use raw SQL to delete all items for the receipt to avoid change-tracking conflicts
        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM ReceiptItems WHERE ReceiptId = {0}",
            receiptId);
    }

    public async Task<Receipt?> GetReceiptByItemIdAsync(Guid receiptItemId)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .ThenInclude(ri => ri.Item)
            .Where(r => r.Items.Any(i => i.Id == receiptItemId))
            .FirstOrDefaultAsync();
    }

    public async Task<(decimal Total, decimal ThisYear, decimal ThisMonth, decimal ThisWeek)> GetReceiptSummaryAsync(
        string userId, 
        DateTime startOfYear, 
        DateTime startOfMonth, 
        DateTime startOfWeek)
    {
        // Use a single query with conditional aggregation for better performance
        var summary = await _dbSet
            .Where(r => r.UserId == userId)
            .GroupBy(r => 1) // Group all records together
            .Select(g => new
            {
                Total = g.Sum(r => r.TotalAmount),
                ThisYear = g.Where(r => r.ReceiptDate >= startOfYear).Sum(r => r.TotalAmount),
                ThisMonth = g.Where(r => r.ReceiptDate >= startOfMonth).Sum(r => r.TotalAmount),
                ThisWeek = g.Where(r => r.ReceiptDate >= startOfWeek).Sum(r => r.TotalAmount)
            })
            .FirstOrDefaultAsync();
        
        if (summary == null)
        {
            return (0, 0, 0, 0);
        }
        
        return (summary.Total, summary.ThisYear, summary.ThisMonth, summary.ThisWeek);
    }

    public override async Task<Receipt> UpdateAsync(Receipt entity)
    {
        // The receipt and its items are already tracked from GetWithItemsAsync
        // EF Core's change tracking will automatically detect all modifications
        // We just need to explicitly mark the receipt as Modified
        _context.Entry(entity).State = EntityState.Modified;

        if (entity.Merchant != null)
        {
            _context.Entry(entity.Merchant).State = EntityState.Modified;
        }

        // Items are already tracked and EF Core will automatically:
        // - UPDATE items that were modified (UpdateDetails was called)
        // - INSERT items that were added to the collection
        // - DELETE items that were removed from the collection (if we configure cascade delete)

        await _context.SaveChangesAsync();
        return entity;
    }
}