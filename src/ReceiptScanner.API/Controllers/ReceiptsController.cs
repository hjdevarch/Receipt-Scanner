using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Application.Interfaces;

namespace ReceiptScanner.API.Controllers;

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

            var result = await _receiptProcessingService.ProcessReceiptImageAsync(createReceiptDto);

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
    public async Task<ActionResult<ReceiptDto>> GetReceipt(Guid id)
    {
        var receipt = await _receiptProcessingService.GetReceiptByIdAsync(id);
        
        if (receipt == null)
        {
            return NotFound($"Receipt with ID {id} not found");
        }

        return Ok(receipt);
    }

    /// <summary>
    /// Get all receipts
    /// </summary>
    /// <returns>List of all receipts</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetAllReceipts()
    {
        var receipts = await _receiptProcessingService.GetAllReceiptsAsync();
        return Ok(receipts);
    }

    /// <summary>
    /// Get receipts by merchant ID
    /// </summary>
    /// <param name="merchantId">Merchant ID</param>
    /// <returns>List of receipts for the specified merchant</returns>
    [HttpGet("merchant/{merchantId}")]
    [ProducesResponseType(typeof(IEnumerable<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetReceiptsByMerchant(Guid merchantId)
    {
        var receipts = await _receiptProcessingService.GetReceiptsByMerchantAsync(merchantId);
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
    public async Task<ActionResult<IEnumerable<ReceiptDto>>> GetReceiptsByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate > endDate)
        {
            return BadRequest("Start date cannot be greater than end date");
        }

        var receipts = await _receiptProcessingService.GetReceiptsByDateRangeAsync(startDate, endDate);
        return Ok(receipts);
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
    public async Task<IActionResult> DeleteReceipt(Guid id)
    {
        var receipt = await _receiptProcessingService.GetReceiptByIdAsync(id);
        if (receipt == null)
        {
            return NotFound($"Receipt with ID {id} not found");
        }

        var success = await _receiptProcessingService.DeleteReceiptAsync(id);
        if (!success)
        {
            return StatusCode(500, "Failed to delete receipt");
        }

        return NoContent();
    }
}