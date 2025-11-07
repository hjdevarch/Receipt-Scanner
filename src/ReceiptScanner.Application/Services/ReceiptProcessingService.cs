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
    private readonly ReceiptItemService _receiptItemService;
    private readonly ILogger<ReceiptProcessingService> _logger;

    public ReceiptProcessingService(
        IReceiptRepository receiptRepository,
        IMerchantRepository merchantRepository,
        IDocumentIntelligenceService documentIntelligenceService,
        ReceiptItemService receiptItemService,
        ILogger<ReceiptProcessingService> logger)
    {
        _receiptRepository = receiptRepository;
        _merchantRepository = merchantRepository;
        _documentIntelligenceService = documentIntelligenceService;
        _receiptItemService = receiptItemService;
        _logger = logger;
    }

    public async Task<ReceiptProcessingResultDto> ProcessReceiptImageAsync(CreateReceiptDto createReceiptDto, string userId)
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
            var merchant = await GetOrCreateMerchantAsync(analysisResult, userId);

            // Create receipt entity
            var receipt = new Receipt(
                receiptNumber: analysisResult.ReceiptNumber ?? Guid.NewGuid().ToString()[..8],
                receiptDate: analysisResult.TransactionDate ?? DateTime.Now,
                subTotal: analysisResult.SubTotal ?? 0,
                taxAmount: analysisResult.Tax ?? 0,
                totalAmount: analysisResult.Total ?? 0,
                merchantId: merchant.Id,
                userId: userId,
                currency: analysisResult.Currency ?? "GBP", // Default to GBP if not detected
                rawText: analysisResult.RawText,
                reward: analysisResult.Reward
            );

            // Add receipt items before saving
            // NOTE: Items are added in-memory but not saved yet. They'll be saved when the receipt is saved.
            foreach (var item in analysisResult.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Name))
                {
                    // If UnitPrice is not available but TotalPrice is, use TotalPrice as UnitPrice (assuming quantity 1)
                    var unitPrice = item.UnitPrice ?? item.TotalPrice ?? 0;
                    // Use the actual quantity from Azure (now supports decimal for items sold by weight)
                    var quantity = item.Quantity ?? 1;
                    var totalPrice = item.TotalPrice ?? (unitPrice * quantity);

                    // Use ReceiptItemService to create the item with ItemName lookup
                    var receiptItem = await _receiptItemService.AddReceiptItemAsync(
                        name: item.Name,
                        quantity: quantity,
                        unitPrice: unitPrice,
                        receiptId: receipt.Id,
                        userId: userId,
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

    public async Task<ReceiptDto?> GetReceiptByIdAsync(Guid id, string userId)
    {
        var receipt = await _receiptRepository.GetWithItemsAsync(id);
        if (receipt == null || receipt.UserId != userId)
            return null;
        return MapToReceiptDto(receipt);
    }

    public async Task<IEnumerable<ReceiptDto>> GetAllReceiptsAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Starting GetAllReceiptsAsync for user {UserId}", userId);
            
            var receipts = await _receiptRepository.GetAllByUserIdAsync(userId);
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

    public async Task<IEnumerable<ReceiptDto>> GetReceiptsByMerchantAsync(Guid merchantId, string userId)
    {
        var receipts = await _receiptRepository.GetByMerchantIdAsync(merchantId, userId);
        return receipts.Select(MapToReceiptDto);
    }

    public async Task<IEnumerable<ReceiptDto>> GetReceiptsByDateRangeAsync(DateTime startDate, DateTime endDate, string userId)
    {
        var receipts = await _receiptRepository.GetByDateRangeAsync(startDate, endDate, userId);
        return receipts.Select(MapToReceiptDto);
    }

    public async Task<ReceiptProcessingResultDto> UpdateReceiptAsync(Guid id, UpdateReceiptDto updateReceiptDto, string userId)
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromMilliseconds(100);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Starting receipt update for ID: {ReceiptId} (attempt {Attempt}/{MaxRetries})", id, attempt, maxRetries);

                // Get the existing receipt WITHOUT items first to avoid tracking issues
                var existingReceipt = await _receiptRepository.GetByIdAsync(id);
                if (existingReceipt == null || existingReceipt.UserId != userId)
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
                if (updateReceiptDto.SubTotal.HasValue || updateReceiptDto.TaxAmount.HasValue || updateReceiptDto.TotalAmount.HasValue || updateReceiptDto.Reward.HasValue)
                {
                    var subTotal = updateReceiptDto.SubTotal ?? existingReceipt.SubTotal;
                    var taxAmount = updateReceiptDto.TaxAmount ?? existingReceipt.TaxAmount;
                    var totalAmount = updateReceiptDto.TotalAmount ?? existingReceipt.TotalAmount;
                    
                    existingReceipt.UpdateAmounts(subTotal, taxAmount, totalAmount);
                    
                    // Update reward separately if provided
                    if (updateReceiptDto.Reward.HasValue)
                    {
                        existingReceipt.UpdateProcessingResults(
                            existingReceipt.RawText,
                            subTotal,
                            taxAmount,
                            totalAmount,
                            updateReceiptDto.Reward
                        );
                    }
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

                // Update items if provided - completely replace all items
                if (updateReceiptDto.Items != null && updateReceiptDto.Items.Any())
                {
                    var existingItemsCount = existingReceipt.Items.Count;
                    _logger.LogInformation("Replacing receipt items: Will delete {ExistingCount} existing items and insert {NewCount} new items", 
                        existingItemsCount, updateReceiptDto.Items.Count);
                    
                    // Step 1: Delete all existing items using raw SQL to avoid EF Core tracking issues
                    await _receiptRepository.DeleteReceiptItemsAsync(existingReceipt.Id);
                    
                    // Step 2: Clear the Items collection so EF Core knows they're gone
                    existingReceipt.Items.Clear();
                    
                    // Step 3: Add new items using ReceiptItemService for ItemName lookup
                    foreach (var itemDto in updateReceiptDto.Items)
                    {
                        var newItem = await _receiptItemService.AddReceiptItemAsync(
                            name: itemDto.Name ?? "Unknown Item",
                            quantity: itemDto.Quantity ?? 1,
                            unitPrice: itemDto.UnitPrice ?? 0,
                            receiptId: existingReceipt.Id,
                            userId: userId,
                            description: itemDto.Description,
                            category: itemDto.Category,
                            sku: itemDto.SKU,
                            quantityUnit: itemDto.QuantityUnit,
                            totalPrice: itemDto.TotalPrice
                        );
                        
                        existingReceipt.Items.Add(newItem);
                    }
                    
                    _logger.LogInformation("Receipt items replaced: Deleted {DeletedCount} items via SQL, will insert {AddedCount} new items via EF Core", 
                        existingItemsCount, existingReceipt.Items.Count);
                }

                /* PREVIOUS APPROACH - CAUSED CONCURRENCY EXCEPTION BECAUSE EF CORE TRIED TO UPDATE DELETED ENTITIES
                // Update items if provided - completely replace all items
                if (updateReceiptDto.Items != null && updateReceiptDto.Items.Any())
                {
                    var existingItemsCount = existingReceipt.Items.Count;
                    _logger.LogInformation("Replacing receipt items: Will delete {ExistingCount} existing items and insert {NewCount} new items", 
                        existingItemsCount, updateReceiptDto.Items.Count);
                    
                    // Important: Clear items collection to mark for deletion
                    // The cascade delete will handle the actual database deletion
                    existingReceipt.Items.Clear();
                    
                    // CRITICAL: Create completely new ReceiptItem entities
                    // Do NOT reuse any existing entities or IDs
                    foreach (var itemDto in updateReceiptDto.Items)
                    {
                        // Create a brand new ReceiptItem - it will get a fresh GUID from BaseEntity
                        var newItem = new ReceiptItem(
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
                        
                        // Add the new item - EF Core should track this as "Added" state
                        existingReceipt.Items.Add(newItem);
                    }
                    
                    _logger.LogInformation("Receipt items collection updated: {DeletedCount} marked for deletion, {AddedCount} marked for insertion", 
                        existingItemsCount, existingReceipt.Items.Count);
                }
                */

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

    public async Task<bool> DeleteReceiptAsync(Guid id, string userId)
    {
        try
        {
            var receipt = await _receiptRepository.GetByIdAsync(id);
            if (receipt == null || receipt.UserId != userId)
                return false;
                
            await _receiptRepository.DeleteAsync(id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting receipt with ID: {ReceiptId}", id);
            return false;
        }
    }

    private async Task<Merchant> GetOrCreateMerchantAsync(DocumentAnalysisResult analysisResult, string userId)
    {
        var merchantName = analysisResult.MerchantName ?? "Unknown Merchant";
        
        // Try to find existing merchant for this user
        var existingMerchant = await _merchantRepository.GetByNameAsync(merchantName, userId);
        if (existingMerchant != null)
        {
            return existingMerchant;
        }

        // Create new merchant
        var newMerchant = new Merchant(
            name: merchantName,
            userId: userId,
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
                Reward = receipt.Reward,
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
                    ReceiptId = receipt.Id,
                    ReceiptDate = receipt.ReceiptDate,
                    Name = item.Name,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    QuantityUnit = item.QuantityUnit,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice,
                    CategoryId = item.CategoryId,
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