using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Entities;

public class Receipt : BaseEntity
{
    public string ReceiptNumber { get; private set; } = string.Empty;
    public DateTime ReceiptDate { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string? ImagePath { get; private set; }
    public string? RawText { get; private set; }
    public ReceiptStatus Status { get; private set; }

    // Foreign keys
    public Guid MerchantId { get; private set; }

    // Navigation properties
    public virtual Merchant Merchant { get; private set; } = null!;
    public virtual ICollection<ReceiptItem> Items { get; private set; } = new List<ReceiptItem>();

    protected Receipt() { } // For EF Core

    public Receipt(string receiptNumber, DateTime receiptDate, decimal subTotal, decimal taxAmount, 
                   decimal totalAmount, Guid merchantId, string currency = "USD", string? imagePath = null, string? rawText = null)
    {
        ReceiptNumber = receiptNumber ?? throw new ArgumentNullException(nameof(receiptNumber));
        ReceiptDate = receiptDate;
        SubTotal = subTotal;
        TaxAmount = taxAmount;
        TotalAmount = totalAmount;
        MerchantId = merchantId;
        Currency = currency;
        ImagePath = imagePath;
        RawText = rawText;
        Status = ReceiptStatus.Processing;
    }

    public void UpdateStatus(ReceiptStatus status)
    {
        Status = status;
        SetUpdatedAt();
    }

    public void UpdateProcessingResults(string? rawText, decimal subTotal, decimal taxAmount, decimal totalAmount)
    {
        RawText = rawText;
        SubTotal = subTotal;
        TaxAmount = taxAmount;
        TotalAmount = totalAmount;
        Status = ReceiptStatus.Processed;
        SetUpdatedAt();
    }

    public void AddItem(ReceiptItem item)
    {
        Items.Add(item);
        SetUpdatedAt();
    }
}

public enum ReceiptStatus
{
    Processing = 1,
    Processed = 2,
    Failed = 3
}