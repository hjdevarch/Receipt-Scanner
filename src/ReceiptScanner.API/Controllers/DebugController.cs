using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Infrastructure.Data;

namespace ReceiptScanner.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly ReceiptScannerDbContext _context;
    private readonly ILogger<DebugController> _logger;

    public DebugController(ReceiptScannerDbContext context, ILogger<DebugController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceiptsDebug()
    {
        try
        {
            var receipts = await _context.Receipts
                .Include(r => r.Merchant)
                .Include(r => r.Items)
                .Select(r => new
                {
                    r.Id,
                    r.ReceiptNumber,
                    r.ReceiptDate,
                    r.SubTotal,
                    r.TaxAmount,
                    r.TotalAmount,
                    r.Currency,
                    MerchantName = r.Merchant.Name,
                    ItemCount = r.Items.Count,
                    Items = r.Items.Select(i => new
                    {
                        i.Id,
                        i.Name,
                        i.Quantity,
                        i.UnitPrice,
                        i.TotalPrice,
                        i.Category,
                        i.SKU
                    }).ToList()
                })
                .ToListAsync();

            return Ok(receipts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting receipts for debug");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("receipt-items")]
    public async Task<IActionResult> GetReceiptItemsDebug()
    {
        try
        {
            var items = await _context.ReceiptItems
                .Include(ri => ri.Receipt)
                .Select(ri => new
                {
                    ri.Id,
                    ri.Name,
                    ri.Description,
                    ri.Quantity,
                    ri.UnitPrice,
                    ri.TotalPrice,
                    ri.Category,
                    ri.SKU,
                    ReceiptNumber = ri.Receipt.ReceiptNumber,
                    ReceiptDate = ri.Receipt.ReceiptDate
                })
                .ToListAsync();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting receipt items for debug");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}