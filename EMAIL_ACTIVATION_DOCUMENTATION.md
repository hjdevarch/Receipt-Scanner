# Email Activation Feature Documentation

## Overview
This document describes the email activation functionality implemented for the Receipt Scanner API. Users must activate their accounts via email before they can log in.

## Features Implemented

### 1. Email Configuration
Email settings are now configurable in `appsettings.json`:

```json
"EmailSettings": {
  "SmtpHost": "smtp.ionos.co.uk",
  "SmtpPort": 587,
  "SenderEmail": "NoReply@devarch.co.uk",
  "SenderName": "Receipt Scanner",
  "Username": "NoReply@devarch.co.uk",
  "Password": "KLO@1361Iran@1983",
  "EnableSsl": true
}
```

**Provider**: IONOS
**Sender Email**: NoReply@devarch.co.uk

### 2. User Registration Flow

#### Before:
- User registers → Account created → JWT tokens issued immediately → User can login

#### Now:
- User registers → Account created with `EmailConfirmed = false` → Activation email sent → User must confirm email before login

**Endpoint**: `POST /api/Auth/register`

**Request**:
```json
{
  "email": "user@example.com",
  "password": "Password123!",
  "confirmPassword": "Password123!",
  "userName": "username"
}
```

**Response** (Success):
```json
{
  "success": true,
  "message": "Registration successful. Please check your email to activate your account.",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "userName": "username",
    "createdAt": "2024-01-01T00:00:00Z"
  },
  "token": null,
  "errors": []
}
```

**Note**: No JWT tokens are issued until email is confirmed.

### 3. Login with Email Confirmation Check

**Endpoint**: `POST /api/Auth/login`

**Request**:
```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

**Response** (Email Not Confirmed):
```json
{
  "success": false,
  "message": "Please activate your account. An activation email has been sent to your email address.",
  "errors": ["Email not confirmed"],
  "token": null,
  "user": null
}
```

**Response** (Email Confirmed - Success):
```json
{
  "success": true,
  "message": "Login successful.",
  "token": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "base64-encoded-refresh-token",
    "accessTokenExpiration": "2024-01-01T01:00:00Z",
    "refreshTokenExpiration": "2024-01-08T00:00:00Z"
  },
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "userName": "username",
    "createdAt": "2024-01-01T00:00:00Z"
  },
  "errors": []
}
```

### 4. Email Confirmation

**Endpoint**: `GET /api/Auth/confirm-email`

**Query Parameters**:
- `userId` (string, required): The user's ID
- `token` (string, required): The email confirmation token

**Example URL**:
```
https://localhost:7091/api/Auth/confirm-email?userId=abc123&token=encoded-token
```

**Response** (Success):
```json
{
  "success": true,
  "message": "Email confirmed successfully. You can now log in.",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "userName": "username",
    "createdAt": "2024-01-01T00:00:00Z"
  },
  "token": null,
  "errors": []
}
```

**Response** (Invalid/Expired Token):
```json
{
  "success": false,
  "message": "Email confirmation failed. The link may have expired.",
  "errors": ["Token validation failed"],
  "token": null,
  "user": null
}
```

**Response** (Already Confirmed):
```json
{
  "success": true,
  "message": "Email is already confirmed.",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "userName": "username",
    "createdAt": "2024-01-01T00:00:00Z"
  },
  "token": null,
  "errors": []
}
```

### 5. Resend Activation Email

**Endpoint**: `POST /api/Auth/resend-activation`

**Request**:
```json
{
  "email": "user@example.com"
}
```

**Response** (Success):
```json
{
  "message": "Activation email has been sent. Please check your inbox."
}
```

**Response** (Failed):
```json
{
  "message": "Failed to resend activation email. Please try again later."
}
```

## Activation Email Template

The activation email includes:
- Welcome message with user's name
- Clear call-to-action button
- Plain text link (fallback)
- Expiration notice (24 hours)
- Professional styling

**Subject**: "Activate Your Receipt Scanner Account"

**Sample Email Body**:
```html
Welcome to Receipt Scanner, {userName}!

