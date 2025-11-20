# Rate Limiting Documentation

## Overview

The Receipt Scanner API implements comprehensive rate limiting to prevent API abuse and ensure fair resource usage across all users. Rate limiting is built using ASP.NET Core's native `Microsoft.AspNetCore.RateLimiting` package (available in .NET 7+).

## Configuration

Rate limiting can be enabled/disabled and configured via `appsettings.json`:

```json
{
  "RateLimiting": {
    "Enabled": true,
    "PermitLimit": 100,
    "WindowMinutes": 1,
    "QueueLimit": 0,
    "AuthPermitLimit": 20,
    "AuthWindowMinutes": 15,
    "UploadPermitLimit": 10,
    "UploadWindowMinutes": 1,
    "ReadOnlyPermitLimit": 200,
    "ReadOnlyWindowMinutes": 1
  }
}
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `Enabled` | Enable/disable rate limiting globally | `true` |
| `PermitLimit` | Max requests per window (global policy) | `100` |
| `WindowMinutes` | Time window in minutes (global policy) | `1` |
| `QueueLimit` | Number of requests to queue when limit reached | `0` |
| `AuthPermitLimit` | Max requests per window for auth endpoints | `20` |
| `AuthWindowMinutes` | Time window for auth endpoints | `15` |
| `UploadPermitLimit` | Max requests per window for upload endpoints | `10` |
| `UploadWindowMinutes` | Time window for upload endpoints | `1` |
| `ReadOnlyPermitLimit` | Max requests per window for GET endpoints | `200` |
| `ReadOnlyWindowMinutes` | Time window for GET endpoints | `1` |

## Rate Limiting Policies

### 1. Global Policy (Default)
- **Algorithm**: Fixed Window
- **Limit**: 100 requests per minute per user
- **Partition Key**: User ID (authenticated) or IP address (anonymous)
- **Applied To**: All endpoints without specific policy

### 2. Authentication Policy (`auth`)
- **Algorithm**: Sliding Window
- **Limit**: 20 requests per 15 minutes per IP address
- **Partition Key**: IP address (prevents credential stuffing)
- **Applied To**: All endpoints in `AuthController`
  - `/api/auth/register`
  - `/api/auth/login`
  - `/api/auth/refresh-token`
  - `/api/auth/forgot-password`
  - `/api/auth/reset-password`
  - `/api/auth/verify-email`
  - `/api/auth/resend-verification`

**Why Sliding Window?** More accurate rate limiting for security-sensitive endpoints, preventing burst attacks.

### 3. Upload Policy (`upload`)
- **Algorithm**: Fixed Window
- **Limit**: 10 requests per minute per user
- **Partition Key**: User ID (authenticated) or IP address (anonymous)
- **Applied To**: File upload endpoints
  - `/api/receipts/upload` - Receipt image upload

**Why More Restrictive?** File uploads consume more server resources (processing, storage, AI analysis).

### 4. Read-Only Policy (`readonly`)
- **Algorithm**: Sliding Window
- **Limit**: 200 requests per minute per user
- **Partition Key**: User ID (authenticated) or IP address (anonymous)
- **Applied To**: GET endpoints
  - `/api/receipts` - Get all receipts
  - `/api/receipts/paged` - Get receipts with pagination
  - `/api/receipts/grouped/*` - Grouped receipts endpoints
  - All other GET operations

**Why More Lenient?** Read operations are less resource-intensive than writes.

## Response Behavior

### When Rate Limit is Exceeded

**Status Code**: `429 Too Many Requests`

**Response Headers**:
```
Retry-After: 45
```

**Response Body**:
```json
{
  "error": "Too Many Requests",
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 45
}
```

### Example Scenarios

#### Scenario 1: Normal Usage
```http
GET /api/receipts
Authorization: Bearer eyJ...

HTTP/1.1 200 OK
```

#### Scenario 2: Rate Limit Exceeded
```http
POST /api/receipts/upload
Authorization: Bearer eyJ...

HTTP/1.1 429 Too Many Requests
Retry-After: 42
Content-Type: application/json

{
  "error": "Too Many Requests",
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 42
}
```

## Algorithm Comparison

### Fixed Window
- Requests are counted within fixed time windows (e.g., 1:00-1:01, 1:01-1:02)
- Simple and efficient
- Can allow bursts at window boundaries
- Used for: Global policy, Upload policy

### Sliding Window
- Uses multiple segments per window for smoother rate limiting
- More accurate but slightly more resource-intensive
- Prevents boundary burst attacks
- Used for: Auth policy, Read-only policy

## Testing Rate Limits

### Test Script (PowerShell)
```powershell
# Test auth endpoint rate limiting
for ($i = 1; $i -le 25; $i++) {
    Write-Host "Request $i"
    $response = Invoke-WebRequest -Uri "http://192.168.0.68:5091/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body '{"email":"test@example.com","password":"Test123!"}' `
        -UseBasicParsing `
        -ErrorAction SilentlyContinue
    
    Write-Host "Status: $($response.StatusCode)"
    
    if ($response.StatusCode -eq 429) {
        Write-Host "Rate limit hit at request $i"
        Write-Host "Retry-After: $($response.Headers['Retry-After'])"
        break
    }
    
    Start-Sleep -Milliseconds 100
}
```

### Test with HTTP File
Create `TestRateLimiting.http`:
```http
### Test 1: Upload rate limiting (should allow 10 requests per minute)
POST http://192.168.0.68:5091/api/receipts/upload
Authorization: Bearer {{$dotenv ACCESS_TOKEN}}
Content-Type: multipart/form-data; boundary=boundary

--boundary
Content-Disposition: form-data; name="receiptImage"; filename="receipt.jpg"
Content-Type: image/jpeg

< ./test-receipt.jpg
--boundary--

### Test 2: Read-only rate limiting (should allow 200 requests per minute)
GET http://192.168.0.68:5091/api/receipts/paged?pageNumber=1&pageSize=10
Authorization: Bearer {{$dotenv ACCESS_TOKEN}}

### Test 3: Auth rate limiting (should allow 20 requests per 15 minutes)
POST http://192.168.0.68:5091/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test123!"
}
```

## Best Practices

### For API Consumers
1. **Implement Exponential Backoff**: When receiving 429 responses, use exponential backoff retry logic
2. **Monitor Rate Limit Headers**: Check `Retry-After` header to know when to retry
3. **Batch Requests**: Group multiple operations when possible to reduce request count
4. **Cache Responses**: Cache GET endpoint responses to reduce redundant calls
5. **Use Webhooks**: Consider implementing webhooks instead of polling for updates

### For API Administrators
1. **Monitor Metrics**: Track 429 responses to identify potential issues or abuse
2. **Adjust Limits**: Fine-tune limits based on actual usage patterns
3. **Set QueueLimit**: Enable request queuing (`QueueLimit > 0`) for smoother user experience during bursts
4. **Regional Policies**: Consider different limits for different regions based on usage
5. **User Tiers**: Implement tiered rate limiting for premium users (requires code changes)

## Architecture Details

### Partition Strategy
- **Authenticated Users**: Partitioned by User ID from JWT claims (`ClaimTypes.NameIdentifier`)
- **Anonymous Users**: Partitioned by IP address (`HttpContext.Connection.RemoteIpAddress`)

This ensures:
- Fair usage per user account
- Protection against unauthenticated abuse
- Support for users behind shared IPs (authenticated users tracked separately)

### Middleware Pipeline Order
```
RequestLoggingMiddleware
  ↓
UseHttpsRedirection
  ↓
UseRateLimiter ← Rate limiting happens here
  ↓
UseAuthentication
  ↓
UseAuthorization
  ↓
Controllers
```

**Critical**: Rate limiter runs BEFORE authentication, allowing it to protect authentication endpoints from abuse.

## Disabling Rate Limiting

### Globally
Set `Enabled` to `false` in `appsettings.json`:
```json
{
  "RateLimiting": {
    "Enabled": false
  }
}
```

### For Specific Endpoints
Add `[DisableRateLimiting]` attribute:
```csharp
[HttpGet("health")]
[DisableRateLimiting]
public IActionResult Health()
{
    return Ok(new { status = "healthy" });
}
```

### For Entire Controller
```csharp
[ApiController]
[Route("api/[controller]")]
[DisableRateLimiting]
public class HealthController : ControllerBase
{
    // All endpoints in this controller bypass rate limiting
}
```

## Troubleshooting

### Issue: All requests return 429
**Cause**: Rate limits too restrictive or misconfigured window
**Solution**: 
1. Check `appsettings.json` configuration
2. Verify window and permit limit values
3. Check if multiple users share same IP (VPN, NAT)

### Issue: Rate limiting not working
**Cause**: Rate limiting disabled or middleware not registered
**Solution**:
1. Verify `RateLimiting:Enabled` is `true`
2. Check `Program.cs` includes `app.UseRateLimiter()`
3. Ensure middleware is in correct order (before authentication)

### Issue: Different users hitting same rate limit
**Cause**: Users behind same IP (NAT, proxy, VPN)
**Solution**: Authenticated users are tracked by User ID, not IP. Ensure users are logging in properly.

### Issue: Burst of requests at window boundary
**Cause**: Using Fixed Window algorithm
**Solution**: Switch to Sliding Window for smoother rate limiting:
```csharp
return RateLimitPartition.GetSlidingWindowLimiter(
    partitionKey: userId,
    factory: _ => new SlidingWindowRateLimiterOptions
    {
        PermitLimit = 100,
        Window = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 4 // Divides window into 4 segments
    });
```

## Future Enhancements

1. **Redis-Based Distributed Rate Limiting**: For multi-instance deployments
2. **User Tier-Based Limits**: Different limits for free/premium users
3. **Endpoint-Specific Metrics**: Track which endpoints hit limits most often
4. **Dynamic Rate Limiting**: Adjust limits based on server load
5. **Rate Limit Response Headers**: Include remaining requests and reset time
6. **Whitelist/Blacklist**: IP-based whitelist for trusted clients

## Related Documentation
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [Request Logging Documentation](./FILELOGGER_USAGE.md)
- [Authentication Guide](./AUTHENTICATION_GUIDE.md)
