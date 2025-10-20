using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces;

public interface IMerchantRepository : IRepository<Merchant>
{
    Task<Merchant?> GetByNameAsync(string name);
    Task<IEnumerable<Merchant>> SearchByNameAsync(string searchTerm);
}