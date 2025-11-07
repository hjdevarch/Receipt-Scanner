namespace ReceiptScanner.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public string UserId { get; private set; } = string.Empty;

    // Navigation properties
    public virtual ApplicationUser User { get; private set; } = null!;

    protected Category() { } // For EF Core

    public Category(string name, string userId, string? icon = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        Icon = icon;
    }

    public void UpdateName(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        SetUpdatedAt();
    }

    public void UpdateIcon(string? icon)
    {
        Icon = icon;
        SetUpdatedAt();
    }

    public void Update(string name, string? icon)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Icon = icon;
        SetUpdatedAt();
    }
}
