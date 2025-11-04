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

        public async Task<Settings?> GetByUserIdAsync(string userId)
        {
            return await _dbSet.FirstOrDefaultAsync(s => s.UserId == userId);
        }

        public async Task<string> GetDefaultCurrencyNameAsync(string userId)
        {
            var settings = await GetByUserIdAsync(userId);
            return settings?.DefaultCurrencyName ?? "GBP"; // Fallback to GBP if no settings found
        }

        public async Task<string> GetDefaultCurrencySymbolAsync(string userId)
        {
            var settings = await GetByUserIdAsync(userId);
            return settings?.DefaultCurrencySymbol ?? "£"; // Fallback to £ if no settings found
        }

        public async Task SetDefaultCurrencyAsync(string userId, string currencyName, string currencySymbol)
        {
            var settings = await GetByUserIdAsync(userId);
            
            if (settings == null)
            {
                // Create new settings if none exist
                settings = new Settings
                {
                    UserId = userId,
                    DefaultCurrencyName = currencyName,
                    DefaultCurrencySymbol = currencySymbol
                };
                await AddAsync(settings);
            }
            else
            {
                // Update existing settings
                settings.DefaultCurrencyName = currencyName;
                settings.DefaultCurrencySymbol = currencySymbol;
                await UpdateAsync(settings);
            }
        }
    }
}