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
    public async Task<ActionResult<AuthResponseDto>> ConfirmEmail([FromQuery] string userId, [FromQuery] string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Email confirmation failed: Missing userId or token");
            return BadRequest(new AuthResponseDto
            {
                Success = false,
                Message = "Invalid confirmation link.",
                Errors = new List<string> { "Missing userId or token" }
            });
        }

        _logger.LogInformation("Email confirmation attempt for user: {UserId}", userId);

        var result = await _authService.ConfirmEmailAsync(userId, token);

        if (!result.Success)
        {
            _logger.LogWarning("Email confirmation failed for user: {UserId}. Errors: {Errors}",
                userId, string.Join(", ", result.Errors));
            return BadRequest(result);
        }

        _logger.LogInformation("Email confirmed successfully for user: {UserId}", userId);
        return Ok(result);
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
}
