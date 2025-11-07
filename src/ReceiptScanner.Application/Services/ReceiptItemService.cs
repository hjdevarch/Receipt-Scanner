using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;

namespace ReceiptScanner.Application.Services;

/// <summary>
/// Service for managing ReceiptItems with ItemName lookup and categorization logic
/// </summary>
public class ReceiptItemService
{
    private readonly IItemNameRepository _itemNameRepository;
    private readonly IReceiptRepository _receiptRepository;

    public ReceiptItemService(
        IItemNameRepository itemNameRepository,
        IReceiptRepository receiptRepository)
    {
        _itemNameRepository = itemNameRepository;
        _receiptRepository = receiptRepository;
    }

    /// <summary>
    /// Adds a receipt item with ItemName lookup and categorization logic.
    /// If the item name exists in ItemNames, reuse its ID and CategoryId.
    /// If not, create a new ItemName entry with null CategoryId.
    /// </summary>
    public async Task<ReceiptItem> AddReceiptItemAsync(
        string name,
        decimal quantity,
        decimal unitPrice,
        Guid receiptId,
        string userId,
        string? description = null,
        string? category = null,
        string? sku = null,
        string? quantityUnit = null,
        decimal? totalPrice = null)
    {
        int? itemId = null;

        // Step 1: Check if ItemNames.Name already exists
        var existingItemName = await _itemNameRepository.GetByNameAsync(name);

        if (existingItemName != null)
        {
            // Step 2a: If exists, retrieve ItemNames.Id
            // CategoryId will be retrieved via the Item navigation property
            itemId = existingItemName.Id;
        }
        else
        {
            // Step 2b: If not exists, insert new ItemNames record with null CategoryId
            var newItemName = new ItemName(name, categoryId: null);
            var createdItemName = await _itemNameRepository.AddAsync(newItemName);
            itemId = createdItemName.Id;
        }

        // Step 3: Create and return the ReceiptItem with ItemId
        // CategoryId is no longer stored directly on ReceiptItem
        var receiptItem = new ReceiptItem(
            name: name,
            quantity: quantity,
            unitPrice: unitPrice,
            receiptId: receiptId,
            userId: userId,
            description: description,
            category: category,
            sku: sku,
            quantityUnit: quantityUnit,
            totalPrice: totalPrice,
            itemId: itemId
        );

        return receiptItem;
    }

    /// <summary>
    /// Adds multiple receipt items efficiently using ItemName lookup
    /// </summary>
    public async Task<List<ReceiptItem>> AddReceiptItemsAsync(
        List<(string name, decimal quantity, decimal unitPrice, string? description, string? category, string? sku, string? quantityUnit, decimal? totalPrice)> items,
        Guid receiptId,
        string userId)
    {
        var receiptItems = new List<ReceiptItem>();

        foreach (var item in items)
        {
            var receiptItem = await AddReceiptItemAsync(
                name: item.name,
                quantity: item.quantity,
                unitPrice: item.unitPrice,
                receiptId: receiptId,
                userId: userId,
                description: item.description,
                category: item.category,
                sku: item.sku,
                quantityUnit: item.quantityUnit,
                totalPrice: item.totalPrice
            );

            receiptItems.Add(receiptItem);
        }

        return receiptItems;
    }
}
