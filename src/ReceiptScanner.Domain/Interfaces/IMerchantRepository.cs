using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces;

public interface IMerchantRepository : IRepository<Merchant>
{
    Task<IEnumerable<Merchant>> GetAllByUserIdAsync(string userId);
    Task<Merchant?> GetByNameAsync(string name, string userId);
    Task<IEnumerable<Merchant>> SearchByNameAsync(string searchTerm, string userId);
    Task<IEnumerable<Merchant>> GetAllWithReceiptTotalsAsync(string userId);
    Task<(IEnumerable<Merchant> Merchants,int TotalCount)> GetAllWithReceiptTotalsPagedAsync(string userId, int skip, int take);
}