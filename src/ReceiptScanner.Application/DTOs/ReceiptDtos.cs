using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ReceiptScanner.Application.DTOs;

/// <summary>
/// Receipt data transfer object containing all receipt information
/// </summary>
public class ReceiptDto
{
    /// <summary>
    /// Unique identifier for the receipt
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Receipt number as extracted from the image or auto-generated
    /// </summary>
    public string ReceiptNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Date and time when the receipt was issued
    /// </summary>
    public DateTime ReceiptDate { get; set; }
    
    /// <summary>
    /// Subtotal amount before tax
    /// </summary>
    public decimal SubTotal { get; set; }
    
    /// <summary>
    /// Tax amount charged
    /// </summary>
    public decimal TaxAmount { get; set; }
    
    /// <summary>
    /// Total amount including tax
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// Reward or discount amount applied to the receipt
    /// </summary>
    public decimal? Reward { get; set; }
    
    /// <summary>
    /// Currency code (e.g., GBP, USD, EUR)
    /// </summary>
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Currency symbol (e.g., £, $, €)
    /// </summary>
    public string CurrencySymbol { get; set; } = "$";
    
    /// <summary>
    /// Path to the stored receipt image
    /// </summary>
    public string? ImagePath { get; set; }
    
    /// <summary>
    /// Current status of the receipt processing
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Merchant information extracted from the receipt
    /// </summary>
    public MerchantDto Merchant { get; set; } = new();
    
    /// <summary>
    /// List of items purchased as shown on the receipt
    /// </summary>
    public List<ReceiptItemDto> Items { get; set; } = new();
    
    /// <summary>
    /// When the receipt was first processed
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the receipt was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}

public class ReceiptItemDto
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string? QuantityUnit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public Guid? CategoryId { get; set; } // Populated from Item.CategoryId relationship
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

/// <summary>
/// Data transfer object for creating a new receipt from uploaded image
/// </summary>
public class CreateReceiptDto
{
    /// <summary>
    /// Receipt image file (JPEG, PNG, BMP, TIFF, or PDF format, max 10MB)
    /// </summary>
    [Required]
    public IFormFile ReceiptImage { get; set; } = null!;
    
    /// <summary>
    /// Optional receipt number override (if not provided, will be auto-generated or extracted from image)
    /// </summary>
    public string? ReceiptNumber { get; set; }
    
    /// <summary>
    /// Optional receipt date override (if not provided, will be extracted from image or use current date)
    /// </summary>
    public DateTime? ReceiptDate { get; set; }
}

public class ReceiptProcessingResultDto
{
    public Guid ReceiptId { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public ReceiptDto? Receipt { get; set; }
}

public class UpdateReceiptDto
{
    public string? ReceiptNumber { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? Reward { get; set; }
    public string? Currency { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? Status { get; set; }
    public UpdateMerchantDto? Merchant { get; set; }
    public List<UpdateReceiptItemDto>? Items { get; set; }
}

public class UpdateReceiptItemDto
{
    public Guid? Id { get; set; } // Null for new items
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public string? QuantityUnit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Category { get; set; }
    public string? SKU { get; set; }
}

public class UpdateMerchantDto
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
}