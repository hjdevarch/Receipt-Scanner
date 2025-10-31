# Authentication & Security Documentation

## Overview

The Receipt Scanner API implements a comprehensive JWT-based authentication system using ASP.NET Core Identity. This provides secure user management, token-based authentication, and role-based authorization.

## Features

### ✅ User Management
- User registration with email and password
- Password validation (minimum 6 characters, requires uppercase, lowercase, and digit)
- Email uniqueness validation
- Account lockout after 5 failed login attempts (5-minute lockout period)

### ✅ JWT Token Authentication
- **Access Token**: Short-lived (60 minutes) for API access
- **Refresh Token**: Long-lived (7 days) for obtaining new access tokens
- Secure token generation using HMAC-SHA256
- Token validation on every request

### ✅ Protected Endpoints
All endpoints except authentication endpoints require a valid JWT token:
- `/api/receipts/*` - Receipt management
- `/api/merchants/*` - Merchant management
- `/api/settings/*` - Settings management

### ✅ Swagger Integration
- Swagger UI includes "Authorize" button
- Test protected endpoints directly from Swagger
- Token input field for easy testing

## Authentication Endpoints

### 1. Register User
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123",
  "confirmPassword": "SecurePass123",
  "userName": "JohnDoe"  // Optional
}
```

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "message": "User registered successfully.",
  "token": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "CfDJ8N...",
    "accessTokenExpiration": "2025-10-31T02:00:00Z",
    "refreshTokenExpiration": "2025-11-07T01:00:00Z"
  },
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "email": "user@example.com",
    "userName": "JohnDoe",
    "createdAt": "2025-10-31T01:00:00Z"
  },
  "errors": []
}
```

**Response (Error - 400 Bad Request):**
```json
{
  "success": false,
  "message": "Failed to create user.",
  "token": null,
  "user": null,
  "errors": [
    "Passwords must have at least one uppercase ('A'-'Z').",
    "Passwords must have at least one digit ('0'-'9')."
  ]
}
```

---

### 2. Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123"
}
```

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "message": "Login successful.",
  "token": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "CfDJ8N...",
    "accessTokenExpiration": "2025-10-31T02:00:00Z",
    "refreshTokenExpiration": "2025-11-07T01:00:00Z"
  },
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "email": "user@example.com",
    "userName": "JohnDoe",
    "createdAt": "2025-10-31T01:00:00Z"
  },
  "errors": []
}
```

**Response (Error - 401 Unauthorized):**
```json
{
  "success": false,
  "message": "Invalid email or password.",
  "token": null,
  "user": null,
  "errors": [
    "Invalid credentials"
  ]
}
```

---

### 3. Refresh Token
```http
POST /api/auth/refresh-token
Content-Type: application/json

{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "CfDJ8N..."
}
```

**Response (Success - 200 OK):**
```json
{
  "success": true,
  "message": "Token refreshed successfully.",
  "token": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",  // New token
    "refreshToken": "CfDJ8N...",  // New refresh token
    "accessTokenExpiration": "2025-10-31T03:00:00Z",
    "refreshTokenExpiration": "2025-11-07T02:00:00Z"
  },
  "user": {
    "id": "123e4567-e89b-12d3-a456-426614174000",
    "email": "user@example.com",
    "userName": "JohnDoe",
    "createdAt": "2025-10-31T01:00:00Z"
  },
  "errors": []
}
```

---

### 4. Get Current User
```http
GET /api/auth/me
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response (Success - 200 OK):**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "email": "user@example.com",
  "userName": "JohnDoe",
  "createdAt": "2025-10-31T01:00:00Z"
}
```

---

### 5. Logout
```http
POST /api/auth/logout
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response (Success - 200 OK):**
```json
{
  "message": "Logout successful"
}
```

---

## Using Authentication

### In Swagger UI

1. Click the **"Authorize"** button (🔓 icon) at the top right
2. Enter your token in the format: `Bearer <your-access-token>`
3. Click **"Authorize"**
4. All subsequent API calls will include the token automatically

### In HTTP Clients (Postman, REST Client, etc.)

Add the `Authorization` header to all protected requests:

```http
GET https://localhost:7127/api/receipts
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### In Code (JavaScript/TypeScript)

```javascript
// Login and get token
const loginResponse = await fetch('https://localhost:7127/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'user@example.com',
    password: 'SecurePass123'
  })
});

const { token } = await loginResponse.json();

// Store tokens
localStorage.setItem('accessToken', token.accessToken);
localStorage.setItem('refreshToken', token.refreshToken);

// Use access token for API calls
const receiptsResponse = await fetch('https://localhost:7127/api/receipts', {
  headers: {
    'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
  }
});

// Refresh token when access token expires
const refreshResponse = await fetch('https://localhost:7127/api/auth/refresh-token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    accessToken: localStorage.getItem('accessToken'),
    refreshToken: localStorage.getItem('refreshToken')
  })
});

const { token: newToken } = await refreshResponse.json();
localStorage.setItem('accessToken', newToken.accessToken);
localStorage.setItem('refreshToken', newToken.refreshToken);
```

