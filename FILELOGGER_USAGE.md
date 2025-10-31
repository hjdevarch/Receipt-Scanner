# FileLogger Usage Guide

## Overview
The `FileLogger` is a static helper class that provides a generic method to log any model/DTO to a text file. It's accessible from any controller in the application.

## Location
- **Namespace**: `ReceiptScanner.API.Helpers`
- **File**: `src/ReceiptScanner.API/Helpers/FileLogger.cs`
- **Log Directory**: `{ProjectRoot}/Logs/RequestPayloads/`

## Method Signature
```csharp
public static async Task LogModelToFileAsync<T>(
    T model, 
    ILogger logger, 
    string? fileName = null, 
    Dictionary<string, string>? additionalInfo = null)
```

## Parameters
- **model** (required): The model/DTO instance to log
- **logger** (required): ILogger instance for logging messages (usually injected in controller)
- **fileName** (optional): Custom file name (without extension). If not provided, uses `{ModelType}_{timestamp}.txt`
- **additionalInfo** (optional): Dictionary of key-value pairs for additional context

## Usage Examples

### 1. Basic Usage in ReceiptsController
```csharp
using ReceiptScanner.API.Helpers;

// Log UpdateReceiptDto with custom filename and metadata
await FileLogger.LogModelToFileAsync(
    updateReceiptDto, 
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
```

### 2. Simple Usage (Auto-generated filename)
```csharp
using ReceiptScanner.API.Helpers;

// Just log the model with default filename
await FileLogger.LogModelToFileAsync(createReceiptDto, _logger);
// Creates file: CreateReceiptDto_20251031_143022_456.txt
```

### 3. Usage in MerchantsController
```csharp
using ReceiptScanner.API.Helpers;
using System.Security.Claims;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MerchantsController : ControllerBase
{
    private readonly ILogger<MerchantsController> _logger;
    
    public MerchantsController(ILogger<MerchantsController> logger)
    {
        _logger = logger;
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateMerchant([FromBody] CreateMerchantDto merchantDto)
    {
        // Log the incoming request
        await FileLogger.LogModelToFileAsync(
            merchantDto,
            _logger,
            fileName: $"CreateMerchant_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt",
            additionalInfo: new Dictionary<string, string>
            {
                { "UserId", User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown" },
                { "Action", "Create Merchant" }
            });
        
        // Process merchant creation...
        return Ok();
    }
}
```

### 4. Usage in SettingsController
```csharp
using ReceiptScanner.API.Helpers;

[HttpPut]
public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsDto settingsDto)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    
    // Log settings update with minimal info
    await FileLogger.LogModelToFileAsync(
        settingsDto,
        _logger,
        additionalInfo: new Dictionary<string, string>
        {
            { "UserId", userId ?? "Unknown" }
        });
    
    // Update settings...
    return Ok();
}
```

### 5. Logging Multiple Objects
```csharp
// Log request
await FileLogger.LogModelToFileAsync(
    requestDto, 
    _logger, 
    fileName: $"Request_{requestId}.txt");

// Process...

// Log response
await FileLogger.LogModelToFileAsync(
    responseDto, 
    _logger, 
    fileName: $"Response_{requestId}.txt");
```

## Log File Format
```
================================================================================
Log Entry: UpdateReceiptDto
Timestamp (UTC): 2025-10-31 14:30:22.456
Timestamp (Local): 2025-10-31 10:30:22.456
================================================================================

Additional Information:
  ReceiptId: 123e4567-e89b-12d3-a456-426614174000
  UserId: 456e4567-e89b-12d3-a456-426614174001
  Endpoint: PUT /api/receipts/{id}
  UserAgent: Mozilla/5.0...
  RemoteIpAddress: 192.168.1.100

Model Data (JSON):
{
  "receiptNumber": "RCP-001",
  "receiptDate": "2025-10-31T00:00:00",
  "subTotal": 45.99,
  "taxAmount": 3.68,
  "totalAmount": 49.67,
  "currency": "GBP",
  "currencySymbol": "£",
  "merchant": {
    "name": "Sample Store",
    "address": "123 Main St"
  },
  "items": [
    {
      "name": "Item 1",
      "quantity": 2,
      "totalPrice": 19.98
    }
  ]
}

================================================================================
```

## Benefits
✅ **Reusable** - Use in any controller throughout the application
✅ **Type-safe** - Generic method works with any model type
✅ **Async** - Non-blocking file I/O operations
✅ **Structured** - Consistent log format with timestamps and metadata
✅ **Auto-directory creation** - Creates log directory if it doesn't exist
✅ **Error handling** - Catches exceptions and logs them without breaking the request
✅ **Flexible** - Custom filenames and additional information supported

## File Location
All log files are saved to:
```
{ProjectRoot}/Logs/RequestPayloads/
```

## Best Practices
1. Always pass the controller's `_logger` instance
2. Include relevant identifiers (IDs, UserIds) in `additionalInfo`
3. Use descriptive file names with timestamps
4. Log sensitive data carefully (consider data privacy regulations)
5. Implement log rotation/cleanup for production environments
6. Consider adding log size limits for large models
