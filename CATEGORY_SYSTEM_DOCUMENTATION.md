# Category System Documentation

## Overview

The Category system provides intelligent, AI-powered categorization of receipt items using GPT/Ollama. It allows automatic classification of items into logical categories (e.g., Groceries, Household, Personal Care, Electronics) to help organize and analyze spending patterns.

## Architecture

### Database Schema

**Categories Table:**
- `Id` (GUID) - Primary key
- `Name` (nvarchar(100)) - Category name
- `UserId` (string) - User who owns this category (multi-tenant isolation)
- `CreatedAt` (datetime) - Auto-generated timestamp
- `UpdatedAt` (datetime) - Auto-updated timestamp

**ReceiptItems Table (Updated):**
- `CategoryId` (GUID, nullable) - Foreign key to Categories table
- Navigation property: `CategoryEntity` (Category)

**Relationships:**
- Category → ReceiptItems (one-to-many)
- Category → ApplicationUser (many-to-one)
- On delete: SetNull (deleting a category sets CategoryId to null on items)

**Indexes:**
- `UserId` - For fast user-scoped queries
- `(UserId, Name)` - Composite index for category name lookups per user
- `CategoryId` on ReceiptItems - For join performance

### Domain Layer

**Entity: `Category.cs`**
```csharp
public class Category : BaseEntity
{
    public string Name { get; private set; }
    public string UserId { get; private set; }
    public virtual ApplicationUser User { get; private set; }
    public virtual ICollection<ReceiptItem> ReceiptItems { get; private set; }
    
    public Category(string name, string userId)
    public void UpdateName(string name)
}
```

**Entity: `ReceiptItem.cs` (Updated)**
```csharp
public class ReceiptItem : BaseEntity
{
    // ... existing properties ...
    public Guid? CategoryId { get; private set; }
    public virtual Category? CategoryEntity { get; private set; }
    
    public void SetCategory(Guid? categoryId)
}
```

**Repository Interface: `ICategoryRepository.cs`**
```csharp
Task<IEnumerable<Category>> GetAllByUserIdAsync(string userId);
Task<Category?> GetByIdAsync(Guid id, string userId);
Task<Category?> GetByNameAsync(string name, string userId);
Task<Category> AddAsync(Category category);
Task UpdateAsync(Category category);
Task DeleteAsync(Guid id, string userId);
Task<bool> ExistsAsync(Guid id, string userId);
```

### Infrastructure Layer

**Repository: `CategoryRepository.cs`**
- Implements all CRUD operations with multi-tenant filtering
- Uses EF Core for data access
- All queries scoped by UserId

### API Layer

**Controller: `CategoryController.cs`**

All endpoints require JWT authentication (`[Authorize]`).

#### Endpoints:

**1. Get All Categories**
```http
GET /api/category
Authorization: Bearer {token}
```
Returns all categories for the authenticated user.

**2. Get Category By ID**
```http
GET /api/category/{id}
Authorization: Bearer {token}
```
Returns a specific category.

**3. Create Category**
```http
POST /api/category
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Office Supplies"
}
```
Creates a new category manually.

**4. Update Category**
```http
PUT /api/category/{id}
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Updated Name"
}
```
Updates an existing category.

**5. Delete Category**
```http
DELETE /api/category/{id}
Authorization: Bearer {token}
```
Deletes a category (sets CategoryId to null on associated items).

**6. Auto-Categorize (AI-Powered)**
```http
POST /api/category/auto-categorize
Authorization: Bearer {token}
```

This is the main feature that uses GPT to automatically categorize all receipt items.

**Algorithm:**
1. Extract userId from JWT token
2. Load all receipts for the user
3. Collect all unique item names from receipt items
4. Join item names with comma: `"Apple, Bread, Milk, Soap, ..."`
5. Create GPT prompt:
   ```
   Categorize these receipt items into logical categories.
   Return ONLY a valid JSON array with format:
   [{"item": "item name", "category": "category name"}, ...]
   
   Items: Apple, Bread, Milk, Soap, ...
   ```
