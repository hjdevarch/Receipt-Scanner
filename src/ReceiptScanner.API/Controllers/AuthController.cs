using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ReceiptScanner.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    [SwaggerOperation(
        Summary = "Register a new user",
        Description = "Creates a new user account with email and password"
    )]
    [SwaggerResponse(200, "User registered successfully", typeof(AuthResponseDto))]
    [SwaggerResponse(400, "Invalid registration data", typeof(AuthResponseDto))]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto registerDto)
    {
        _logger.LogInformation("Registration attempt for email: {Email}", registerDto.Email);

        var result = await _authService.RegisterAsync(registerDto);

        if (!result.Success)
        {
            _logger.LogWarning("Registration failed for email: {Email}. Errors: {Errors}",
                registerDto.Email, string.Join(", ", result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);
        return Ok(result);
    }

    [HttpPost("login")]
    [SwaggerOperation(
        Summary = "Login user",
        Description = "Authenticates user and returns JWT access and refresh tokens"
    )]
    [SwaggerResponse(200, "Login successful", typeof(AuthResponseDto))]
    [SwaggerResponse(401, "Invalid credentials", typeof(AuthResponseDto))]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        _logger.LogInformation("Login attempt for email: {Email}", loginDto.Email);

        var result = await _authService.LoginAsync(loginDto);

        if (!result.Success)
        {
            _logger.LogWarning("Login failed for email: {Email}", loginDto.Email);
            return Unauthorized(result);
        }

        _logger.LogInformation("User logged in successfully: {Email}", loginDto.Email);
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    [SwaggerOperation(
        Summary = "Refresh access token",
        Description = "Generates new access and refresh tokens using a valid refresh token"
    )]
    [SwaggerResponse(200, "Token refreshed successfully", typeof(AuthResponseDto))]
    [SwaggerResponse(401, "Invalid or expired refresh token", typeof(AuthResponseDto))]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
    {
        _logger.LogInformation("Token refresh attempt");

        var result = await _authService.RefreshTokenAsync(refreshTokenDto);

        if (!result.Success)
        {
            _logger.LogWarning("Token refresh failed");
            return Unauthorized(result);
        }

        _logger.LogInformation("Token refreshed successfully");
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    [SwaggerOperation(
        Summary = "Logout user",
        Description = "Revokes the user's refresh token"
    )]
    [SwaggerResponse(200, "Logout successful")]
    [SwaggerResponse(401, "Unauthorized")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Logout failed: User ID not found");
            return Unauthorized(new { message = "User not authenticated" });
        }

        var result = await _authService.RevokeTokenAsync(userId);

        if (!result)
        {
            _logger.LogWarning("Logout failed for user: {UserId}", userId);
            return BadRequest(new { message = "Logout failed" });
        }

        _logger.LogInformation("User logged out successfully: {UserId}", userId);
        return Ok(new { message = "Logout successful" });
    }

    [Authorize]
    [HttpGet("me")]
    [SwaggerOperation(
        Summary = "Get current user",
        Description = "Returns the currently authenticated user's information"
    )]
    [SwaggerResponse(200, "User information retrieved", typeof(UserDto))]
    [SwaggerResponse(401, "Unauthorized")]
    [SwaggerResponse(404, "User not found")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Get current user failed: User ID not found");
            return Unauthorized(new { message = "User not authenticated" });
        }

        var user = await _authService.GetCurrentUserAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", userId);
            return NotFound(new { message = "User not found" });
        }

        return Ok(user);
    }

    [Authorize]
    [HttpPost("change-password")]
    [SwaggerOperation(
        Summary = "Change user password",
        Description = "Allows authenticated user to change their password"
    )]
    [SwaggerResponse(200, "Password changed successfully", typeof(AuthResponseDto))]
    [SwaggerResponse(400, "Invalid password data", typeof(AuthResponseDto))]
    [SwaggerResponse(401, "Unauthorized")]
    public async Task<ActionResult<AuthResponseDto>> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Change password failed: User ID not found");
            return Unauthorized(new { message = "User not authenticated" });
        }

        _logger.LogInformation("Change password attempt for user: {UserId}", userId);

        var result = await _authService.ChangePasswordAsync(userId, changePasswordDto);

        if (!result.Success)
        {
            _logger.LogWarning("Change password failed for user: {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Password changed successfully for user: {UserId}", userId);
        return Ok(result);
    }

    [HttpGet("confirm-email")]
    [SwaggerOperation(
        Summary = "Confirm user email",
        Description = "Activates a user account using the email confirmation token sent via email"
    )]
    [SwaggerResponse(200, "Email confirmed successfully", typeof(AuthResponseDto))]
    [SwaggerResponse(400, "Invalid confirmation token", typeof(AuthResponseDto))]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token, [FromQuery] bool html = true)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Email confirmation failed: Missing userId or token");
            var errorResult = new AuthResponseDto
            {
                Success = false,
                Message = "Invalid confirmation link.",
                Errors = new List<string> { "Missing userId or token" }
            };
            if (ShouldReturnHtml(html)) return Content(BuildConfirmationHtml(errorResult), "text/html");
            return BadRequest(errorResult);
        }

        _logger.LogInformation("Email confirmation attempt for user: {UserId}", userId);

        var result = await _authService.ConfirmEmailAsync(userId, token);

        if (!result.Success)
        {
            _logger.LogWarning("Email confirmation failed for user: {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors));
            if (ShouldReturnHtml(html)) return Content(BuildConfirmationHtml(result), "text/html");
            return BadRequest(result);
        }

        _logger.LogInformation("Email confirmed successfully for user: {UserId}", userId);
        if (ShouldReturnHtml(html)) return Content(BuildConfirmationHtml(result), "text/html");
        return Ok(result);
    }

    private bool ShouldReturnHtml(bool htmlFlag)
    {
        if (!htmlFlag) return false;
        var accept = Request.Headers["Accept"].ToString();
        if (string.IsNullOrWhiteSpace(accept)) return htmlFlag; // default to html if flag true and no explicit Accept
        return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) || htmlFlag;
    }

    private static string BuildConfirmationHtml(AuthResponseDto result)
    {
        var success = result.Success;
        var title = success ? "Email Confirmed" : "Email Confirmation Failed";
        var statusColor = success ? "#2e7d32" : "#c62828";
        var secondary = success ? "#4caf50" : "#f44336";
        var message = System.Net.WebUtility.HtmlEncode(result.Message);
        var userEmail = System.Net.WebUtility.HtmlEncode(result.User?.Email ?? "");
        var userName = System.Net.WebUtility.HtmlEncode(result.User?.UserName ?? userEmail);
        var errors = (result.Errors ?? new List<string>())
            .Select(e => $"<li>{System.Net.WebUtility.HtmlEncode(e)}</li>")
            .Aggregate(string.Empty, (acc, li) => acc + li);

        var errorsBlock = string.IsNullOrEmpty(errors) ? "" : $"<div style='margin-top:20px'><h3 style='color:{statusColor};margin:0 0 5px 0;font-size:16px'>Details</h3><ul style='padding-left:20px;margin:5px 0'>{errors}</ul></div>";
        var loginHint = success ? "<a href='/' style='display:inline-block;margin-top:25px;padding:12px 22px;background:#1976d2;color:#fff;text-decoration:none;border-radius:6px;font-weight:600'>Proceed to Login</a>" : "<a href='/' style='display:inline-block;margin-top:25px;padding:12px 22px;background:#555;color:#fff;text-decoration:none;border-radius:6px;font-weight:600'>Return Home</a>";

        return $@"<!DOCTYPE html>
<html lang='en'>
<head>
<meta charset='utf-8' />
<title>{title} - Receipt Scanner</title>
<meta name='viewport' content='width=device-width,initial-scale=1' />
<style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background:#f5f7fa; margin:0; padding:0; color:#222; }}
    .container {{ max-width:640px; margin:40px auto; background:#fff; box-shadow:0 4px 18px rgba(0,0,0,.08); border-radius:14px; overflow:hidden; }}
    header {{ background:{secondary}; padding:28px 34px; color:#fff; }}
    header h1 {{ margin:0; font-size:26px; font-weight:600; }}
    main {{ padding:34px; }}
    .status {{ display:flex; align-items:center; gap:14px; margin-bottom:20px; }}
    .badge {{ padding:6px 14px; background:{statusColor}; color:#fff; border-radius:18px; font-size:13px; letter-spacing:.5px; text-transform:uppercase; font-weight:600; }}
    p {{ line-height:1.55; margin:14px 0; }}
    footer {{ padding:26px 34px; background:#fafafa; font-size:12px; color:#555; text-align:center; }}
    a:hover {{ opacity:.92; }}
</style>
</head>
<body>
<div class='container'>
    <header>
        <h1>Receipt Scanner</h1>
    </header>
    <main>
        <div class='status'>
            <div class='badge'>{(success ? "SUCCESS" : "FAILED")}</div>
            <h2 style='margin:0;font-size:22px;font-weight:600;color:{statusColor}'>{title}</h2>
        </div>
        <p style='font-size:15px'>{message}</p>
        {(success ? (string.IsNullOrEmpty(userEmail) ? "" : $"<p style='font-size:14px;color:#444'>Account: <strong>{userName}</strong> ({userEmail})</p>") : "")}
        {errorsBlock}
        <div style='margin-top:25px;padding:18px;background:#e3f2fd;border-left:4px solid #1976d2;border-radius:6px'>
            <p style='margin:0;color:#1565c0;font-weight:600'>✓ You can now return to the app and log in with your account.</p>
        </div>
    </main>
    <footer>
        <p>&copy; {DateTime.UtcNow.Year} Receipt Scanner. If this action was not initiated by you, no further steps are required.</p>
    </footer>
</div>
</body>
</html>";
    }

    [HttpPost("resend-activation")]
    [SwaggerOperation(
        Summary = "Resend activation email",
        Description = "Resends the account activation email to the specified email address"
    )]
    [SwaggerResponse(200, "Activation email resent successfully")]
    [SwaggerResponse(400, "Failed to resend activation email")]
    public async Task<IActionResult> ResendActivation([FromBody] ResendActivationDto dto)
    {
        _logger.LogInformation("Resend activation email attempt for: {Email}", dto.Email);

        var result = await _authService.ResendActivationEmailAsync(dto.Email);

        if (!result)
        {
            _logger.LogWarning("Failed to resend activation email for: {Email}", dto.Email);
            return BadRequest(new { message = "Failed to resend activation email. Please try again later." });
        }

        _logger.LogInformation("Activation email resent successfully for: {Email}", dto.Email);
        return Ok(new { message = "Activation email has been sent. Please check your inbox." });
    }

    [HttpPost("forgot-password")]
    [SwaggerOperation(
        Summary = "Forgot password",
        Description = "Initiates password reset process by generating a reset token"
    )]
    [SwaggerResponse(200, "Password reset token generated", typeof(AuthResponseDto))]
    [SwaggerResponse(400, "Invalid request", typeof(AuthResponseDto))]
    public async Task<ActionResult<AuthResponseDto>> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        _logger.LogInformation("Forgot password request for email: {Email}", forgotPasswordDto.Email);

        var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);

        if (!result.Success)
        {
            _logger.LogWarning("Forgot password failed for email: {Email}", forgotPasswordDto.Email);
            return BadRequest(result);
        }

        _logger.LogInformation("Forgot password processed for email: {Email}", forgotPasswordDto.Email);
        return Ok(result);
    }

    [HttpGet("reset-password")]
    [SwaggerOperation(
        Summary = "Reset password page",
        Description = "Displays password reset form when user clicks email link"
    )]
    [SwaggerResponse(200, "Password reset form displayed", typeof(string))]
    [SwaggerResponse(400, "Invalid or expired token", typeof(string))]
    public IActionResult ResetPasswordPage([FromQuery] string email, [FromQuery] string token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            return Content(GetErrorHtml("Invalid password reset link. Please request a new password reset."), "text/html");
        }

        var html = GetResetPasswordFormHtml(email, token);
        return Content(html, "text/html");
    }

    [HttpPost("reset-password")]
    [SwaggerOperation(
        Summary = "Reset password",
        Description = "Resets user password using the provided reset token"
    )]
    [SwaggerResponse(200, "Password reset successful", typeof(AuthResponseDto))]
    [SwaggerResponse(400, "Invalid reset request", typeof(AuthResponseDto))]
    public async Task<ActionResult<AuthResponseDto>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        _logger.LogInformation("Password reset attempt for email: {Email}", resetPasswordDto.Email);

        var result = await _authService.ResetPasswordAsync(resetPasswordDto);

        if (!result.Success)
        {
            _logger.LogWarning("Password reset failed for email: {Email}", resetPasswordDto.Email);
            return BadRequest(result);
        }

        _logger.LogInformation("Password reset successful for email: {Email}", resetPasswordDto.Email);
        return Ok(result);
    }

    private string GetResetPasswordFormHtml(string email, string token)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Reset Password - Receipt Scanner</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.1);
            padding: 40px;
            max-width: 500px;
            width: 100%;
        }}
        h1 {{
            color: #333;
            margin-bottom: 10px;
            font-size: 28px;
        }}
        p {{
            color: #666;
            margin-bottom: 30px;
            line-height: 1.6;
        }}
        .form-group {{
            margin-bottom: 20px;
        }}
        label {{
            display: block;
            color: #333;
            font-weight: 600;
            margin-bottom: 8px;
        }}
        input {{
            width: 100%;
            padding: 12px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            transition: border-color 0.3s;
        }}
        input:focus {{
            outline: none;
            border-color: #667eea;
        }}
        .password-hint {{
            font-size: 12px;
            color: #999;
            margin-top: 5px;
        }}
        button {{
            width: 100%;
            padding: 14px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
        }}
        button:hover {{
            transform: translateY(-2px);
            box-shadow: 0 5px 20px rgba(102, 126, 234, 0.4);
        }}
        button:active {{
            transform: translateY(0);
        }}
        button:disabled {{
            background: #ccc;
            cursor: not-allowed;
            transform: none;
        }}
        .message {{
            padding: 12px;
            border-radius: 6px;
            margin-bottom: 20px;
            display: none;
        }}
        .message.success {{
            background: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }}
        .message.error {{
            background: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }}
        .logo {{
            text-align: center;
            margin-bottom: 30px;
            font-size: 36px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='logo'>🧾</div>
        <h1>Reset Your Password</h1>
        <p>Enter your new password below. Make sure it's strong and secure.</p>
        
        <div id='message' class='message'></div>
        
        <form id='resetForm'>
            <div class='form-group'>
                <label for='password'>New Password</label>
                <input 
                    type='password' 
                    id='password' 
                    name='password' 
                    required 
                    minlength='6'
                    placeholder='Enter new password'
                />
                <div class='password-hint'>At least 6 characters, including uppercase, lowercase, and numbers</div>
            </div>
            
            <div class='form-group'>
                <label for='confirmPassword'>Confirm Password</label>
                <input 
                    type='password' 
                    id='confirmPassword' 
                    name='confirmPassword' 
                    required 
                    placeholder='Confirm new password'
                />
            </div>
            
            <button type='submit' id='submitBtn'>Reset Password</button>
        </form>
    </div>

    <script>
        const form = document.getElementById('resetForm');
        const messageDiv = document.getElementById('message');
        const submitBtn = document.getElementById('submitBtn');
        const passwordInput = document.getElementById('password');
        const confirmPasswordInput = document.getElementById('confirmPassword');

        function showMessage(text, isSuccess) {{
            messageDiv.textContent = text;
            messageDiv.className = 'message ' + (isSuccess ? 'success' : 'error');
            messageDiv.style.display = 'block';
        }}

        form.addEventListener('submit', async (e) => {{
            e.preventDefault();
            
            const password = passwordInput.value;
            const confirmPassword = confirmPasswordInput.value;

            console.log('Form submitted');

            // Validate passwords match
            if (password !== confirmPassword) {{
                showMessage('Passwords do not match. Please try again.', false);
                return;
            }}

            // Validate password strength
            if (password.length < 6) {{
                showMessage('Password must be at least 6 characters long.', false);
                return;
            }}

            submitBtn.disabled = true;
            submitBtn.textContent = 'Resetting...';

            const payload = {{
                email: decodeURIComponent('{Uri.EscapeDataString(email)}'),
                token: decodeURIComponent('{Uri.EscapeDataString(token)}'),
                newPassword: password,
                confirmPassword: confirmPassword
            }};

            console.log('Payload:', payload);
            console.log('Calling API...');

            try {{
                const response = await fetch('/api/Auth/reset-password', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json'
                    }},
                    body: JSON.stringify(payload)
                }});

                console.log('Response status:', response.status);
                console.log('Response:', response);

                const data = await response.json();
                console.log('Response data:', data);

                if (response.ok && data.success) {{
                    showMessage('Password reset successful! You can now return to the app and log in with your new password.', true);
                    form.reset();
                    submitBtn.style.display = 'none';
                }} else {{
                    showMessage(data.message || 'Failed to reset password. The link may have expired.', false);
                    submitBtn.disabled = false;
                    submitBtn.textContent = 'Reset Password';
                }}
            }} catch (error) {{
                console.error('Error:', error);
                showMessage('An error occurred. Please try again later.', false);
                submitBtn.disabled = false;
                submitBtn.textContent = 'Reset Password';
            }}
        }});
    </script>
</body>
</html>";
    }

    private string GetErrorHtml(string message)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Error - Receipt Scanner</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.1);
            padding: 40px;
            max-width: 500px;
            width: 100%;
            text-align: center;
        }}
        .icon {{
            font-size: 64px;
            margin-bottom: 20px;
        }}
        h1 {{
            color: #333;
            margin-bottom: 20px;
            font-size: 24px;
        }}
        p {{
            color: #666;
            line-height: 1.6;
            margin-bottom: 30px;
        }}
        a {{
            display: inline-block;
            padding: 12px 30px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            transition: transform 0.2s;
        }}
        a:hover {{
            transform: translateY(-2px);
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>⚠️</div>
        <h1>Invalid Reset Link</h1>
        <p>{message}</p>
        <div style='margin-top:30px;padding:18px;background:#fff3cd;border-left:4px solid #ffc107;border-radius:6px;text-align:left'>
            <p style='margin:0;color:#856404;font-weight:600'>Please return to the app and request a new password reset link.</p>
        </div>
    </div>
</body>
</html>";
    }
}
