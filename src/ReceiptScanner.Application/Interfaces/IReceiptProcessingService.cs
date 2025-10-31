using ReceiptScanner.Application.DTOs;

namespace ReceiptScanner.Application.Interfaces;

public interface IReceiptProcessingService
{
    Task<ReceiptProcessingResultDto> ProcessReceiptImageAsync(CreateReceiptDto createReceiptDto, string userId);
    Task<ReceiptDto?> GetReceiptByIdAsync(Guid id, string userId);
    Task<IEnumerable<ReceiptDto>> GetAllReceiptsAsync(string userId);
    Task<IEnumerable<ReceiptDto>> GetReceiptsByMerchantAsync(Guid merchantId, string userId);
    Task<IEnumerable<ReceiptDto>> GetReceiptsByDateRangeAsync(DateTime startDate, DateTime endDate, string userId);
    Task<ReceiptProcessingResultDto> UpdateReceiptAsync(Guid id, UpdateReceiptDto updateReceiptDto, string userId);
    Task<bool> DeleteReceiptAsync(Guid id, string userId);
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
    public decimal? Reward { get; set; }
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