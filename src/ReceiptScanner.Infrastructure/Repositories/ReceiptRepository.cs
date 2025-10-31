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
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Receipt>> GetByMerchantIdAsync(Guid merchantId, string userId)
    {
        return await _dbSet
            .Include(r => r.Merchant)
            .Include(r => r.Items.OrderBy(ri => ri.CreatedAt))
            .Where(r => r.MerchantId == merchantId && r.UserId == userId)
            .ToListAsync();
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

    public override async Task<Receipt> UpdateAsync(Receipt entity)
    {
        // Ensure the receipt itself is tracked
        if (_context.Entry(entity).State == EntityState.Detached)
        {
            _dbSet.Attach(entity);
        }

        _context.Entry(entity).State = EntityState.Modified;

        if (entity.Merchant != null)
        {
            var merchantEntry = _context.Entry(entity.Merchant);
            if (merchantEntry.State == EntityState.Detached)
            {
                _context.Attach(entity.Merchant);
            }

            merchantEntry.State = EntityState.Modified;
        }

        if (entity.Items != null)
        {
            foreach (var item in entity.Items)
            {
                // All current items should be inserted (we hard-deleted existing ones via SQL)
                // Use Add to unequivocally mark as Added and queue INSERT statements
                _context.Add(item);
            }
        }

        await _context.SaveChangesAsync();
        return entity;
    }
}