using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Domain.Interfaces;

namespace ReceiptScanner.API.Controllers
{
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

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            try
            {
                var settings = await _settingsRepository.GetDefaultSettingsAsync();
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
                var currencyName = await _settingsRepository.GetDefaultCurrencyNameAsync();
                var currencySymbol = await _settingsRepository.GetDefaultCurrencySymbolAsync();

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
    }
}