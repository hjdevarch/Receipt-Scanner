using ReceiptScanner.Domain.Entities;

namespace ReceiptScanner.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<IEnumerable<Category>> GetAllByUserIdAsync(string userId);
    Task<Category?> GetByIdAsync(Guid id, string userId);
    Task<Category?> GetByNameAsync(string name, string userId);
    Task<Category> AddAsync(Category category);
    Task UpdateAsync(Category category);
    Task DeleteAsync(Guid id, string userId);
    Task<bool> ExistsAsync(Guid id, string userId);
}
