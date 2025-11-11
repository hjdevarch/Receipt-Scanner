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
    /// If ItemId is provided, use it directly. If CategoryId is provided, update the ItemName's CategoryId.
    /// If ItemId is not provided: If the item name exists in ItemNames, reuse its ID and CategoryId.
    /// If not, create a new ItemName entry with null CategoryId (or the provided CategoryId).
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
        decimal? totalPrice = null,
        int? itemId = null,
        Guid? categoryId = null)
    {
        int? resolvedItemId = itemId;

        if (resolvedItemId.HasValue)
        {
            // If ItemId is explicitly provided, use it and optionally update its CategoryId
            if (categoryId.HasValue)
            {
                var existingItemName = await _itemNameRepository.GetByIdAsync(resolvedItemId.Value);
                if (existingItemName != null)
                {
                    existingItemName.SetCategory(categoryId.Value);
                    await _itemNameRepository.UpdateAsync(existingItemName);
                }
            }
        }
        else
        {
            // Step 1: Check if ItemNames.Name already exists
            var existingItemName = await _itemNameRepository.GetByNameAsync(name);

            if (existingItemName != null)
            {
                // Step 2a: If exists, retrieve ItemNames.Id
                resolvedItemId = existingItemName.Id;
                
                // Update CategoryId if provided
                if (categoryId.HasValue)
                {
                    existingItemName.SetCategory(categoryId.Value);
                    await _itemNameRepository.UpdateAsync(existingItemName);
                }
            }
            else
            {
                // Step 2b: If not exists, insert new ItemNames record with provided or null CategoryId
                var newItemName = new ItemName(name, categoryId: categoryId);
                var createdItemName = await _itemNameRepository.AddAsync(newItemName);
                resolvedItemId = createdItemName.Id;
            }
        }

        // Step 3: Create and return the ReceiptItem with ItemId
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
            itemId: resolvedItemId
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

    /// <summary>
    /// Updates the category of an ItemName record
    /// </summary>
    public async Task<ItemName?> UpdateItemNameCategoryAsync(int itemId, Guid categoryId)
    {
        var itemName = await _itemNameRepository.GetByIdAsync(itemId);
        if (itemName == null)
        {
            return null;
        }

        itemName.SetCategory(categoryId);
        await _itemNameRepository.UpdateAsync(itemName);
        return itemName;
    }
}
