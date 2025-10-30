using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.Infrastructure.Repositories
{
    public class SettingsRepository : BaseRepository<Settings>, ISettingsRepository
    {
        public SettingsRepository(ReceiptScannerDbContext context) : base(context)
        {
        }

        public async Task<Settings?> GetDefaultSettingsAsync()
        {
            // Get the first settings record (should be only one)
            return await _dbSet.FirstOrDefaultAsync();
        }

        public async Task<string> GetDefaultCurrencyNameAsync()
        {
            var settings = await GetDefaultSettingsAsync();
            return settings?.DefaultCurrencyName ?? "GBP"; // Fallback to GBP if no settings found
        }

        public async Task<string> GetDefaultCurrencySymbolAsync()
        {
            var settings = await GetDefaultSettingsAsync();
            return settings?.DefaultCurrencySymbol ?? "£"; // Fallback to £ if no settings found
        }
    }
}