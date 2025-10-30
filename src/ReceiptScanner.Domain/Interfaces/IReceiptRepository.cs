using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces;

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<IEnumerable<Receipt>> GetByMerchantIdAsync(Guid merchantId);
    Task<IEnumerable<Receipt>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<Receipt?> GetWithItemsAsync(Guid id);
    Task<IEnumerable<Receipt>> GetByStatusAsync(ReceiptStatus status);
    Task DeleteReceiptItemsAsync(Guid receiptId);
}