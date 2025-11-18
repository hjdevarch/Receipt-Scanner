using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ReceiptScanner.Application.DTOs;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    [DefaultValue("hamid.jolany@gmail.com")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [DefaultValue("Iran@1983")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare("Password")]
    [DefaultValue("Iran@1983")]
    public string ConfirmPassword { get; set; } = string.Empty;
    
    public string? UserName { get; set; }
}

public class LoginDto
{
    [Required]
    [DefaultValue("hamid.jolany@gmail.com")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DefaultValue("Iran@1983")]
    public string Password { get; set; } = string.Empty;
} 

public class TokenDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiration { get; set; }
    public DateTime RefreshTokenExpiration { get; set; }
}

public class RefreshTokenDto
{
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ResendActivationDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TokenDto? Token { get; set; }
    public UserDto? User { get; set; }
    public List<string> Errors { get; set; } = new();
}
