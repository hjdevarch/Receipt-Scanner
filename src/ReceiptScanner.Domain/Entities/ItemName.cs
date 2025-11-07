namespace ReceiptScanner.Domain.Entities;

/// <summary>
/// Represents a unique item name with its associated category.
/// This table serves as a lookup for item names to enable consistent categorization.
/// </summary>
public class ItemName
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? CategoryId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    // Navigation properties
    public virtual Category? Category { get; private set; }
    public virtual ICollection<ReceiptItem> ReceiptItems { get; private set; } = new List<ReceiptItem>();

    protected ItemName() { } // For EF Core

    public ItemName(string name, Guid? categoryId = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        CategoryId = categoryId;
    }

    public void SetCategory(Guid? categoryId)
    {
        CategoryId = categoryId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateName(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        UpdatedAt = DateTime.UtcNow;
    }
}
