using ReceiptScanner.Application.DTOs;

namespace ReceiptScanner.Application.Interfaces;

public interface IReceiptProcessingService
{
    Task<ReceiptProcessingResultDto> ProcessReceiptImageAsync(CreateReceiptDto createReceiptDto);
    Task<ReceiptDto?> GetReceiptByIdAsync(Guid id);
    Task<IEnumerable<ReceiptDto>> GetAllReceiptsAsync();
    Task<IEnumerable<ReceiptDto>> GetReceiptsByMerchantAsync(Guid merchantId);
    Task<IEnumerable<ReceiptDto>> GetReceiptsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<bool> DeleteReceiptAsync(Guid id);
}

public interface IDocumentIntelligenceService
{
    Task<DocumentAnalysisResult> AnalyzeReceiptAsync(Stream imageStream);
}

public class DocumentAnalysisResult
{
    public string? MerchantName { get; set; }
    public string? MerchantAddress { get; set; }
    public string? MerchantPhone { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Total { get; set; }
    public string? Currency { get; set; }
    public List<DocumentReceiptItem> Items { get; set; } = new();
    public string? RawText { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DocumentReceiptItem
{
    public string Name { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? QuantityUnit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Category { get; set; }
}