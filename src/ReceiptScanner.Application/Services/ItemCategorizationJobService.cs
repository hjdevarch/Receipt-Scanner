using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ReceiptScanner.Application.Services;

/// <summary>
/// Background job service for updating CategoryId in ItemNames table
/// based on AI/ML categorization or business rules
/// </summary>
public class ItemCategorizationJobService
{
    private readonly IItemNameRepository _itemNameRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<ItemCategorizationJobService> _logger;

    public ItemCategorizationJobService(
        IItemNameRepository itemNameRepository,
        ICategoryRepository categoryRepository,
        ILogger<ItemCategorizationJobService> logger)
    {
        _itemNameRepository = itemNameRepository;
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Main job method to categorize uncategorized items
    /// This method should be scheduled to run periodically (e.g., daily, weekly)
    /// </summary>
    public async Task RunCategorizationJobAsync(string userId)
    {
        _logger.LogInformation("Starting item categorization job for user {UserId}", userId);

        try
        {
            // Step 1: Get all uncategorized items from ItemNames
            var uncategorizedItems = await _itemNameRepository.GetUncategorizedAsync();
            _logger.LogInformation("Found {Count} uncategorized items", uncategorizedItems.Count());

            if (!uncategorizedItems.Any())
            {
                _logger.LogInformation("No uncategorized items found. Job complete.");
                return;
            }

            // Step 2: Get all categories for the user
            var categories = await _categoryRepository.GetAllByUserIdAsync(userId);
            var categoryMap = categories.ToDictionary(c => c.Name.ToLower(), c => c.Id);

            int itemsProcessed = 0;
            int itemsUpdated = 0;

            // Step 3: Process each uncategorized item
            foreach (var itemName in uncategorizedItems)
            {
                itemsProcessed++;

                // TODO: Implement your categorization logic here
                // Options:
                // 1. Use AI/ML model to predict category
                // 2. Use keyword matching against category names
                // 3. Use external API for product categorization
                // 4. Use historical data patterns
                
                var suggestedCategoryId = await CategorizeItemAsync(itemName.Name, categoryMap);

                if (suggestedCategoryId.HasValue)
                {
                    // Step 4: Update ItemName with CategoryId
                    itemName.SetCategory(suggestedCategoryId);
                    await _itemNameRepository.UpdateAsync(itemName);

                    itemsUpdated++;
                    _logger.LogInformation("Categorized item '{ItemName}' with category ID {CategoryId}", 
                        itemName.Name, suggestedCategoryId);
                }
            }

            _logger.LogInformation("Categorization job complete. Processed: {Processed}, Updated: {Updated}", 
                itemsProcessed, itemsUpdated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during item categorization job");
            throw;
        }
    }

    /// <summary>
    /// Categorize a single item name using business logic or AI
    /// TODO: Implement your categorization algorithm here
    /// </summary>
    private async Task<Guid?> CategorizeItemAsync(string itemName, Dictionary<string, Guid> categoryMap)
    {
        // STUB: This is where you'll implement your categorization logic
        // Example strategies:
        
        // Strategy 1: Simple keyword matching
        var lowerItemName = itemName.ToLower();
        
        if (lowerItemName.Contains("milk") || lowerItemName.Contains("bread") || lowerItemName.Contains("cheese"))
        {
            return categoryMap.TryGetValue("groceries", out var groceryId) ? groceryId : null;
        }
        
        if (lowerItemName.Contains("soap") || lowerItemName.Contains("shampoo") || lowerItemName.Contains("toothpaste"))
        {
            return categoryMap.TryGetValue("personal care", out var personalCareId) ? personalCareId : null;
        }
        
        // Strategy 2: Use ML model (integrate with your GPT helper or other ML service)
        // var prediction = await _mlService.PredictCategoryAsync(itemName);
        // return prediction?.CategoryId;
        
        // Strategy 3: Use historical patterns
        // Look at what category similar items have been assigned to in the past
        
        // Return null if no match found
        await Task.CompletedTask; // Placeholder for async
        return null;
    }

    /// <summary>
    /// Manually categorize a specific item
    /// Useful for user-initiated categorization or corrections
    /// Updates only the ItemNames table, not ReceiptItems
    /// </summary>
    public async Task ManuallyCategorizItem(string itemName, Guid categoryId, string userId)
    {
        var item = await _itemNameRepository.GetByNameAsync(itemName);
        
        if (item == null)
        {
            throw new InvalidOperationException($"Item '{itemName}' not found in ItemNames");
        }

        // Update the ItemName only
        item.SetCategory(categoryId);
        await _itemNameRepository.UpdateAsync(item);

        _logger.LogInformation("Manually categorized item '{ItemName}' with category ID {CategoryId}", 
            itemName, categoryId);
    }

    /// <summary>
    /// Bulk categorize items based on a mapping dictionary
    /// Useful for batch imports or migrations
    /// </summary>
    public async Task BulkCategorizeItemsAsync(Dictionary<string, Guid> itemCategoryMapping, string userId)
    {
        foreach (var (itemName, categoryId) in itemCategoryMapping)
        {
            try
            {
                await ManuallyCategorizItem(itemName, categoryId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to categorize item '{ItemName}'", itemName);
                // Continue with next item
            }
        }
    }
}
