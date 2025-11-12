using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Application.Interfaces;
using ReceiptScanner.Application.Settings;
using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailService _emailService;
    private readonly ApplicationSettings _appSettings;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IOptions<JwtSettings> jwtSettings,
        IEmailService emailService,
        IOptions<ApplicationSettings> appSettings)
    {
        _userManager = userManager;
        _jwtSettings = jwtSettings.Value;
        _emailService = emailService;
        _appSettings = appSettings.Value;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "User with this email already exists.",
                    Errors = new List<string> { "Email already registered" }
                };
            }

            // Create new user
            var user = new ApplicationUser
            {
                Email = registerDto.Email,
                UserName = registerDto.UserName ?? registerDto.Email,
                EmailConfirmed = false // Require email confirmation
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (!result.Succeeded)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Failed to create user.",
                    Errors = result.Errors.Select(e => e.Description).ToList()
                };
            }

            // Generate email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var activationLink = $"{_appSettings.BaseUrl}/api/Auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

            // Send activation email
            await _emailService.SendActivationEmailAsync(user.Email!, user.UserName ?? "User", activationLink);

            return new AuthResponseDto
            {
                Success = true,
                Message = "Registration successful. Please check your email to activate your account.",
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    UserName = user.UserName,
                    CreatedAt = user.CreatedAt
                }
            };
        }
        catch (Exception ex)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred during registration.",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);
            if (user == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid email or password.",
                    Errors = new List<string> { "Invalid credentials" }
                };
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
            if (!isPasswordValid)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid email or password.",
                    Errors = new List<string> { "Invalid credentials" }
                };
            }

            // Check if email is confirmed
            if (!user.EmailConfirmed)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Please activate your account. An activation email has been sent to your email address.",
                    Errors = new List<string> { "Email not confirmed" }
                };
            }

            // Generate tokens
            var tokens = await GenerateTokensAsync(user);

            return new AuthResponseDto
            {
                Success = true,
                Message = "Login successful.",
                Token = tokens,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    UserName = user.UserName,
                    CreatedAt = user.CreatedAt
                }
            };
        }
        catch (Exception ex)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred during login.",
                Errors = new List<string> { ex.Message }
            };
        }
    } 

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto)
    {
        try
        {
            var principal = GetPrincipalFromExpiredToken(refreshTokenDto.AccessToken);
            if (principal == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid access token.",
                    Errors = new List<string> { "Invalid token" }
                };
            }

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid token claims.",
                    Errors = new List<string> { "User ID not found in token" }
                };
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.RefreshToken != refreshTokenDto.RefreshToken ||
                user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid or expired refresh token.",
                    Errors = new List<string> { "Refresh token invalid" }
                };
            }

            // Generate new tokens
            var tokens = await GenerateTokensAsync(user);

            return new AuthResponseDto
            {
                Success = true,
                Message = "Token refreshed successfully.",
                Token = tokens,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    UserName = user.UserName,
                    CreatedAt = user.CreatedAt
                }
            };
        }
        catch (Exception ex)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred during token refresh.",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<bool> RevokeTokenAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return null;

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                UserName = user.UserName,
                CreatedAt = user.CreatedAt
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResponseDto> ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "User not found.",
                    Errors = new List<string> { "User not found" }
                };
            }

            // Verify current password
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, changePasswordDto.CurrentPassword);
            if (!isPasswordValid)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Current password is incorrect.",
                    Errors = new List<string> { "Invalid current password" }
                };
            }

            // Change password
            var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
            if (!result.Succeeded)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Failed to change password.",
                    Errors = result.Errors.Select(e => e.Description).ToList()
                };
            }

            // Update timestamp
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return new AuthResponseDto
            {
                Success = true,
                Message = "Password changed successfully.",
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    UserName = user.UserName,
                    CreatedAt = user.CreatedAt
                }
            };
        }
        catch (Exception ex)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred while changing password.",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<AuthResponseDto> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
            if (user == null)
            {
                // For security reasons, don't reveal if the email exists or not
                return new AuthResponseDto
                {
                    Success = true,
                    Message = "If an account with that email exists, a password reset link has been sent.",
                    Errors = new List<string>()
                };
            }

            // Generate password reset token
            var resetToken = GeneratePasswordResetToken();
            var resetTokenExpiry = DateTime.UtcNow.AddHours(1); // Token valid for 1 hour

            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiryTime = resetTokenExpiry;
            user.UpdatedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);

            // TODO: Send email with reset token
            // For now, we'll return the token in the response (in production, this should be sent via email)
            return new AuthResponseDto
            {
                Success = true,
                Message = "Password reset token generated successfully. In production, this would be sent via email.",
                Errors = new List<string> { $"Reset Token: {resetToken}" } // Remove this in production
            };
        }
        catch (Exception ex)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred while processing the forgot password request.",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
            if (user == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid reset request.",
                    Errors = new List<string> { "User not found" }
                };
            }

            // Validate reset token
            if (string.IsNullOrEmpty(user.PasswordResetToken) ||
                user.PasswordResetToken != resetPasswordDto.Token ||
                user.PasswordResetTokenExpiryTime == null ||
                user.PasswordResetTokenExpiryTime <= DateTime.UtcNow)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Invalid or expired reset token.",
                    Errors = new List<string> { "Reset token is invalid or has expired" }
                };
            }

            // Reset password using UserManager
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, resetPasswordDto.NewPassword);

            if (!result.Succeeded)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Failed to reset password.",
                    Errors = result.Errors.Select(e => e.Description).ToList()
                };
            }

            // Clear the password reset token
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiryTime = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);

            return new AuthResponseDto
            {
                Success = true,
                Message = "Password reset successfully.",
                Errors = new List<string>()
            };
        }
        catch (Exception ex)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred while resetting the password.",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private async Task<TokenDto> GenerateTokensAsync(ApplicationUser user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        var accessTokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes);
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays);

        // Save refresh token to user
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = refreshTokenExpiration;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiration = accessTokenExpiration,
            RefreshTokenExpiration = refreshTokenExpiration
        };
    }

    private string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.UserName ?? user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static string GeneratePasswordResetToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
            ValidateLifetime = false // We don't validate lifetime here
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResponseDto> ConfirmEmailAsync(string userId, string token)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "User not found.",
                    Errors = new List<string> { "Invalid user" }
                };
            }

            if (user.EmailConfirmed)
            {
                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Email is already confirmed.",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Email = user.Email!,
                        UserName = user.UserName,
                        CreatedAt = user.CreatedAt
                    }
                };
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                return new AuthResponseDto
                {
                    Success = false,
                    Message = "Email confirmation failed. The link may have expired.",
                    Errors = result.Errors.Select(e => e.Description).ToList()
                };
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return new AuthResponseDto
            {
                Success = true,
                Message = "Email confirmed successfully. You can now log in.",
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    UserName = user.UserName,
                    CreatedAt = user.CreatedAt
                }
            };
        }
        catch (Exception ex)
        {
            return new AuthResponseDto
            {
                Success = false,
                Message = "An error occurred during email confirmation.",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<bool> ResendActivationEmailAsync(string email)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return false;

            if (user.EmailConfirmed)
                return true; // Already confirmed

            // Generate new email confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var activationLink = $"{_appSettings.BaseUrl}/api/Auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

            // Send activation email
            return await _emailService.SendActivationEmailAsync(user.Email!, user.UserName ?? "User", activationLink);
        }
        catch
        {
            return false;
        }
    }
}
