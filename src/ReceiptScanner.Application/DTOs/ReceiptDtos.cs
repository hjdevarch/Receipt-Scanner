using Microsoft.AspNetCore.Http;

namespace ReceiptScanner.Application.DTOs;

public class ReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ImagePath { get; set; }
    public string Status { get; set; } = string.Empty;
    public MerchantDto Merchant { get; set; } = new();
    public List<ReceiptItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ReceiptItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string? QuantityUnit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Category { get; set; }
    public string? SKU { get; set; }
}

public class MerchantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
}

public class CreateReceiptDto
{
    public IFormFile ReceiptImage { get; set; } = null!;
    public string? ReceiptNumber { get; set; }
    public DateTime? ReceiptDate { get; set; }
}

public class ReceiptProcessingResultDto
{
    public Guid ReceiptId { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public ReceiptDto? Receipt { get; set; }
}