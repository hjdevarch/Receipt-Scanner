using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces;

public interface IReceiptRepository : IRepository<Receipt>
{
    Task<IEnumerable<Receipt>> GetAllByUserIdAsync(string userId);
    Task<(IEnumerable<Receipt> Items, int TotalCount)> GetAllByUserIdPagedAsync(string userId, int skip, int take);
    Task<IEnumerable<Receipt>> GetByMerchantIdAsync(Guid merchantId, string userId);
    Task<(IEnumerable<Receipt> Items, int TotalCount)> GetByMerchantIdPagedAsync(Guid merchantId, string userId, int skip, int take);
    Task<IEnumerable<Receipt>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, string userId);
    Task<(IEnumerable<Receipt> Items, int TotalCount)> GetByDateRangePagedAsync(DateTime startDate, DateTime endDate, string userId, int skip, int take);
    Task<Receipt?> GetWithItemsAsync(Guid id);
    Task<IEnumerable<Receipt>> GetByStatusAsync(ReceiptStatus status, string userId);
    Task DeleteReceiptItemsAsync(Guid receiptId);
    Task<Receipt?> GetReceiptByItemIdAsync(Guid receiptItemId);
    Task<(decimal Total, decimal ThisYear, decimal ThisMonth, decimal ThisWeek)> GetReceiptSummaryAsync(string userId, DateTime startOfYear, DateTime startOfMonth, DateTime startOfWeek);
}