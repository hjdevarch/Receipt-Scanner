using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces;

public interface IItemNameRepository
{
    Task<ItemName?> GetByIdAsync(int id);
    Task<ItemName?> GetByNameAsync(string name);
    Task<IEnumerable<ItemName>> GetAllAsync();
    Task<IEnumerable<ItemName>> GetUncategorizedAsync();
    Task<ItemName> AddAsync(ItemName itemName);
    Task<ItemName> UpdateAsync(ItemName itemName);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string name);
}
