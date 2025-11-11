using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces;

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<IEnumerable<Receipt>> GetAllByUserIdAsync(string userId);
    Task<IEnumerable<Receipt>> GetByMerchantIdAsync(Guid merchantId, string userId);
    Task<IEnumerable<Receipt>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, string userId);
    Task<Receipt?> GetWithItemsAsync(Guid id);
    Task<IEnumerable<Receipt>> GetByStatusAsync(ReceiptStatus status, string userId);
    Task DeleteReceiptItemsAsync(Guid receiptId);
    Task<Receipt?> GetReceiptByItemIdAsync(Guid receiptItemId);
}