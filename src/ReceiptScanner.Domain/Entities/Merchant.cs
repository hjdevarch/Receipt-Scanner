using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Entities;

public class Merchant : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }
    public string? Website { get; private set; }

    // Foreign keys
    public string UserId { get; private set; } = string.Empty;

    // Navigation properties
    public virtual ApplicationUser User { get; private set; } = null!;
    public virtual ICollection<Receipt> Receipts { get; private set; } = new List<Receipt>();

    protected Merchant() { } // For EF Core

    public Merchant(string name, string userId, string? address = null, string? phoneNumber = null, string? email = null, string? website = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Address = address;
        PhoneNumber = phoneNumber;
        Email = email;
        Website = website;
    }

    public void UpdateDetails(string name, string? address = null, string? phoneNumber = null, string? email = null, string? website = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Address = address;
        PhoneNumber = phoneNumber;
        Email = email;
        Website = website;
        SetUpdatedAt();
    }
}