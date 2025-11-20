# Rate Limiting Implementation Summary

## Overview
Implemented comprehensive API rate limiting using ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` package to prevent API abuse and ensure fair resource usage.

## Changes Made

### 1. Program.cs
- Added `System.Threading.RateLimiting` and `Microsoft.AspNetCore.RateLimiting` using statements
- Configured rate limiting with 4 policies:
  - **Global Policy**: 100 requests/minute per user (Fixed Window)
  - **Auth Policy**: 20 requests/15 minutes per IP (Sliding Window)
  - **Upload Policy**: 10 requests/minute per user (Fixed Window)
  - **Read-Only Policy**: 200 requests/minute per user (Sliding Window)
- Added `app.UseRateLimiter()` middleware after HTTPS redirection
- Configured 429 response with custom JSON message and Retry-After header

### 2. appsettings.json & appsettings.Development.json
Added `RateLimiting` configuration section:
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

### 3. AuthController.cs
- Added `using Microsoft.AspNetCore.RateLimiting`
- Applied `[EnableRateLimiting("auth")]` attribute to entire controller
- Protects all authentication endpoints (login, register, forgot password, etc.)

### 4. ReceiptsController.cs
- Added `using Microsoft.AspNetCore.RateLimiting`
- Applied `[EnableRateLimiting("upload")]` to `/api/receipts/upload` endpoint
- Applied `[EnableRateLimiting("readonly")]` to GET endpoints:
  - `/api/receipts`
  - `/api/receipts/paged`

### 5. Documentation
Created comprehensive documentation files:
- **RATE_LIMITING_DOCUMENTATION.md**: Full guide with configuration, testing, troubleshooting
- **TestRateLimiting.http**: HTTP test file for manual rate limit testing

## Rate Limiting Policies

| Policy | Endpoints | Limit | Window | Algorithm | Partition Key |
|--------|-----------|-------|--------|-----------|---------------|
| Global | Default | 100 req | 1 min | Fixed Window | User ID / IP |
| Auth | `/api/auth/*` | 20 req | 15 min | Sliding Window | IP Address |
| Upload | `/api/receipts/upload` | 10 req | 1 min | Fixed Window | User ID / IP |
| Read-Only | GET endpoints | 200 req | 1 min | Sliding Window | User ID / IP |

## Key Features

✅ **Configurable**: Enable/disable via appsettings.json  
✅ **User-Based Partitioning**: Authenticated users tracked by User ID  
✅ **IP-Based Fallback**: Anonymous users tracked by IP address  
✅ **Custom 429 Response**: JSON error with retry-after information  
✅ **Multiple Algorithms**: Fixed Window and Sliding Window support  
✅ **Endpoint-Specific Policies**: Different limits for different operations  
✅ **Zero Queue**: Immediate rejection when limit exceeded (configurable)  

## Testing

### Quick Test (PowerShell)
```powershell
# Test auth rate limiting (should fail after 20 requests)
1..25 | ForEach-Object {
    $response = Invoke-WebRequest -Uri "http://192.168.0.68:5091/api/auth/login" `
        -Method POST -ContentType "application/json" `
        -Body '{"email":"test@test.com","password":"Test123!"}' `
        -UseBasicParsing -ErrorAction SilentlyContinue
    Write-Host "Request $_`: Status $($response.StatusCode)"
}
```

### Expected Response When Rate Limited
```json
HTTP/1.1 429 Too Many Requests
Retry-After: 45
Content-Type: application/json

{
  "error": "Too Many Requests",
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 45
}
```

## Architecture

### Middleware Pipeline Order
```
RequestLoggingMiddleware
    ↓
UseHttpsRedirection
    ↓
UseRateLimiter ⬅️ Rate limiting enforced here
    ↓
UseAuthentication
    ↓
UseAuthorization
    ↓
Controllers
```

### Partition Strategy
- **Authenticated Users**: `ClaimTypes.NameIdentifier` from JWT
- **Anonymous Users**: `HttpContext.Connection.RemoteIpAddress`

This ensures fair usage per user and prevents shared IP issues.

## Configuration Options

### Disable Rate Limiting
Set in appsettings.json:
```json
{
  "RateLimiting": {
    "Enabled": false
  }
}
```

### Adjust Limits
Modify policy limits:
```json
{
  "RateLimiting": {
    "PermitLimit": 200,        // Global: 200 req/min
    "AuthPermitLimit": 50,     // Auth: 50 req/15min
    "UploadPermitLimit": 20,   // Upload: 20 req/min
    "ReadOnlyPermitLimit": 500 // Read: 500 req/min
  }
}
```

### Enable Request Queuing
```json
{
  "RateLimiting": {
    "QueueLimit": 5  // Queue up to 5 requests when limit reached
  }
}
```

## Benefits

1. **Prevents API Abuse**: Protects against credential stuffing, brute force, and DOS attacks
2. **Fair Resource Allocation**: Ensures all users get equal access
3. **Performance Protection**: Prevents resource exhaustion from excessive requests
4. **Security**: Limits authentication endpoint attacks
5. **Cost Control**: Reduces unnecessary processing and third-party API calls (Azure Document Intelligence)

## Next Steps

1. **Monitor 429 Responses**: Track rate limit hits in production
2. **Adjust Limits**: Fine-tune based on actual usage patterns
3. **Add Metrics**: Consider logging rate limit violations for analysis
4. **Consider Redis**: For distributed rate limiting across multiple instances
5. **User Tiers**: Implement different limits for premium users

## Related Files
- `src/ReceiptScanner.API/Program.cs` - Rate limiting configuration
- `src/ReceiptScanner.API/Controllers/AuthController.cs` - Auth policy
- `src/ReceiptScanner.API/Controllers/ReceiptsController.cs` - Upload and read-only policies
- `appsettings.json` - Configuration settings
- `RATE_LIMITING_DOCUMENTATION.md` - Full documentation
- `TestRateLimiting.http` - Test cases

## Troubleshooting

**Issue**: All requests return 429  
**Solution**: Check `PermitLimit` values in appsettings.json - may be too restrictive

**Issue**: Rate limiting not working  
**Solution**: Verify `RateLimiting:Enabled` is `true` and app restarted

**Issue**: Different users hitting same limit  
**Solution**: Ensure users are authenticated - authenticated users are tracked separately by User ID

For more details, see `RATE_LIMITING_DOCUMENTATION.md`.
