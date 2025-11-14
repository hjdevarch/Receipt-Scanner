using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.Infrastructure.Repositories;

public class BaseRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ReceiptScannerDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public BaseRepository(ReceiptScannerDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        // AddAsync will track the entity and its navigation properties (like Receipt.Items collection)
        await _dbSet.AddAsync(entity);
        
        // For receipts, explicitly ensure all items in the collection are tracked
        // This is needed because items might have been created outside of EF Core tracking
        if (entity is Receipt receipt && receipt.Items.Any())
        {
            foreach (var item in receipt.Items)
            {
                // Check if this item is already tracked
                var entry = _context.Entry(item);
                if (entry.State == EntityState.Detached)
                {
                    // Item is not tracked, so explicitly add it
                    _context.Set<ReceiptItem>().Add(item);
                }
            }
        }
        
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}