Thank you for registering with Receipt Scanner. To complete your registration, 
please activate your account by clicking the link below:

[Activate Account Button]

Or copy and paste this link into your browser:
https://localhost:7091/api/Auth/confirm-email?userId=abc&token=xyz

This activation link will expire in 24 hours.

If you didn't create an account, please ignore this email.

---
Receipt Scanner Team
```

## Technical Implementation

### New Files Created:

1. **EmailSettings.cs** - Configuration class for email settings
2. **ApplicationSettings.cs** - Configuration class for application settings (base URL)
3. **IEmailService.cs** - Email service interface
4. **EmailService.cs** - Email service implementation using System.Net.Mail
5. **ResendActivationDto.cs** - DTO for resend activation endpoint

### Modified Files:

1. **AuthService.cs**
   - Added `IEmailService` dependency
   - Added `ApplicationSettings` dependency
   - Modified `RegisterAsync` to set `EmailConfirmed = false` and send activation email
   - Modified `LoginAsync` to check email confirmation status
   - Added `ConfirmEmailAsync` method
   - Added `ResendActivationEmailAsync` method

2. **IAuthService.cs**
   - Added `ConfirmEmailAsync` method signature
   - Added `ResendActivationEmailAsync` method signature

3. **AuthController.cs**
   - Added `ConfirmEmail` endpoint (GET)
   - Added `ResendActivation` endpoint (POST)

4. **ServiceCollectionExtensions.cs**
   - Registered `IEmailService` and `EmailService`

5. **Program.cs**
   - Configured `EmailSettings` from appsettings
   - Configured `ApplicationSettings` from appsettings

6. **appsettings.json** & **appsettings.Development.json**
   - Added `EmailSettings` section
   - Added `ApplicationSettings` section

## Security Considerations

1. **Email Confirmation Tokens**: Generated by ASP.NET Core Identity with built-in expiration and security
2. **Token Expiry**: Tokens expire after 24 hours (configurable)
3. **URL Encoding**: Tokens are properly URL-encoded to prevent issues with special characters
4. **No Sensitive Data**: Emails don't contain passwords or other sensitive information
5. **Secure SMTP**: Uses TLS/SSL encryption for email transmission

## Testing

Use the provided `TestEmailActivation.http` file to test the complete flow:

1. Register a new user
2. Attempt to login (should fail with activation message)
3. Check email for activation link
4. Click activation link or use the confirm-email endpoint
5. Login again (should succeed)

## Configuration for Production

For production deployment, update `appsettings.json`:

```json
"EmailSettings": {
  "SmtpHost": "smtp.ionos.co.uk",
  "SmtpPort": 587,
  "SenderEmail": "NoReply@devarch.co.uk",
  "SenderName": "Receipt Scanner",
  "Username": "NoReply@devarch.co.uk",
  "Password": "YOUR_SECURE_PASSWORD",
  "EnableSsl": true
},
"ApplicationSettings": {
  "BaseUrl": "https://your-production-domain.com"
}
```

**Important**: Store email password securely using:
- Azure Key Vault
- Environment variables
- User secrets (development only)

## Error Messages

The implementation uses clear, user-friendly error messages:

✅ **Good**: "Please activate your account. An activation email has been sent to your email address."

❌ **Avoid**: "Email not confirmed" (too technical)

## Future Enhancements

Potential improvements:
1. Add email templates with HTML/CSS framework
2. Implement rate limiting for resend activation
3. Add email verification link expiration UI
4. Support for multiple languages
5. Email delivery confirmation/tracking
6. Bounce handling and retry logic

## Troubleshooting

### Email Not Sending
- Check SMTP credentials
- Verify firewall allows SMTP port (587)
- Check email service logs
- Verify IONOS account is active

### Activation Link Not Working
- Check token hasn't expired
- Verify URL encoding is correct
- Ensure BaseUrl matches the running application

### User Can't Login
- Verify email confirmation status in database
- Check `AspNetUsers.EmailConfirmed` column
- Use resend activation endpoint if needed
