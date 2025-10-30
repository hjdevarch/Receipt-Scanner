using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces
{
    public interface ISettingsRepository : IRepository<Settings>
    {
        Task<Settings?> GetDefaultSettingsAsync();
        Task<string> GetDefaultCurrencyNameAsync();
        Task<string> GetDefaultCurrencySymbolAsync();
    }
}