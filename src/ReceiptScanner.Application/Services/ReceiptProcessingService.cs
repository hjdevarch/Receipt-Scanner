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
        try
        {
            _logger.LogInformation("Starting GetAllReceiptsAsync");
            
            var receipts = await _receiptRepository.GetAllAsync();
            _logger.LogInformation("Retrieved {Count} receipts from repository", receipts?.Count() ?? 0);
            
            if (receipts == null || !receipts.Any())
            {
                _logger.LogInformation("No receipts found, returning empty list");
                return new List<ReceiptDto>();
            }
            
            var result = new List<ReceiptDto>();
            foreach (var receipt in receipts)
            {
                try
                {
                    _logger.LogDebug("Processing receipt {ReceiptId}", receipt.Id);
                    var dto = MapToReceiptDto(receipt);
                    result.Add(dto);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error mapping receipt {ReceiptId} to DTO", receipt.Id);
                    // Continue processing other receipts
                }
            }
            
            _logger.LogInformation("Successfully mapped {Count} receipts to DTOs", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAllReceiptsAsync");
            throw;
        }
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

    public async Task<ReceiptProcessingResultDto> UpdateReceiptAsync(Guid id, UpdateReceiptDto updateReceiptDto)
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(100);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Starting receipt update for ID: {ReceiptId} (attempt {Attempt}/{MaxRetries})", id, attempt, maxRetries);

                // Get the existing receipt
                var existingReceipt = await _receiptRepository.GetByIdAsync(id);
                if (existingReceipt == null)
                {
                    return new ReceiptProcessingResultDto
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Receipt with ID {id} not found"
                    };
                }

                // Update basic details if provided
                if (!string.IsNullOrEmpty(updateReceiptDto.ReceiptNumber) || updateReceiptDto.ReceiptDate.HasValue || !string.IsNullOrEmpty(updateReceiptDto.Currency))
                {
                    var receiptNumber = updateReceiptDto.ReceiptNumber ?? existingReceipt.ReceiptNumber;
                    var receiptDate = updateReceiptDto.ReceiptDate ?? existingReceipt.ReceiptDate;
                    var currency = updateReceiptDto.Currency ?? existingReceipt.Currency;
                    
                    existingReceipt.UpdateBasicDetails(receiptNumber, receiptDate, currency);
                }

                // Update amounts if provided
                if (updateReceiptDto.SubTotal.HasValue || updateReceiptDto.TaxAmount.HasValue || updateReceiptDto.TotalAmount.HasValue)
                {
                    var subTotal = updateReceiptDto.SubTotal ?? existingReceipt.SubTotal;
                    var taxAmount = updateReceiptDto.TaxAmount ?? existingReceipt.TaxAmount;
                    var totalAmount = updateReceiptDto.TotalAmount ?? existingReceipt.TotalAmount;
                    
                    existingReceipt.UpdateAmounts(subTotal, taxAmount, totalAmount);
                }
                
                // Update status if provided
                if (!string.IsNullOrEmpty(updateReceiptDto.Status) && Enum.TryParse<ReceiptStatus>(updateReceiptDto.Status, out var status))
                {
                    existingReceipt.UpdateStatus(status);
                }

                // Update merchant if provided
                if (updateReceiptDto.Merchant != null)
                {
                    var merchant = existingReceipt.Merchant;
                    var name = updateReceiptDto.Merchant.Name ?? merchant.Name;
                    var address = updateReceiptDto.Merchant.Address ?? merchant.Address;
                    var phoneNumber = updateReceiptDto.Merchant.PhoneNumber ?? merchant.PhoneNumber;
                    var email = updateReceiptDto.Merchant.Email ?? merchant.Email;
                    var website = updateReceiptDto.Merchant.Website ?? merchant.Website;
                    
                    merchant.UpdateDetails(name, address, phoneNumber, email, website);
                }

                // Update items if provided (skip for now to avoid concurrency issues)
                // TODO: Implement item updates in a separate endpoint or with better concurrency handling
                if (updateReceiptDto.Items != null)
                {
                    _logger.LogInformation("Item updates requested but skipped to avoid concurrency conflicts. Use dedicated item management endpoints.");
                    // Commenting out item updates for now to avoid concurrency exceptions
                    /*
                    // Clear existing items
                    existingReceipt.ClearItems();
                    
                    // Add updated/new items
                    foreach (var itemDto in updateReceiptDto.Items)
                    {
                        var receiptItem = new ReceiptItem(
                            name: itemDto.Name ?? "Unknown Item",
                            quantity: itemDto.Quantity ?? 1,
                            unitPrice: itemDto.UnitPrice ?? 0,
                            receiptId: existingReceipt.Id,
                            description: itemDto.Description,
                            category: itemDto.Category,
                            sku: itemDto.SKU,
                            quantityUnit: itemDto.QuantityUnit,
                            totalPrice: itemDto.TotalPrice
                        );
                        
                        existingReceipt.AddItem(receiptItem);
                    }
                    */
                }

                // Save changes
                var updatedReceipt = await _receiptRepository.UpdateAsync(existingReceipt);
                
                _logger.LogInformation("Receipt updated successfully. Receipt ID: {ReceiptId}", id);
                
                return new ReceiptProcessingResultDto
                {
                    ReceiptId = updatedReceipt.Id,
                    IsSuccess = true,
                    Receipt = MapToReceiptDto(updatedReceipt)
                };
            }
            catch (Exception ex) when (attempt < maxRetries && IsConcurrencyException(ex))
            {
                _logger.LogWarning("Concurrency conflict on attempt {Attempt}/{MaxRetries} for receipt {ReceiptId}. Retrying after {Delay}ms...", 
                    attempt, maxRetries, id, delay.TotalMilliseconds);
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating receipt with ID: {ReceiptId} on attempt {Attempt}", id, attempt);
                
                if (attempt == maxRetries)
                {
                    return new ReceiptProcessingResultDto
                    {
                        IsSuccess = false,
                        ErrorMessage = $"An error occurred while updating the receipt after {maxRetries} attempts: {ex.Message}"
                    };
                }
                
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
                continue;
            }
        }
        
        // This should not be reached, but just in case
        return new ReceiptProcessingResultDto
        {
            IsSuccess = false,
            ErrorMessage = "Maximum retry attempts exceeded"
        };
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

    private static string GetCurrencySymbol(string currencyCode)
    {
        return currencyCode?.ToUpper() switch
        {
            "GBP" => "£",
            "EUR" => "€",
            "USD" => "$",
            "JPY" => "¥",
            "CHF" => "₣",
            "CAD" => "C$",
            "AUD" => "A$",
            "NZD" => "NZ$",
            "CNY" => "¥",
            "INR" => "₹",
            "KRW" => "₩",
            "SEK" => "kr",
            "NOK" => "kr",
            "DKK" => "kr",
            "PLN" => "zł",
            "CZK" => "Kč",
            "HUF" => "Ft",
            "RUB" => "₽",
            "TRY" => "₺",
            "BRL" => "R$",
            "MXN" => "$",
            "ZAR" => "R",
            "SGD" => "S$",
            "HKD" => "HK$",
            "THB" => "฿",
            "PHP" => "₱",
            "MYR" => "RM",
            "IDR" => "Rp",
            "VND" => "₫",
            _ => "$" // Default to USD symbol
        };
    }

    private static ReceiptDto MapToReceiptDto(Receipt receipt)
    {
        try
        {
            var receiptDto = new ReceiptDto
            {
                Id = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                ReceiptDate = receipt.ReceiptDate,
                SubTotal = receipt.SubTotal,
                TaxAmount = receipt.TaxAmount,
                TotalAmount = receipt.TotalAmount,
                Currency = receipt.Currency,
                CurrencySymbol = GetCurrencySymbol(receipt.Currency),
                ImagePath = receipt.ImagePath,
                Status = receipt.Status.ToString(),
                CreatedAt = receipt.CreatedAt,
                UpdatedAt = receipt.UpdatedAt,
                Merchant = receipt.Merchant != null ? new MerchantDto
                {
                    Id = receipt.Merchant.Id,
                    Name = receipt.Merchant.Name,
                    Address = receipt.Merchant.Address,
                    PhoneNumber = receipt.Merchant.PhoneNumber,
                    Email = receipt.Merchant.Email,
                    Website = receipt.Merchant.Website
                } : new MerchantDto
                {
                    Id = Guid.Empty,
                    Name = "Unknown Merchant",
                    Address = null,
                    PhoneNumber = null,
                    Email = null,
                    Website = null
                },
                Items = receipt.Items?.Select(item => new ReceiptItemDto
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
                }).ToList() ?? new List<ReceiptItemDto>()
            };
            
            return receiptDto;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error mapping receipt {receipt.Id} to DTO: {ex.Message}", ex);
        }
    }

    private static bool IsConcurrencyException(Exception ex)
    {
        // Check if it's a concurrency exception by examining the type name and message
        var exceptionType = ex.GetType().Name;
        return exceptionType.Contains("DbUpdateConcurrencyException") || 
               exceptionType.Contains("ConcurrencyException") ||
               (ex.Message != null && ex.Message.Contains("expected to affect") && ex.Message.Contains("row(s), but actually affected"));
    }
}