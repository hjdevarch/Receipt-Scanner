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
    private readonly IRepository<ReceiptItem> _receiptItemRepository;
    private readonly ILogger<ReceiptProcessingService> _logger;

    public ReceiptProcessingService(
        IReceiptRepository receiptRepository,
        IMerchantRepository merchantRepository,
        IDocumentIntelligenceService documentIntelligenceService,
        ReceiptItemService receiptItemService,
        IRepository<ReceiptItem> receiptItemRepository,
        ILogger<ReceiptProcessingService> logger)
    {
        _receiptRepository = receiptRepository;
        _merchantRepository = merchantRepository;
        _documentIntelligenceService = documentIntelligenceService;
        _receiptItemService = receiptItemService;
        _receiptItemRepository = receiptItemRepository;
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
            return result.OrderByDescending(r => r.ReceiptDate);
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
        try
        {
            _logger.LogInformation("Starting receipt update for ID: {ReceiptId}", id);

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

            // Track if receipt entity itself was modified
            bool receiptModified = !string.IsNullOrEmpty(updateReceiptDto.ReceiptNumber) || 
                                  updateReceiptDto.ReceiptDate.HasValue || 
                                  !string.IsNullOrEmpty(updateReceiptDto.Currency) ||
                                  updateReceiptDto.SubTotal.HasValue || 
                                  updateReceiptDto.TaxAmount.HasValue || 
                                  updateReceiptDto.TotalAmount.HasValue || 
                                  updateReceiptDto.Reward.HasValue ||
                                  !string.IsNullOrEmpty(updateReceiptDto.Status) ||
                                  updateReceiptDto.Merchant != null;
            
            // Update items if provided - this must be done last after all receipt updates
            if (updateReceiptDto.Items != null && updateReceiptDto.Items.Any())
            {
                _logger.LogInformation("Updating receipt items for receipt {ReceiptId}: {ItemCount} items provided", 
                    existingReceipt.Id, updateReceiptDto.Items.Count);
                
                foreach (var itemDto in updateReceiptDto.Items)
                {
                    if (itemDto.Id.HasValue)
                    {
                        // Update existing item by retrieving it directly
                        var existingItem = await _receiptItemRepository.GetByIdAsync(itemDto.Id.Value);
                        if (existingItem != null && existingItem.ReceiptId == existingReceipt.Id)
                        {
                            _logger.LogInformation("Updating existing item {ItemId}", itemDto.Id.Value);
                            
                            // Update ItemName category if both ItemId and CategoryId are provided
                            if (itemDto.ItemId.HasValue && itemDto.CategoryId.HasValue)
                            {
                                await _receiptItemService.UpdateItemNameCategoryAsync(itemDto.ItemId.Value, itemDto.CategoryId.Value);
                                _logger.LogInformation("Updated ItemName {ItemId} to category {CategoryId}", 
                                    itemDto.ItemId.Value, itemDto.CategoryId.Value);
                            }
                            
                            // Update the receipt item properties
                            existingItem.UpdateDetails(
                                name: itemDto.Name ?? existingItem.Name,
                                quantity: itemDto.Quantity ?? existingItem.Quantity,
                                unitPrice: itemDto.UnitPrice ?? existingItem.UnitPrice,
                                description: itemDto.Description,
                                category: itemDto.Category,
                                sku: itemDto.SKU,
                                quantityUnit: itemDto.QuantityUnit,
                                itemId: itemDto.ItemId,
                                totalPrice: itemDto.TotalPrice
                            );
                            
                            await _receiptItemRepository.UpdateAsync(existingItem);
                        }
                        else
                        {
                            _logger.LogWarning("Item {ItemId} not found in receipt {ReceiptId}, skipping update", 
                                itemDto.Id.Value, existingReceipt.Id);
                        }
                    }
                    else
                    {
                        // Add new item (no ID provided)
                        _logger.LogInformation("Adding new item without ID: {ItemName}", itemDto.Name);
                        
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
                            totalPrice: itemDto.TotalPrice,
                            itemId: itemDto.ItemId,
                            categoryId: itemDto.CategoryId
                        );
                        
                        // Actually save the new item to the database
                        await _receiptItemRepository.AddAsync(newItem);
                        _logger.LogInformation("New receipt item added with ID: {ItemId}", newItem.Id);
                    }
                }
                
                _logger.LogInformation("Receipt items update completed");
            }

            // Only update the receipt if receipt fields were modified
            // Item updates already update the receipt timestamp automatically
            Receipt updatedReceipt;
            if (receiptModified)
            {
                updatedReceipt = await _receiptRepository.UpdateAsync(existingReceipt);
            }
            else
            {
                // Just reload to get the latest state with updated timestamp from item changes
                updatedReceipt = await _receiptRepository.GetByIdAsync(existingReceipt.Id) ?? existingReceipt;
            }
            
            _logger.LogInformation("Receipt updated successfully. Receipt ID: {ReceiptId}", id);
            
            return new ReceiptProcessingResultDto
            {
                ReceiptId = updatedReceipt.Id,
                IsSuccess = true,
                Receipt = MapToReceiptDto(updatedReceipt)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating receipt with ID: {ReceiptId}", id);
            return new ReceiptProcessingResultDto
            {
                IsSuccess = false,
                ErrorMessage = $"An error occurred while updating the receipt: {ex.Message}"
            };
        }
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

    public async Task<bool> UpdateReceiptItemCategoryAsync(Guid receiptItemId, Guid categoryId, string userId)
    {
        try
        {
            _logger.LogInformation("Updating category for receipt item {ReceiptItemId} to category {CategoryId}", receiptItemId, categoryId);

            // Step 1: Get the receipt item with its receipt to verify ownership
            var receipt = await _receiptRepository.GetReceiptByItemIdAsync(receiptItemId);
            if (receipt == null || receipt.UserId != userId)
            {
                _logger.LogWarning("Receipt item {ReceiptItemId} not found or user {UserId} is not the owner", receiptItemId, userId);
                return false;
            }

            // Step 2: Find the receipt item
            var receiptItem = receipt.Items.FirstOrDefault(i => i.Id == receiptItemId);
            if (receiptItem == null)
            {
                _logger.LogWarning("Receipt item {ReceiptItemId} not found in receipt {ReceiptId}", receiptItemId, receipt.Id);
                return false;
            }

            // Step 3: If the item has an ItemId, update the ItemName's CategoryId
            if (receiptItem.ItemId.HasValue)
            {
                var itemName = await _receiptItemService.UpdateItemNameCategoryAsync(receiptItem.ItemId.Value, categoryId);
                if (itemName == null)
                {
                    _logger.LogWarning("ItemName with ID {ItemId} not found", receiptItem.ItemId.Value);
                    return false;
                }
                
                _logger.LogInformation("Updated ItemName {ItemId} category to {CategoryId}", receiptItem.ItemId.Value, categoryId);
                return true;
            }
            else
            {
                _logger.LogWarning("Receipt item {ReceiptItemId} does not have an associated ItemId", receiptItemId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category for receipt item {ReceiptItemId}", receiptItemId);
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
                    Website = receipt.Merchant.Website,
                    LogoPath = receipt.Merchant.LogoPath
                } : new MerchantDto
                {
                    Id = Guid.Empty,
                    Name = "Unknown Merchant",
                    Address = null,
                    PhoneNumber = null,
                    Email = null,
                    Website = null,
                    LogoPath = null
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
                    ItemId = item.ItemId, // Include ItemId (foreign key to ItemNames)
                    CategoryId = item.Item?.CategoryId, // Get category from ItemName relationship
                    Category = item.Item?.Name,
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
}