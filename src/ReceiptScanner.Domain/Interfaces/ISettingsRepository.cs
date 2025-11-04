using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces
{
    public interface ISettingsRepository : IRepository<Settings>
    {
        Task<Settings?> GetByUserIdAsync(string userId);
        Task<string> GetDefaultCurrencyNameAsync(string userId);
        Task<string> GetDefaultCurrencySymbolAsync(string userId);
        Task SetDefaultCurrencyAsync(string userId, string currencyName, string currencySymbol);
    }
}