---

## Configuration

### JWT Settings (appsettings.json)

```json
{
  "JwtSettings": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong",
    "Issuer": "ReceiptScannerAPI",
    "Audience": "ReceiptScannerClient",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  }
}
```

**Important:** Change the `Secret` value in production to a strong, randomly generated key.

### Password Requirements

- Minimum length: 6 characters
- Requires at least one uppercase letter (A-Z)
- Requires at least one lowercase letter (a-z)
- Requires at least one digit (0-9)
- Special characters: Optional (not required)

To modify these requirements, edit `ServiceCollectionExtensions.cs`:

```csharp
services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;  // Set to true to require special chars
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;  // Change minimum length
    options.Password.RequiredUniqueChars = 1;
    
    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
```

---

## Database Tables

The authentication system creates the following tables:

- **AspNetUsers** - User accounts (extends with RefreshToken, RefreshTokenExpiryTime)
- **AspNetRoles** - User roles (for future role-based authorization)
- **AspNetUserRoles** - User-role mappings
- **AspNetUserClaims** - User claims
- **AspNetUserLogins** - External login providers (Google, Facebook, etc.)
- **AspNetUserTokens** - User tokens
- **AspNetRoleClaims** - Role claims

---

## Security Best Practices

### ✅ Implemented
- ✔️ JWT tokens with HMAC-SHA256 signing
- ✔️ Refresh token rotation (new refresh token on every refresh)
- ✔️ Token expiration (access: 60 min, refresh: 7 days)
- ✔️ Password hashing with ASP.NET Core Identity
- ✔️ Account lockout protection
- ✔️ HTTPS enforcement in production
- ✔️ Unique email validation
- ✔️ Token revocation on logout

### 🔒 Production Recommendations
1. **Use strong secrets**: Generate a cryptographically secure 256-bit key
2. **Use environment variables**: Don't commit secrets to source control
3. **Enable HTTPS**: Require HTTPS in production
4. **Implement rate limiting**: Prevent brute force attacks
5. **Add email confirmation**: Verify user emails before allowing login
6. **Add two-factor authentication**: Extra security layer for sensitive operations
7. **Implement role-based authorization**: Add roles (Admin, User, etc.)
8. **Add audit logging**: Track login attempts and security events
9. **Implement CORS properly**: Restrict allowed origins in production
10. **Use secure cookie storage**: For web clients, consider HttpOnly cookies

---

## Error Codes

| Status Code | Meaning |
|------------|---------|
| 200 OK | Request successful |
| 400 Bad Request | Validation error or invalid request data |
| 401 Unauthorized | Missing, invalid, or expired token |
| 404 Not Found | User not found |
| 500 Internal Server Error | Server error |

---

## Testing the Authentication System

Use the provided `TestAuthentication.http` file:

```bash
# Open in VS Code with REST Client extension
# Or use with any HTTP client (Postman, Insomnia, etc.)
```

### Test Flow
1. **Register** a new user
2. **Login** to get access and refresh tokens
3. **Get current user** info using the access token
4. **Access protected endpoints** (receipts, merchants, settings)
5. **Refresh token** when access token expires
6. **Logout** to revoke the refresh token
7. **Test unauthorized access** (should fail with 401)

---

## Troubleshooting

### "401 Unauthorized" on protected endpoints
- ✓ Check that you're including the Authorization header
- ✓ Verify the token format: `Bearer <token>`
- ✓ Ensure the access token hasn't expired (60 minutes)
- ✓ Try refreshing the token if expired

### "Invalid email or password"
- ✓ Check email spelling (case-sensitive)
- ✓ Verify password meets requirements
- ✓ Account may be locked (wait 5 minutes)

### "User already exists"
- ✓ Use a different email address
- ✓ Or login with existing credentials

### Token refresh fails
- ✓ Both access and refresh tokens are required
- ✓ Refresh token may have expired (7 days)
- ✓ Refresh token may have been revoked (after logout)
- ✓ Re-login to get new tokens

---

## Future Enhancements

- [ ] Role-based authorization (Admin, Manager, User)
- [ ] Email confirmation workflow
- [ ] Password reset functionality
- [ ] Two-factor authentication (2FA)
- [ ] OAuth2 providers (Google, Microsoft, GitHub)
- [ ] User profile management
- [ ] Activity logging and audit trail
- [ ] API rate limiting
- [ ] Refresh token rotation with versioning
- [ ] Blacklist for revoked tokens

---

## Support

For issues or questions about authentication:
1. Check this documentation
2. Review the Swagger UI documentation at https://localhost:7127
3. Check application logs for detailed error messages
4. Test with the provided `TestAuthentication.http` file

---

**Last Updated:** October 31, 2025  
**Version:** 1.0.0
