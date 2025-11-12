# Quick Start Guide - Email Activation

## For Developers

### 1. Register a New User
```http
POST https://localhost:7091/api/Auth/register
Content-Type: application/json

{
  "email": "newuser@example.com",
  "password": "SecurePass123!",
  "confirmPassword": "SecurePass123!",
  "userName": "newuser"
}
```

**Expected Response:**
```json
{
  "success": true,
  "message": "Registration successful. Please check your email to activate your account.",
  "user": { ... },
  "token": null
}
```

**Action**: Check email inbox for activation link

---

### 2. Try to Login (Will Fail Before Activation)
```http
POST https://localhost:7091/api/Auth/login
Content-Type: application/json

{
  "email": "newuser@example.com",
  "password": "SecurePass123!"
}
```

**Expected Response:**
```json
{
  "success": false,
  "message": "Please activate your account. An activation email has been sent to your email address.",
  "errors": ["Email not confirmed"]
}
```

---

### 3. Activate Account
Click the link in the email or manually call:

```http
GET https://localhost:7091/api/Auth/confirm-email?userId=USER_ID&token=CONFIRMATION_TOKEN
```

**Expected Response:**
```json
{
  "success": true,
  "message": "Email confirmed successfully. You can now log in.",
  "user": { ... }
}
```

---

### 4. Login Successfully
```http
POST https://localhost:7091/api/Auth/login
Content-Type: application/json

{
  "email": "newuser@example.com",
  "password": "SecurePass123!"
}
```

**Expected Response:**
```json
{
  "success": true,
  "message": "Login successful.",
  "token": {
    "accessToken": "eyJhbGc...",
    "refreshToken": "...",
    "accessTokenExpiration": "...",
    "refreshTokenExpiration": "..."
  },
  "user": { ... }
}
```

---

### 5. Resend Activation Email (Optional)
If user didn't receive the email:

```http
POST https://localhost:7091/api/Auth/resend-activation
Content-Type: application/json

{
  "email": "newuser@example.com"
}
```

**Expected Response:**
```json
{
  "message": "Activation email has been sent. Please check your inbox."
}
```

---

## For Testing

Use the provided `TestEmailActivation.http` file in VS Code with REST Client extension:

1. Open `TestEmailActivation.http`
2. Click "Send Request" above each request
3. Follow the workflow in order
4. Check email for activation link
5. Copy userId and token from email to test manual confirmation

---

## Email Configuration

Located in `appsettings.json`:

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

**Note**: Email will be sent from `NoReply@devarch.co.uk` using IONOS SMTP server.

---

## Troubleshooting

### Email Not Arriving
1. Check spam/junk folder
2. Verify SMTP settings are correct
3. Check API logs for email sending errors
4. Use resend activation endpoint

### Activation Link Not Working
1. Ensure link hasn't expired (24-hour validity)
2. Check userId and token are correctly URL-encoded
3. Verify BaseUrl in ApplicationSettings matches running API

### Can't Login After Activation
1. Clear browser cache
2. Verify email confirmation in database: `SELECT EmailConfirmed FROM AspNetUsers WHERE Email = 'user@example.com'`
3. Try resending activation and confirming again

---

## Database Check

To verify email confirmation status:

```sql
SELECT Id, Email, EmailConfirmed, CreatedAt 
FROM AspNetUsers 
WHERE Email = 'newuser@example.com'
```

`EmailConfirmed` should be `1` (true) after activation.

---

## Swagger UI

Access Swagger documentation at:
- **URL**: https://localhost:7091/swagger
- **Endpoints**: All authentication endpoints including new email activation endpoints
- **Try it out**: Test endpoints directly from browser

---

## Quick Command Reference

### Build Project
```bash
dotnet build .\ReceiptScanner.sln
```

### Run API
```bash
cd src\ReceiptScanner.API
dotnet run
```

### Check Logs
Look for these log entries:
- `Registration attempt for email: {Email}`
- `Email sent successfully to {Email}`
- `Email confirmation attempt for user: {UserId}`
- `Email confirmed successfully for user: {UserId}`

---

## Error Messages Reference

| Message | Meaning | Action |
|---------|---------|--------|
| "Please activate your account..." | Email not confirmed | Check email and click activation link |
| "Invalid confirmation link" | Missing userId or token | Use proper activation link from email |
| "Email confirmation failed. The link may have expired." | Token expired or invalid | Request new activation email |
| "Email is already confirmed." | Account already active | Proceed to login |
| "User not found" | Invalid userId | Verify userId in link |

---

**Ready to Test!** 🚀

Start with the registration endpoint and follow the workflow above.
