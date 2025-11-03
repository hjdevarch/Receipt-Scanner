namespace ReceiptScanner.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;

    // Navigation properties
    public virtual ApplicationUser User { get; private set; } = null!;
    public virtual ICollection<ReceiptItem> ReceiptItems { get; private set; } = new List<ReceiptItem>();

    protected Category() { } // For EF Core

    public Category(string name, string userId)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
    }

    public void UpdateName(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SetUpdatedAt();
    }
}
