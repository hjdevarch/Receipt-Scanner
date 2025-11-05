using System.ComponentModel.DataAnnotations;
using ReceiptScanner.Domain.Entities;

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

public class UpdateThresholdRequest
{
    [Required]
    public ThresholdType ThresholdType { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Threshold rate must be greater than 0")]
    public decimal ThresholdRate { get; set; }
}