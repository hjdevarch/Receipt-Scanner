using ReceiptScanner.Application.DTOs;

namespace  ReceiptScanner.Application.Services
{
    public interface IMerchantService
    {
        Task<PagedResultDto<MerchantWithTotalDto>> GetMerchantsWithTotalsAsync(Guid userId, PaginationParameters pagination);
    }
}