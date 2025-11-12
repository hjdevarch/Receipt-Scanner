# Forgot Password API Documentation

## Overview
The Receipt Scanner API now includes forgot password functionality that allows users to reset their passwords securely using email-based password reset tokens.

## New Endpoints

### 1. Forgot Password
Initiates the password reset process by generating a reset token for the user.

**Endpoint:** `POST /api/auth/forgot-password`

**Request Body:**
```json
{
    "email": "user@example.com"
}
```

**Response:**
```json
{
    "success": true,
    "message": "If an account with that email exists, a password reset link has been sent.",
    "token": null,
    "user": null,
    "errors": []
}
```

**Notes:**
- For security reasons, the API returns a successful response even if the email doesn't exist
- In production, the reset token would be sent via email
- Currently, the token is returned in the response for testing purposes

### 2. Reset Password
Resets the user's password using the provided reset token.

**Endpoint:** `POST /api/auth/reset-password`

**Request Body:**
```json
{
    "email": "user@example.com",
    "token": "ABC123XYZ789",
    "newPassword": "NewPassword123!",
    "confirmPassword": "NewPassword123!"
}
```

**Response:**
```json
{
    "success": true,
    "message": "Password reset successfully.",
    "token": null,
    "user": null,
    "errors": []
}
```

## Database Changes

Added two new fields to the `ApplicationUser` entity:
- `PasswordResetToken` (string, nullable): Stores the generated reset token
- `PasswordResetTokenExpiryTime` (DateTime, nullable): Stores the token expiration time

## Security Features

1. **Token Expiration**: Reset tokens expire after 1 hour
2. **One-time Use**: Tokens are cleared after successful password reset
3. **Email Validation**: Only valid email addresses can request password resets
4. **Password Validation**: New passwords must meet minimum security requirements (6+ characters)
5. **Token Uniqueness**: Each reset token is cryptographically generated and unique

## Testing

Use the `TestForgotPassword.http` file to test the functionality:

1. First register a test user
2. Request a forgot password token
3. Use the returned token to reset the password
4. Verify the password was reset by logging in with the new password

## Future Improvements

1. **Email Integration**: Integrate with an email service to send reset tokens via email
2. **Rate Limiting**: Add rate limiting to prevent abuse of the forgot password endpoint
3. **Audit Logging**: Add detailed logging for password reset attempts
4. **Token Cleanup**: Add a background service to clean up expired tokens

## Error Handling

The API handles various error scenarios:
- Invalid email addresses
- Expired reset tokens
- Invalid reset tokens
- Password validation failures
- Database errors

All errors are returned in a consistent format with appropriate HTTP status codes.