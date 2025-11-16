using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Application.Services;
using ReceiptScanner.Domain.Interfaces;
using System.Security.Claims;

namespace ReceiptScanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MerchantsController : ControllerBase
{
    private readonly IMerchantRepository _merchantRepository;
    private readonly IMerchantService _merchantService;
    private readonly ILogger<MerchantsController> _logger;

    public MerchantsController(IMerchantRepository merchantRepository
            , IMerchantService merchantService
            , ILogger<MerchantsController> logger)
    {
        _merchantRepository = merchantRepository;
        _merchantService = merchantService;
        _logger = logger;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>
    /// Get all merchants
    /// </summary>
    /// <returns>List of all merchants</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MerchantDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MerchantDto>>> GetAllMerchants()
    {
        var userId = GetUserId();
        var merchants = await _merchantRepository.GetAllByUserIdAsync(userId);
        var merchantDtos = merchants.Select(m => new MerchantDto
        {
            Id = m.Id,
            Name = m.Name,
            Address = m.Address,
            PhoneNumber = m.PhoneNumber,
            Email = m.Email,
            Website = m.Website,
            LogoPath = m.LogoPath
        });

        return Ok(merchantDtos);
    }

    /// <summary>
    /// Get all merchants with their total receipt amounts
    /// </summary>
    /// <returns>List of merchants with total amounts</returns>
    [HttpGet("with-totals/paged")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMerchantsWithTotals([FromQuery] int pageSize = 10, [FromQuery] int pageNumber = 1)
    {
        var userId = GetUserId();
        var pagination = new PaginationParameters
        {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var merchantsWithTotals = await _merchantService.GetMerchantsWithTotalsAsync(Guid.Parse(userId), pagination).ConfigureAwait(false);

        return Ok(merchantsWithTotals);
    }

    /// <summary>
    /// Get a specific merchant by ID
    /// </summary>
    /// <param name="id">Merchant ID</param>
    /// <returns>Merchant details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MerchantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MerchantDto>> GetMerchant(Guid id)
    {
        var merchant = await _merchantRepository.GetByIdAsync(id);
        
        if (merchant == null)
        {
            return NotFound($"Merchant with ID {id} not found");
        }

        var merchantDto = new MerchantDto
        {
            Id = merchant.Id,
            Name = merchant.Name,
            Address = merchant.Address,
            PhoneNumber = merchant.PhoneNumber,
            Email = merchant.Email,
            Website = merchant.Website,
            LogoPath = merchant.LogoPath
        };

        return Ok(merchantDto);
    }

    /// <summary>
    /// Search merchants by name
    /// </summary>
    /// <param name="name">Merchant name to search for</param>
    /// <returns>List of matching merchants</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<MerchantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<MerchantDto>>> SearchMerchants([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Search name cannot be empty");
        }

        var userId = GetUserId();
        var merchant = await _merchantRepository.GetByNameAsync(name, userId);
        var merchants = merchant != null ? new[] { merchant } : Array.Empty<Domain.Entities.Merchant>();
        
        var merchantDtos = merchants.Select(m => new MerchantDto
        {
            Id = m.Id,
            Name = m.Name,
            Address = m.Address,
            PhoneNumber = m.PhoneNumber,
            Email = m.Email,
            Website = m.Website,
            LogoPath = m.LogoPath
        });

        return Ok(merchantDtos);
    }
}