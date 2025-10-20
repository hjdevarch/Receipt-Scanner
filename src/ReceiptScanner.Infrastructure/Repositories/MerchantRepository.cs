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

    public async Task<Merchant?> GetByNameAsync(string name)
    {
        return await _dbSet
            .FirstOrDefaultAsync(m => m.Name.ToLower() == name.ToLower());
    }

    public async Task<IEnumerable<Merchant>> SearchByNameAsync(string searchTerm)
    {
        return await _dbSet
            .Where(m => m.Name.ToLower().Contains(searchTerm.ToLower()))
            .OrderBy(m => m.Name)
            .ToListAsync();
    }
}