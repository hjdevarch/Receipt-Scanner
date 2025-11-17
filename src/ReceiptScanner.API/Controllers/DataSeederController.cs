using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Infrastructure.Data;
using Swashbuckle.AspNetCore.Annotations;

namespace ReceiptScanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DataSeederController : ControllerBase
{
    private readonly ReceiptScannerDbContext _context;
    private readonly ILogger<DataSeederController> _logger;

    public DataSeederController(ReceiptScannerDbContext context, ILogger<DataSeederController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("seed")]
    [SwaggerOperation(Summary = "Seed dummy data for testing", Description = "Creates fake receipts and items for a user using Bogus library")]
    [SwaggerResponse(200, "Data seeded successfully")]
    [SwaggerResponse(400, "Invalid parameters")]
    [SwaggerResponse(404, "User not found")]
    public async Task<IActionResult> SeedDummyData(
        [FromQuery, SwaggerParameter("User ID to seed data for")] string userId,
        [FromQuery, SwaggerParameter("Number of receipts to create")] int receiptsCount = 100,
        [FromQuery, SwaggerParameter("Maximum items per receipt")] int maxReceiptItemsCount = 20)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId is required");

        if (receiptsCount <= 0)
            return BadRequest("receiptsCount must be greater than 0");

        if (maxReceiptItemsCount <= 0)
            return BadRequest("maxReceiptItemsCount must be greater than 0");

        try
        {
            var seeder = new DatabaseSeeder(_context);
            await seeder.SeedDummyDataAsync(userId, receiptsCount, maxReceiptItemsCount);

            return Ok(new
            {
                message = "Dummy data seeded successfully",
                userId = userId,
                receiptsCreated = receiptsCount,
                maxItemsPerReceipt = maxReceiptItemsCount
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "User not found: {UserId}", userId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding dummy data for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while seeding data", details = ex.Message });
        }
    }

    [HttpDelete("clear")]
    [SwaggerOperation(Summary = "Clear all data for a user", Description = "Deletes all receipts, items, and merchants for the specified user")]
    [SwaggerResponse(200, "Data cleared successfully")]
    [SwaggerResponse(400, "Invalid parameters")]
    public async Task<IActionResult> ClearUserData([FromQuery, SwaggerParameter("User ID to clear data for")] string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("userId is required");

        try
        {
            var seeder = new DatabaseSeeder(_context);
            await seeder.ClearDummyDataAsync(userId);

            return Ok(new
            {
                message = "User data cleared successfully",
                userId = userId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing data for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while clearing data", details = ex.Message });
        }
    }
}
