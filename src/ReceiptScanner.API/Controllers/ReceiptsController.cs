using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Application.Interfaces;
using ReceiptScanner.API.Helpers;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace ReceiptScanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptProcessingService _receiptProcessingService;
    private readonly ILogger<ReceiptsController> _logger;

    public ReceiptsController(IReceiptProcessingService receiptProcessingService, ILogger<ReceiptsController> logger)
    {
        _receiptProcessingService = receiptProcessingService;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// Upload and process a receipt image
    /// </summary>
    /// <param name="receiptImage">The receipt image file</param>
    /// <param name="receiptNumber">Optional receipt number override</param>
    /// <param name="receiptDate">Optional receipt date override</param>
    /// <returns>Processing result with extracted receipt data</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ReceiptProcessingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Upload and process a receipt image",
        Description = "Uploads a receipt image and processes it using Azure Document Intelligence to extract receipt data including merchant information, items, and amounts. Supports JPEG, PNG, BMP, TIFF, and PDF formats up to 10MB.",
        OperationId = "UploadReceipt",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipt processed successfully", typeof(ReceiptProcessingResultDto))]
    [SwaggerResponse(400, "Bad request - invalid file type, size, or missing file", typeof(string))]
    [SwaggerResponse(500, "Internal server error during processing", typeof(string))]
    public async Task<ActionResult<ReceiptProcessingResultDto>> UploadReceipt(
        IFormFile receiptImage,
        [FromForm] string? receiptNumber = null,
        [FromForm] DateTime? receiptDate = null)
    {
        try
        {
            if (receiptImage == null || receiptImage.Length == 0)
            {
                return BadRequest("No receipt image provided");
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/bmp", "image/tiff", "application/pdf" };
            if (!allowedTypes.Contains(receiptImage.ContentType.ToLower()))
            {
                return BadRequest("Invalid file type. Supported types: JPEG, PNG, BMP, TIFF, PDF");
            }

            // Validate file size (10MB max)
            if (receiptImage.Length > 10 * 1024 * 1024)
            {
                return BadRequest("File size cannot exceed 10MB");
            }

            var createReceiptDto = new CreateReceiptDto
            {
                ReceiptImage = receiptImage,
                ReceiptNumber = receiptNumber,
                ReceiptDate = receiptDate
            };

            var userId = GetUserId();
            var result = await _receiptProcessingService.ProcessReceiptImageAsync(createReceiptDto, userId);

            if (!result.IsSuccess)
            {
                return BadRequest(result.ErrorMessage);
            }

            _logger.LogInformation("Receipt processed successfully. Receipt ID: {ReceiptId}", result.ReceiptId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading receipt");
            return StatusCode(500, "An error occurred while processing the receipt");
        }
    }

    /// <summary>
    /// Get a specific receipt by ID
    /// </summary>
    /// <param name="id">Receipt ID</param>
    /// <returns>Receipt details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReceiptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get a specific receipt by ID",
        Description = "Retrieves detailed information about a specific receipt including merchant details, items, and currency information.",
        OperationId = "GetReceipt",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipt found", typeof(ReceiptDto))]
    [SwaggerResponse(404, "Receipt not found")]
    public async Task<ActionResult<ReceiptDto>> GetReceipt(Guid id)
    {
        var userId = GetUserId();
        var receipt = await _receiptProcessingService.GetReceiptByIdAsync(id, userId);
        
        if (receipt == null)
        {
            return NotFound($"Receipt with ID {id} not found");
        }

        return Ok(receipt);
    }

    /// <summary>
    /// Get all receipts
    /// </summary>
    /// <returns>List of all receipts with currency symbols</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReceiptDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Get all receipts",
        Description = "Retrieves a list of all receipts in the system with complete details including currency codes and symbols.",
        OperationId = "GetAllReceipts",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "List of receipts retrieved successfully", typeof(IEnumerable<ReceiptDto>))]
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetAllReceipts()
    {
        var userId = GetUserId();
        var receipts = await _receiptProcessingService.GetAllReceiptsAsync(userId);
        return Ok(receipts);
    }

    /// <summary>
    /// Get receipts by merchant ID
    /// </summary>
    /// <param name="merchantId">Merchant ID</param>
    /// <returns>List of receipts for the specified merchant</returns>
    [HttpGet("merchant/{merchantId}")]
    [ProducesResponseType(typeof(IEnumerable<ReceiptDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Get receipts by merchant ID",
        Description = "Retrieves all receipts associated with a specific merchant.",
        OperationId = "GetReceiptsByMerchant",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipts for the merchant retrieved successfully", typeof(IEnumerable<ReceiptDto>))]
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetReceiptsByMerchant(Guid merchantId)
    {
        var userId = GetUserId();
        var receipts = await _receiptProcessingService.GetReceiptsByMerchantAsync(merchantId, userId);
        return Ok(receipts);
    }

    /// <summary>
    /// Get receipts within a date range
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <returns>List of receipts within the specified date range</returns>
    [HttpGet("date-range")]
    [ProducesResponseType(typeof(IEnumerable<ReceiptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [SwaggerOperation(
        Summary = "Get receipts within a date range",
        Description = "Retrieves all receipts that fall within the specified date range (inclusive).",
        OperationId = "GetReceiptsByDateRange",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipts within date range retrieved successfully", typeof(IEnumerable<ReceiptDto>))]
    [SwaggerResponse(400, "Invalid date range - start date cannot be greater than end date")]
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetReceiptsByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate > endDate)
        {
            return BadRequest("Start date cannot be greater than end date");
        }

        var userId = GetUserId();
        var receipts = await _receiptProcessingService.GetReceiptsByDateRangeAsync(startDate, endDate, userId);
        return Ok(receipts);
    }

    /// <summary>
    /// Get receipts by category ID
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <returns>List of receipts containing items in the specified category</returns>
    [HttpGet("category/{categoryId}")]
    [ProducesResponseType(typeof(IEnumerable<ReceiptDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Get receipts by category ID",
        Description = "Retrieves all receipts for the current user that contain at least one item in the specified category.",
        OperationId = "GetReceiptsByCategory",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipts for the category retrieved successfully", typeof(IEnumerable<ReceiptDto>))]
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetReceiptsByCategory(Guid categoryId)
    {
        var userId = GetUserId();
        var allReceipts = await _receiptProcessingService.GetAllReceiptsAsync(userId);
        
        // Filter receipts that contain at least one item with the specified category
        var receiptsWithCategory = allReceipts
            .Where(r => r.Items != null && r.Items.Any(i => i.CategoryId == categoryId))
            .ToList();
        
        return Ok(receiptsWithCategory);
    }

    /// <summary>
    /// Get all receipt items by category ID
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <returns>List of receipt items in the specified category</returns>
    [HttpGet("items/category/{categoryId?}")]
    [ProducesResponseType(typeof(IEnumerable<ReceiptItemDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Get all receipt items by category ID",
        Description = "Retrieves all receipt items for the current user that belong to the specified category. If no category ID is provided, returns all items.",
        OperationId = "GetReceiptItemsByCategory",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipt items for the category retrieved successfully", typeof(IEnumerable<ReceiptItemDto>))]
    public async Task<ActionResult<IEnumerable<ReceiptItemDto>>> GetReceiptItemsByCategory(Guid? categoryId = null)
    {
        var userId = GetUserId();
        var allReceipts = await _receiptProcessingService.GetAllReceiptsAsync(userId);
        
        // Extract and flatten all items that match the category
        var itemsInCategory = allReceipts
            .Where(r => r.Items != null)
            .SelectMany(r => r.Items)
            .Where(i => i.CategoryId == categoryId)
            .ToList();
        
        return Ok(itemsInCategory);
    }

    /// <summary>
    /// Update an existing receipt
    /// </summary>
    /// <param name="id">Receipt ID</param>
    /// <param name="updateReceiptDto">Updated receipt data</param>
    /// <returns>Updated receipt processing result</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReceiptProcessingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Update an existing receipt",
        Description = "Updates the details of an existing receipt including amounts, currency, merchant information, and items.",
        OperationId = "UpdateReceipt",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipt updated successfully", typeof(ReceiptProcessingResultDto))]
    [SwaggerResponse(400, "Invalid update data", typeof(string))]
    [SwaggerResponse(404, "Receipt not found")]
    [SwaggerResponse(500, "Internal server error during update", typeof(string))]
    public async Task<ActionResult<ReceiptProcessingResultDto>> UpdateReceipt(Guid id, [FromBody] UpdateReceiptDto updateReceiptDto)
    {
        try
        {
            _logger.LogInformation("=== UPDATE RECEIPT REQUEST ===");
            _logger.LogInformation("Receipt ID: {ReceiptId}", id);
            
            if (updateReceiptDto == null)
            {
                _logger.LogWarning("Update data is null");
                return BadRequest("Update data cannot be null");
            }

            // Log the UpdateReceiptDto to a text file using the generic logger
            var userId = GetUserId();
            await FileLogger.LogModelToFileAsync(updateReceiptDto, 
                _logger,
                fileName: $"UpdateReceipt_{id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt",
                additionalInfo: new Dictionary<string, string>
                {
                    { "ReceiptId", id.ToString() },
                    { "UserId", userId },
                    { "Endpoint", "PUT /api/receipts/{id}" },
                    { "UserAgent", Request.Headers["User-Agent"].ToString() },
                    { "RemoteIpAddress", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown" }
                });

            var result = await _receiptProcessingService.UpdateReceiptAsync(id, updateReceiptDto, userId);

            if (!result.IsSuccess)
            {
                if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(result.ErrorMessage);
                }
                return BadRequest(result.ErrorMessage);
            }

            _logger.LogInformation("Receipt updated successfully. Receipt ID: {ReceiptId}", result.ReceiptId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating receipt with ID: {ReceiptId}", id);
            return StatusCode(500, "An error occurred while updating the receipt");
        }
    }

    /// <summary>
    /// Update the category of a receipt item
    /// </summary>
    /// <param name="updateDto">Receipt item ID and new category ID</param>
    /// <returns>Success status</returns>
    [HttpPatch("items/category")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Update the category of a receipt item",
        Description = "Updates the category of a specific receipt item by finding the associated ItemName record and updating its CategoryId. This affects all receipt items that reference the same ItemName.",
        OperationId = "UpdateReceiptItemCategory",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(200, "Receipt item category updated successfully")]
    [SwaggerResponse(400, "Invalid request data", typeof(string))]
    [SwaggerResponse(404, "Receipt item not found or does not have an associated ItemName")]
    [SwaggerResponse(500, "Internal server error during update")]
    public async Task<IActionResult> UpdateReceiptItemCategory([FromBody] UpdateReceiptItemCategoryDto updateDto)
    {
        try
        {
            if (updateDto == null)
            {
                return BadRequest("Update data cannot be null");
            }

            var userId = GetUserId();
            var success = await _receiptProcessingService.UpdateReceiptItemCategoryAsync(
                updateDto.ReceiptItemId, 
                updateDto.CategoryId, 
                userId);

            if (!success)
            {
                return NotFound("Receipt item not found, user is not the owner, or item does not have an associated ItemName");
            }

            _logger.LogInformation("Receipt item {ReceiptItemId} category updated to {CategoryId}", 
                updateDto.ReceiptItemId, updateDto.CategoryId);
            
            return Ok(new { message = "Category updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating receipt item category");
            return StatusCode(500, "An error occurred while updating the receipt item category");
        }
    }

    /// <summary>
    /// Delete a receipt
    /// </summary>
    /// <param name="id">Receipt ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Delete a receipt",
        Description = "Permanently deletes a receipt and all associated data from the system.",
        OperationId = "DeleteReceipt",
        Tags = new[] { "Receipts" }
    )]
    [SwaggerResponse(204, "Receipt deleted successfully")]
    [SwaggerResponse(404, "Receipt not found")]
    [SwaggerResponse(500, "Internal server error during deletion")]
    public async Task<IActionResult> DeleteReceipt(Guid id)
    {
        var userId = GetUserId();
        var receipt = await _receiptProcessingService.GetReceiptByIdAsync(id, userId);
        if (receipt == null)
        {
            return NotFound($"Receipt with ID {id} not found");
        }

        var success = await _receiptProcessingService.DeleteReceiptAsync(id, userId);
        if (!success)
        {
            return StatusCode(500, "Failed to delete receipt");
        }

        return NoContent();
    }
}