using ReceiptScanner.Application.DTOs;

namespace ReceiptScanner.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto);
    Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
    Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto);
    Task<bool> RevokeTokenAsync(string userId);
    Task<UserDto?> GetCurrentUserAsync(string userId);
    Task<AuthResponseDto> ChangePasswordAsync(string userId, ChangePasswordDto changePasswordDto);
    Task<AuthResponseDto> ConfirmEmailAsync(string userId, string token);
    Task<bool> ResendActivationEmailAsync(string email);
}
