using System.ComponentModel.DataAnnotations;

namespace ReceiptScanner.Application.DTOs;

public class SetDefaultCurrencyRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string CurrencyName { get; set; } = string.Empty;

    [Required]
    [StringLength(10, MinimumLength = 1)]
    public string CurrencySymbol { get; set; } = string.Empty;
}