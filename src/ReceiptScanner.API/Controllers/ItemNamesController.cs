using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.Services;
using ReceiptScanner.Domain.Interfaces;
using System.Security.Claims;

namespace ReceiptScanner.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ItemNamesController : ControllerBase
{
    private readonly IItemNameRepository _itemNameRepository;
    private readonly ItemCategorizationJobService _categorizationJobService;
    private readonly ILogger<ItemNamesController> _logger;

    public ItemNamesController(
        IItemNameRepository itemNameRepository,
        ItemCategorizationJobService categorizationJobService,
        ILogger<ItemNamesController> logger)
    {
        _itemNameRepository = itemNameRepository;
        _categorizationJobService = categorizationJobService;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// Get all item names
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllItemNames()
    {
        try
        {
            var itemNames = await _itemNameRepository.GetAllAsync();
            return Ok(itemNames.Select(i => new
            {
                id = i.Id,
                name = i.Name,
                categoryId = i.CategoryId,
                createdAt = i.CreatedAt,
                updatedAt = i.UpdatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting item names");
            return StatusCode(500, new { message = "An error occurred while retrieving item names" });
        }
    }

    /// <summary>
    /// Get uncategorized item names (items without a CategoryId)
    /// </summary>
    [HttpGet("uncategorized")]
    public async Task<IActionResult> GetUncategorizedItems()
    {
        try
        {
            var items = await _itemNameRepository.GetUncategorizedAsync();
            return Ok(items.Select(i => new
            {
                id = i.Id,
                name = i.Name,
                createdAt = i.CreatedAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting uncategorized items");
            return StatusCode(500, new { message = "An error occurred while retrieving uncategorized items" });
        }
    }

    /// <summary>
    /// Manually categorize a specific item by name
    /// </summary>
    [HttpPut("categorize")]
    public async Task<IActionResult> CategorizeItem([FromBody] CategorizeItemRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _categorizationJobService.ManuallyCategorizItem(
                request.ItemName, 
                request.CategoryId, 
                userId);
            
            return Ok(new { message = $"Item '{request.ItemName}' has been categorized successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error categorizing item");
            return StatusCode(500, new { message = "An error occurred while categorizing the item" });
        }
    }

    /// <summary>
    /// Run the background categorization job manually
    /// In production, this would be triggered by a scheduled job (e.g., Hangfire, Quartz)
    /// </summary>
    [HttpPost("run-categorization-job")]
    public async Task<IActionResult> RunCategorizationJob()
    {
        try
        {
            var userId = GetUserId();
            await _categorizationJobService.RunCategorizationJobAsync(userId);
            
            return Ok(new { message = "Categorization job completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running categorization job");
            return StatusCode(500, new { message = "An error occurred while running the categorization job" });
        }
    }

    /// <summary>
    /// Get a specific item name by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetItemNameById(int id)
    {
        try
        {
            var itemName = await _itemNameRepository.GetByIdAsync(id);
            
            if (itemName == null)
            {
                return NotFound(new { message = "Item name not found" });
            }

            return Ok(new
            {
                id = itemName.Id,
                name = itemName.Name,
                categoryId = itemName.CategoryId,
                categoryName = itemName.Category?.Name,
                createdAt = itemName.CreatedAt,
                updatedAt = itemName.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting item name {ItemId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the item name" });
        }
    }
}

// DTOs
public class CategorizeItemRequest
{
    public string ItemName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
}
