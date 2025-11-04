using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.Interfaces;
using ReceiptScanner.Domain.Entities;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json;

namespace ReceiptScanner.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoryController : ControllerBase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IReceiptRepository _receiptRepository;
    private readonly IGPTHelperService _gptHelper;
    private readonly ILogger<CategoryController> _logger;
    private readonly ReceiptScannerDbContext _context;

    public CategoryController(
        ICategoryRepository categoryRepository,
        IReceiptRepository receiptRepository,
        IGPTHelperService gptHelper,
        ILogger<CategoryController> logger,
        ReceiptScannerDbContext context)
    {
        _categoryRepository = categoryRepository;
        _receiptRepository = receiptRepository;
        _gptHelper = gptHelper;
        _logger = logger;
        _context = context;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// Get all categories for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllCategories()
    {
        try
        {
            var userId = GetUserId();
            var categories = await _categoryRepository.GetAllByUserIdAsync(userId);

            return Ok(categories.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                icon = c.Icon,
                createdAt = c.CreatedAt,
                updatedAt = c.UpdatedAt
            }));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, new { message = "An error occurred while retrieving categories" });
        }
    }

    /// <summary>
    /// Get a specific category by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var category = await _categoryRepository.GetByIdAsync(id, userId);

            if (category == null)
            {
                return NotFound(new { message = "Category not found" });
            }

            return Ok(new
            {
                id = category.Id,
                name = category.Name,
                icon = category.Icon,
                createdAt = category.CreatedAt,
                updatedAt = category.UpdatedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category {CategoryId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the category" });
        }
    }

    /// <summary>
    /// Create a new category
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            var userId = GetUserId();

            // Check if category with same name already exists
            var existingCategory = await _categoryRepository.GetByNameAsync(request.Name, userId);
            if (existingCategory != null)
            {
                return Conflict(new { message = "A category with this name already exists" });
            }

            var category = new Category(request.Name, userId, request.Icon);
            await _categoryRepository.AddAsync(category);

            return CreatedAtAction(nameof(GetCategoryById), new { id = category.Id }, new
            {
                id = category.Id,
                name = category.Name,
                icon = category.Icon,
                createdAt = category.CreatedAt,
                updatedAt = category.UpdatedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            return StatusCode(500, new { message = "An error occurred while creating the category" });
        }
    }

    /// <summary>
    /// Update an existing category
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            var userId = GetUserId();
            var category = await _categoryRepository.GetByIdAsync(id, userId);

            if (category == null)
            {
                return NotFound(new { message = "Category not found" });
            }

            // Check if another category with the same name exists
            var existingCategory = await _categoryRepository.GetByNameAsync(request.Name, userId);
            if (existingCategory != null && existingCategory.Id != id)
            {
                return Conflict(new { message = "A category with this name already exists" });
            }

            category.Update(request.Name, request.Icon);
            await _categoryRepository.UpdateAsync(category);

            return Ok(new
            {
                id = category.Id,
                name = category.Name,
                icon = category.Icon,
                createdAt = category.CreatedAt,
                updatedAt = category.UpdatedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category {CategoryId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the category" });
        }
    }

    /// <summary>
    /// Delete a category
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var category = await _categoryRepository.GetByIdAsync(id, userId);

            if (category == null)
            {
                return NotFound(new { message = "Category not found" });
            }

            await _categoryRepository.DeleteAsync(id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category {CategoryId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the category" });
        }
    }

    /// <summary>
    /// Auto-categorize all receipt items for the current user using GPT
    /// </summary>
    [HttpPost("auto-categorize")]
    public async Task<IActionResult> AutoCategorize()
    {
        try
        {
            var userId = GetUserId();
            _logger.LogInformation("Starting auto-categorization for user {UserId}", userId);

            // 1. Load all receipts for the user
            var receipts = (await _receiptRepository.GetAllByUserIdAsync(userId)).ToList();

            if (!receipts.Any())
            {
                return Ok(new
                {
                    itemsProcessed = 0,
                    categoriesCreated = 0,
                    itemsUpdated = 0,
                    message = "No receipts found for categorization"
                });
            }

            // 2. Extract all unique item names
            var itemNames = receipts
                .SelectMany(r => r.Items)
                .Where(i => !string.IsNullOrWhiteSpace(i.Name) && i.CategoryId == null)
                .Select(i => i.Name)
                .Distinct()
                .ToList();

            if (!itemNames.Any())
            {
                var ctgrs = await _categoryRepository.GetAllByUserIdAsync(userId);

                return Ok(ctgrs.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    icon = c.Icon,
                    createdAt = c.CreatedAt,
                    updatedAt = c.UpdatedAt
                }));
            }

            _logger.LogInformation("Found {ItemCount} unique items to categorize", itemNames.Count);

            // 3. Create GPT prompt
            var joinedNames = string.Join(", ", itemNames);
            var prompt = $@"Categorize these receipt items into logical categories (e.g., Groceries, Household, Personal Care, Electronics, Clothing, Entertainment, etc.). 

Return ONLY a valid JSON array with this exact format (no additional text or explanation):
[{{""item"": ""item name"", ""category"": ""category name""}}, ...]

Items to categorize: {joinedNames}";

            _logger.LogInformation("Sending prompt to GPT for categorization");

            // 4. Send to GPT
            string gptResponse;
            try
            {
                gptResponse = await _gptHelper.SendPromptAsync(prompt);
                _logger.LogInformation("Received response from GPT");
            }
            catch (InvalidOperationException)
            {
                return StatusCode(503, new { message = "GPT service is not available. Please ensure Ollama is running." });
            }
            catch (TimeoutException)
            {
                return StatusCode(408, new { message = "GPT request timed out. Please try again." });
            }

            // 5. Parse GPT response to extract JSON
            var categorizations = ParseGptResponse(gptResponse);

            if (categorizations == null || !categorizations.Any())
            {
                _logger.LogWarning("Failed to parse GPT response or no categorizations returned");
                return BadRequest(new { message = "Failed to parse GPT response", gptResponse });
            }

            _logger.LogInformation("Parsed {CategorizationCount} categorizations from GPT", categorizations.Count);

            // 6. Create unique categories
            var uniqueCategories = categorizations
                .Select(c => c.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var categoryMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            int categoriesCreated = 0;

            foreach (var categoryName in uniqueCategories)
            {
                var existing = await _categoryRepository.GetByNameAsync(categoryName, userId);
                if (existing == null)
                {
                    var newCategory = new Category(categoryName, userId);
                    await _categoryRepository.AddAsync(newCategory);
                    categoryMap[categoryName] = newCategory.Id;
                    categoriesCreated++;
                    _logger.LogInformation("Created new category: {CategoryName}", categoryName);
                }
                else
                {
                    categoryMap[categoryName] = existing.Id;
                }
            }

            // 7. Update receipt items with their categories
            int itemsUpdated = 0;
            foreach (var receipt in receipts)
            {
                foreach (var item in receipt.Items)
                {
                    var categorization = categorizations
                        .FirstOrDefault(c => c.Item.Equals(item.Name, StringComparison.OrdinalIgnoreCase));

                    if (categorization != null && categoryMap.TryGetValue(categorization.Category, out var categoryId))
                    {
                        item.SetCategory(categoryId);
                        itemsUpdated++;
                    }
                }
            }

            // Save all changes at once using the DbContext
            // This is more efficient than calling UpdateAsync on each receipt
            // and avoids the duplicate key issue since items are already tracked
            await _context.SaveChangesAsync();

            _logger.LogInformation("Auto-categorization completed. Items: {ItemsProcessed}, Categories created: {CategoriesCreated}, Items updated: {ItemsUpdated}",
                itemNames.Count, categoriesCreated, itemsUpdated);

            var categories = await _categoryRepository.GetAllByUserIdAsync(userId);

            return Ok(categories.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                icon = c.Icon,
                createdAt = c.CreatedAt,
                updatedAt = c.UpdatedAt
            }));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-categorization");
            return StatusCode(500, new { message = "An error occurred during auto-categorization", error = ex.Message });
        }
    }

    private List<ItemCategorization>? ParseGptResponse(string gptResponse)
    {
        try
        {
            // Try to extract JSON from the response
            // GPT might include extra text, so we need to find the JSON array
            var jsonStart = gptResponse.IndexOf('[');
            var jsonEnd = gptResponse.LastIndexOf(']');

            if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("No valid JSON array found in GPT response");
                return null;
            }

            var jsonContent = gptResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<ItemCategorization>>(jsonContent, options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse GPT response as JSON: {Response}", gptResponse);
            return null;
        }
    }
}

// DTOs
public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
}

public class ItemCategorization
{
    public string Item { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
