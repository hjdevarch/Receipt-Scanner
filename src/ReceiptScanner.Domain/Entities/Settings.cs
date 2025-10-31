using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Entities
{
    public class Settings : BaseEntity
    {
        public string DefaultCurrencyName { get; set; } = string.Empty;
        public string DefaultCurrencySymbol { get; set; } = string.Empty;
        
        // Foreign keys
        public string UserId { get; set; } = string.Empty;

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
        
        // Future fields can be added here:
        // public string DefaultLanguage { get; set; } = string.Empty;
        // public string TimeZone { get; set; } = string.Empty;
        // public bool EnableNotifications { get; set; } = true;
        // public int RetentionPeriodDays { get; set; } = 365;
    }
}