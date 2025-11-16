using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Domain.Interfaces;

namespace ReceiptScanner.Application.Services
{
    public class MerchantService : IMerchantService
    {
        private readonly IMerchantRepository _merchantRepository;

        public MerchantService(IMerchantRepository merchantRepository)
        {
            _merchantRepository = merchantRepository;
        }

        public async Task<PagedResultDto<MerchantWithTotalDto>> GetMerchantsWithTotalsAsync(Guid userId, PaginationParameters pagination)
        {
            int skip = (pagination.PageNumber - 1) * pagination.PageSize;
            var result = await _merchantRepository.GetAllWithReceiptTotalsPagedAsync(userId.ToString(), skip, pagination.PageSize).ConfigureAwait(false);
            return new PagedResultDto<MerchantWithTotalDto>
            {
                Items = result.Merchants.Select(m => new MerchantWithTotalDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    TotalAmount = m.Receipts.Sum(r => r.TotalAmount),
                    Address = m.Address,
                    PhoneNumber = m.PhoneNumber,
                    Email = m.Email,
                    Website = m.Website,
                    LogoPath = m.LogoPath,
                    ReceiptCount = m.Receipts.Count
                }),
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize,
                TotalCount = result.TotalCount,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (double)pagination.PageSize),
                HasPreviousPage = pagination.PageNumber > 1,
                HasNextPage = (pagination.PageNumber * pagination.PageSize) < result.TotalCount
            };
        }
    }
    
}