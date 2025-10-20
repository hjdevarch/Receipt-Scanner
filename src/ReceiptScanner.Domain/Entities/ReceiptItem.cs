using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Entities;

public class ReceiptItem : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Quantity { get; private set; }
    public string? QuantityUnit { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }
    public string? Category { get; private set; }
    public string? SKU { get; private set; }

    // Foreign keys
    public Guid ReceiptId { get; private set; }

    // Navigation properties
    public virtual Receipt Receipt { get; private set; } = null!;

    protected ReceiptItem() { } // For EF Core

    public ReceiptItem(string name, decimal quantity, decimal unitPrice, Guid receiptId, 
                       string? description = null, string? category = null, string? sku = null, 
                       string? quantityUnit = null, decimal? totalPrice = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        UnitPrice = unitPrice >= 0 ? unitPrice : throw new ArgumentException("Unit price cannot be negative", nameof(unitPrice));
        TotalPrice = totalPrice ?? (quantity * unitPrice);
        ReceiptId = receiptId;
        Description = description;
        Category = category;
        SKU = sku;
        QuantityUnit = quantityUnit;
    }

    public void UpdateDetails(string name, decimal quantity, decimal unitPrice, string? description = null, 
                          string? category = null, string? sku = null, string? quantityUnit = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Quantity = quantity > 0 ? quantity : throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
        UnitPrice = unitPrice >= 0 ? unitPrice : throw new ArgumentException("Unit price cannot be negative", nameof(unitPrice));
        TotalPrice = quantity * unitPrice;
        Description = description;
        Category = category;
        SKU = sku;
        QuantityUnit = quantityUnit;
        SetUpdatedAt();
    }
}