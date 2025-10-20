using Microsoft.Extensions.Logging;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Application.Interfaces;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;

namespace ReceiptScanner.Application.Services;

public class ReceiptProcessingService : IReceiptProcessingService
{
    private readonly IReceiptRepository _receiptRepository;
    private readonly IMerchantRepository _merchantRepository;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly ILogger<ReceiptProcessingService> _logger;

    public ReceiptProcessingService(
        IReceiptRepository receiptRepository,
        IMerchantRepository merchantRepository,
        IDocumentIntelligenceService documentIntelligenceService,
        ILogger<ReceiptProcessingService> logger)
    {
        _receiptRepository = receiptRepository;
        _merchantRepository = merchantRepository;
        _documentIntelligenceService = documentIntelligenceService;
        _logger = logger;
    }

    public async Task<ReceiptProcessingResultDto> ProcessReceiptImageAsync(CreateReceiptDto createReceiptDto)
    {
        try
        {
            _logger.LogInformation("Starting receipt processing for uploaded image");

            // Analyze the receipt image using Azure Document Intelligence
            using var imageStream = createReceiptDto.ReceiptImage.OpenReadStream();
            var analysisResult = await _documentIntelligenceService.AnalyzeReceiptAsync(imageStream);

            if (!analysisResult.IsSuccess)
            {
                return new ReceiptProcessingResultDto
                {
                    IsSuccess = false,
                    ErrorMessage = analysisResult.ErrorMessage ?? "Failed to analyze receipt image"
                };
            }

            // Find or create merchant
            var merchant = await GetOrCreateMerchantAsync(analysisResult);

            // Create receipt entity
            var receipt = new Receipt(
                receiptNumber: analysisResult.ReceiptNumber ?? Guid.NewGuid().ToString()[..8],
                receiptDate: analysisResult.TransactionDate ?? DateTime.Now,
                subTotal: analysisResult.SubTotal ?? 0,
                taxAmount: analysisResult.Tax ?? 0,
                totalAmount: analysisResult.Total ?? 0,
                merchantId: merchant.Id,
                currency: analysisResult.Currency ?? "GBP", // Default to GBP if not detected
                rawText: analysisResult.RawText
            );

            // Add receipt items before saving
            foreach (var item in analysisResult.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                {
                    // If UnitPrice is not available but TotalPrice is, use TotalPrice as UnitPrice (assuming quantity 1)
                    var unitPrice = item.UnitPrice ?? item.TotalPrice ?? 0;
                    // Use the actual quantity from Azure (now supports decimal for items sold by weight)
                    var quantity = item.Quantity ?? 1;
                    var totalPrice = item.TotalPrice ?? (unitPrice * quantity);

                    var receiptItem = new ReceiptItem(
                        name: item.Name,
                        quantity: quantity,
                        unitPrice: unitPrice,
                        receiptId: receipt.Id,
                        quantityUnit: item.QuantityUnit,
                        totalPrice: totalPrice
                    );

                    receipt.AddItem(receiptItem);
                }
            }

            // Update receipt with processed status
            receipt.UpdateStatus(ReceiptStatus.Processed);

            // Save receipt to database (this will save items too due to cascade)
            var savedReceipt = await _receiptRepository.AddAsync(receipt);
            
            _logger.LogInformation("Receipt processing completed successfully. Receipt ID: {ReceiptId}", savedReceipt.Id);

            // Convert to DTO and return
            var receiptDto = MapToReceiptDto(savedReceipt);
            return new ReceiptProcessingResultDto
            {
                ReceiptId = savedReceipt.Id,
                IsSuccess = true,
                Receipt = receiptDto
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing receipt image");
            return new ReceiptProcessingResultDto
            {
                IsSuccess = false,
                ErrorMessage = $"Error processing receipt: {ex.Message}"
            };
        }
    }

    public async Task<ReceiptDto?> GetReceiptByIdAsync(Guid id)
    {
        var receipt = await _receiptRepository.GetWithItemsAsync(id);
        return receipt != null ? MapToReceiptDto(receipt) : null;
    }

    public async Task<IEnumerable<ReceiptDto>> GetAllReceiptsAsync()
    {
        var receipts = await _receiptRepository.GetAllAsync();
        return receipts.Select(MapToReceiptDto);
    }

    public async Task<IEnumerable<ReceiptDto>> GetReceiptsByMerchantAsync(Guid merchantId)
    {
        var receipts = await _receiptRepository.GetByMerchantIdAsync(merchantId);
        return receipts.Select(MapToReceiptDto);
    }

    public async Task<IEnumerable<ReceiptDto>> GetReceiptsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var receipts = await _receiptRepository.GetByDateRangeAsync(startDate, endDate);
        return receipts.Select(MapToReceiptDto);
    }

    public async Task<bool> DeleteReceiptAsync(Guid id)
    {
        try
        {
            await _receiptRepository.DeleteAsync(id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting receipt with ID: {ReceiptId}", id);
            return false;
        }
    }

    private async Task<Merchant> GetOrCreateMerchantAsync(DocumentAnalysisResult analysisResult)
    {
        var merchantName = analysisResult.MerchantName ?? "Unknown Merchant";
        
        // Try to find existing merchant
        var existingMerchant = await _merchantRepository.GetByNameAsync(merchantName);
        if (existingMerchant != null)
        {
            return existingMerchant;
        }

        // Create new merchant
        var newMerchant = new Merchant(
            name: merchantName,
            address: analysisResult.MerchantAddress,
            phoneNumber: analysisResult.MerchantPhone
        );

        return await _merchantRepository.AddAsync(newMerchant);
    }

    private static ReceiptDto MapToReceiptDto(Receipt receipt)
    {
        return new ReceiptDto
        {
            Id = receipt.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            SubTotal = receipt.SubTotal,
            TaxAmount = receipt.TaxAmount,
            TotalAmount = receipt.TotalAmount,
            Currency = receipt.Currency,
            ImagePath = receipt.ImagePath,
            Status = receipt.Status.ToString(),
            CreatedAt = receipt.CreatedAt,
            UpdatedAt = receipt.UpdatedAt,
            Merchant = new MerchantDto
            {
                Id = receipt.Merchant.Id,
                Name = receipt.Merchant.Name,
                Address = receipt.Merchant.Address,
                PhoneNumber = receipt.Merchant.PhoneNumber,
                Email = receipt.Merchant.Email,
                Website = receipt.Merchant.Website
            },
            Items = receipt.Items.Select(item => new ReceiptItemDto
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                Quantity = item.Quantity,
                QuantityUnit = item.QuantityUnit,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice,
                Category = item.Category,
                SKU = item.SKU
            }).ToList()
        };
    }
}