6. Send prompt to GPT via `IGPTHelperService.SendPromptAsync()`
7. Parse JSON response to extract item-category mappings
8. Create unique categories in database (if they don't exist)
9. Update each ReceiptItem with appropriate CategoryId
10. Save all changes

**Response:**
```json
{
  "itemsProcessed": 45,
  "categoriesCreated": 8,
  "itemsUpdated": 45,
  "message": "Auto-categorization completed successfully"
}
```

**Error Responses:**
- `401 Unauthorized` - Invalid/missing JWT token
- `503 Service Unavailable` - Ollama/GPT service not running
- `408 Request Timeout` - GPT request took too long (>5 minutes)
- `400 Bad Request` - Failed to parse GPT response
- `500 Internal Server Error` - Unexpected error

## GPT Integration

### Prompt Format

The system sends a structured prompt to GPT:

```
Categorize these receipt items into logical categories (e.g., Groceries, Household, Personal Care, Electronics, Clothing, Entertainment, etc.). 

Return ONLY a valid JSON array with this exact format (no additional text or explanation):
[{"item": "item name", "category": "category name"}, ...]

Items to categorize: Apple, Bread, Milk, Dish Soap, Coffee, Orange Juice, Paper Towels, Shampoo, Notebook, Pen
```

### Expected GPT Response

```json
[
  {"item": "Apple", "category": "Groceries"},
  {"item": "Bread", "category": "Groceries"},
  {"item": "Milk", "category": "Groceries"},
  {"item": "Dish Soap", "category": "Household"},
  {"item": "Coffee", "category": "Groceries"},
  {"item": "Orange Juice", "category": "Groceries"},
  {"item": "Paper Towels", "category": "Household"},
  {"item": "Shampoo", "category": "Personal Care"},
  {"item": "Notebook", "category": "Office Supplies"},
  {"item": "Pen", "category": "Office Supplies"}
]
```

### Response Parsing

The controller uses `ParseGptResponse()` which:
1. Extracts JSON array from response (handles extra text)
2. Deserializes to `List<ItemCategorization>`
3. Case-insensitive matching for robustness
4. Logs errors if parsing fails

## Usage Workflow

### Initial Setup

1. Ensure Ollama is running:
   ```bash
   ollama serve
   ```

2. Test GPT service availability:
   ```http
   GET /api/gpt/status
   Authorization: Bearer {token}
   ```

### Auto-Categorization Workflow

1. **Authenticate** - Get JWT token:
   ```http
   POST /api/auth/login
   Content-Type: application/json
   
   {
     "email": "user@example.com",
     "password": "Password123"
   }
   ```

2. **Add some receipts** (if not already present):
   ```http
   POST /api/receipts
   Authorization: Bearer {token}
   ```

3. **Run auto-categorization**:
   ```http
   POST /api/category/auto-categorize
   Authorization: Bearer {token}
   ```

4. **View categories**:
   ```http
   GET /api/category
   Authorization: Bearer {token}
   ```

5. **View receipts with categorized items**:
   ```http
   GET /api/receipts
   Authorization: Bearer {token}
   ```

### Manual Category Management

You can also create, update, and delete categories manually:

```http
# Create
POST /api/category
{
  "name": "Custom Category"
}

# Update
PUT /api/category/{id}
{
  "name": "Updated Category"
}

# Delete
DELETE /api/category/{id}
```

## Multi-Tenant Isolation

All category operations are scoped to the authenticated user:
- Categories are filtered by `UserId`
- Each user has their own set of categories
- Users cannot access other users' categories
- Auto-categorization only processes the current user's receipts

## Performance Considerations

### Indexes

- **UserId index** - Fast filtering by user
- **(UserId, Name) composite index** - Fast category name lookups per user
- **CategoryId index on ReceiptItems** - Efficient joins

### GPT Request Optimization

- **Batch processing** - All items sent in one GPT request (not per-item)
- **Unique items only** - Duplicates filtered before sending to GPT
- **Timeout** - 5-minute maximum to prevent hanging
- **Connection pooling** - HttpClient factory pattern for efficiency

### Database Optimization

- **Bulk updates** - All receipt items updated in batch
- **Lazy loading** - Navigation properties loaded only when needed
- **Nullable FK** - CategoryId is nullable to support gradual rollout

## Error Handling

### Common Scenarios

1. **Ollama not running**
   - Error: `503 Service Unavailable`
   - Solution: Start Ollama with `ollama serve`

2. **Invalid GPT response**
   - Error: `400 Bad Request`
   - Logged: GPT response for debugging
   - Solution: Check Ollama model, try again

3. **Timeout**
   - Error: `408 Request Timeout`
   - Cause: Too many items or slow GPT response
   - Solution: Reduce number of items or increase timeout

4. **No receipts found**
   - Response: `itemsProcessed: 0`
   - Message: "No receipts found for categorization"

### Logging

The controller logs:
- Info: Start/completion of auto-categorization
- Info: Item counts, categories created
- Warning: Failed to parse GPT response
- Error: Exceptions with stack traces

Log locations:
- Application logs: `{ProjectRoot}/Logs/`
- GPT responses: Logged via `ILogger<CategoryController>`

## Testing

### Test Files

**TestCategoryAutoCategorization.http**
```http
### Get all categories
GET https://localhost:7215/api/category
Authorization: Bearer {token}

### Auto-categorize
POST https://localhost:7215/api/category/auto-categorize
Authorization: Bearer {token}
```

### Manual Testing

1. Start the API:
   ```bash
   dotnet run --project src/ReceiptScanner.API
   ```

2. Start Ollama:
   ```bash
   ollama serve
   ```

3. Execute test requests in VS Code (REST Client extension)

### Expected Results

After auto-categorization:
- New categories created (e.g., "Groceries", "Household")
- All receipt items have `CategoryId` set
- Response shows counts: items processed, categories created, items updated

## Migration

### Database Migration

Migration file: `20251102230109_AddCategoryTableAndCategoryIdToReceiptItems.cs`

**Up:**
- Creates `Categories` table
- Adds `CategoryId` column to `ReceiptItems`
- Creates foreign key relationship
- Creates indexes

**Down:**
- Drops foreign key
- Removes `CategoryId` column
- Drops `Categories` table

### Applying Migration

```bash
cd src/ReceiptScanner.Infrastructure
dotnet ef database update --startup-project ../ReceiptScanner.API
```

## Configuration

### appsettings.json

```json
{
  "Ollama": {
    "BaseUrl": "http://127.0.0.1:11434"
  }
}
```

### Service Registration

In `ServiceCollectionExtensions.cs`:

```csharp
// Repository
services.AddScoped<ICategoryRepository, CategoryRepository>();

// GPT service (already registered)
services.AddHttpClient<IGPTHelperService, GPTHelperService>();
```

## Future Enhancements

1. **Category Statistics**
   - Endpoint to show spending by category
   - Top categories by total amount
   - Category trends over time

2. **Custom Categories**
   - Allow users to define custom category mappings
   - Override GPT suggestions for specific items

3. **Batch Processing**
   - Process receipts in smaller batches for large datasets
   - Background job for auto-categorization

4. **Category Merging**
   - Merge similar categories (e.g., "Grocery" and "Groceries")
   - Suggest duplicates to user

5. **Export/Import**
   - Export categories to CSV/JSON
   - Import category mappings from file

## Troubleshooting

### Problem: Auto-categorization returns 0 items

**Possible causes:**
- No receipts in database
- Receipts have no items
- All item names are empty/null

**Solution:**
- Check receipts endpoint: `GET /api/receipts`
- Verify items have `Name` field populated

### Problem: GPT response not parsed

**Possible causes:**
- GPT returned invalid JSON
- Extra text in response

**Solution:**
- Check logs for GPT response
- Verify Ollama model is working: `ollama run llama3`
- Response parser extracts JSON array from text

### Problem: Categories not appearing in receipts

**Possible causes:**
- CategoryEntity navigation not loaded
- Include statement missing in repository

**Solution:**
- Add `.Include(r => r.Items).ThenInclude(i => i.CategoryEntity)` to receipt queries

## Security

- **Authentication required** - All endpoints use `[Authorize]`
- **User isolation** - Categories filtered by UserId from JWT
- **Input validation** - Category names validated (non-null, max 100 chars)
- **SQL injection protection** - EF Core parameterizes queries
- **XSS protection** - JSON responses auto-escaped

## API Reference Summary

| Endpoint | Method | Description | Auth Required |
|----------|--------|-------------|---------------|
| `/api/category` | GET | Get all categories | Yes |
| `/api/category/{id}` | GET | Get category by ID | Yes |
| `/api/category` | POST | Create category | Yes |
| `/api/category/{id}` | PUT | Update category | Yes |
| `/api/category/{id}` | DELETE | Delete category | Yes |
| `/api/category/auto-categorize` | POST | AI auto-categorization | Yes |

## Dependencies

- **EF Core** - Database access
- **ASP.NET Core Identity** - User management and JWT
- **GPTHelperService** - Ollama/LLM integration
- **System.Text.Json** - JSON parsing
- **Microsoft.Extensions.Logging** - Logging
