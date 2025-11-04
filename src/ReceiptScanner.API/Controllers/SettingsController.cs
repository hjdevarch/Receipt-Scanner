using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Domain.Interfaces;
using System.Security.Claims;

namespace ReceiptScanner.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(ISettingsRepository settingsRepository, ILogger<SettingsController> logger)
        {
            _settingsRepository = settingsRepository;
            _logger = logger;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? throw new UnauthorizedAccessException("User ID not found in token");
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var userId = GetUserId();
                var settings = await _settingsRepository.GetByUserIdAsync(userId);
                if (settings == null)
                {
                    return NotFound("No settings found");
                }

                return Ok(new
                {
                    Id = settings.Id,
                    DefaultCurrencyName = settings.DefaultCurrencyName,
                    DefaultCurrencySymbol = settings.DefaultCurrencySymbol,
                    CreatedAt = settings.CreatedAt,
                    UpdatedAt = settings.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving settings");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("default-currency")]
        public async Task<IActionResult> GetDefaultCurrency()
        {
            try
            {
                var userId = GetUserId();
                var currencyName = await _settingsRepository.GetDefaultCurrencyNameAsync(userId);
                var currencySymbol = await _settingsRepository.GetDefaultCurrencySymbolAsync(userId);

                return Ok(new
                {
                    CurrencyName = currencyName,
                    CurrencySymbol = currencySymbol
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default currency");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut]
        public async Task<IActionResult> SetDefaultCurrency([FromBody] SetDefaultCurrencyRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CurrencyName) || string.IsNullOrWhiteSpace(request.CurrencySymbol))
            {
                return BadRequest("Invalid request data");
            }

            try
            {
                var userId = GetUserId();
                await _settingsRepository.SetDefaultCurrencyAsync(userId, request.CurrencyName, request.CurrencySymbol);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting default currency");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}