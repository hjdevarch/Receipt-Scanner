# ItemNames System Documentation

## Overview
The ItemNames system provides intelligent categorization of receipt items by maintaining a lookup table of unique item names and their associated categories. This enables automatic categorization of items based on previous categorization decisions and supports batch categorization through background jobs.

## Architecture

### Database Schema

#### ItemNames Table
```sql
CREATE TABLE ItemNames (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    CategoryId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_ItemNames_Categories_CategoryId 
        FOREIGN KEY (CategoryId) REFERENCES Categories (Id) ON DELETE SET NULL,
    INDEX IX_ItemNames_Name UNIQUE (Name)
);
```

#### ReceiptItems Table (Updated)
```sql
ALTER TABLE ReceiptItems
ADD ItemId INT NULL,
    CONSTRAINT FK_ReceiptItems_ItemNames_ItemId 
        FOREIGN KEY (ItemId) REFERENCES ItemNames (Id) ON DELETE SET NULL;
```

### Entity Relationships

```
ItemName (1) ←→ (*) ReceiptItem
ItemName (*) ←→ (1) Category

- ItemName.Id → ReceiptItem.ItemId (one-to-many)
- ItemName.CategoryId → Category.Id (many-to-one, nullable)
- Both foreign keys use ON DELETE SET NULL to prevent cascade issues
```

## Core Components

### 1. ItemName Entity
**Location:** `src/ReceiptScanner.Domain/Entities/ItemName.cs`

**Purpose:** Represents a unique item name in the system with optional category association.

**Key Properties:**
- `Id` (int): Auto-increment primary key
- `Name` (string): Unique item name (max 100 characters)
- `CategoryId` (Guid?): Optional foreign key to Categories table
- `Category` (Category): Navigation property

**Key Methods:**
- `SetCategory(Guid? categoryId)`: Updates the category association
- `UpdateName(string newName)`: Updates the item name

### 2. IItemNameRepository Interface
**Location:** `src/ReceiptScanner.Domain/Interfaces/IItemNameRepository.cs`

**Methods:**
- `GetByIdAsync(int id)`: Retrieve item by ID
- `GetByNameAsync(string name)`: Retrieve item by name (case-insensitive)
- `GetAllAsync()`: Get all item names with category details
- `GetUncategorizedAsync()`: Get items without categories
- `AddAsync(ItemName itemName)`: Add new item
- `UpdateAsync(ItemName itemName)`: Update existing item
- `DeleteAsync(int id)`: Delete item
- `ExistsAsync(string name)`: Check if item name exists

### 3. ReceiptItemService
**Location:** `src/ReceiptScanner.Application/Services/ReceiptItemService.cs`

**Purpose:** Manages receipt item creation with intelligent ItemName lookup and categorization.

**Key Method: `AddReceiptItemAsync`**

**Logic Flow:**
1. Check if `ItemNames.Name` already exists
2. If exists:
   - Retrieve `ItemNames.Id`
   - Retrieve `ItemNames.CategoryId`
   - Set `ReceiptItem.ItemId` = `ItemNames.Id`
   - Set `ReceiptItem.CategoryId` = `ItemNames.CategoryId`
3. If not exists:
   - Create new `ItemName` with `CategoryId` = NULL
   - Set `ReceiptItem.ItemId` = newly created `ItemNames.Id`
   - Set `ReceiptItem.CategoryId` = NULL

**Example:**
```csharp
var receiptItem = await _receiptItemService.AddReceiptItemAsync(
    name: "Milk",
    quantity: 2,
    unitPrice: 3.50m,
    receiptId: receiptId,
    userId: userId,
    quantityUnit: "bottles",
    totalPrice: 7.00m
);
// Result: If "Milk" exists with CategoryId=groceries-guid, 
// the receiptItem will have CategoryId=groceries-guid automatically
```

### 4. ItemCategorizationJobService
**Location:** `src/ReceiptScanner.Application/Services/ItemCategorizationJobService.cs`

**Purpose:** Background job service for automatic and manual categorization of items.

**Key Methods:**

#### `RunCategorizationJobAsync(string userId)`
Runs the full categorization job:
1. Gets all uncategorized items
2. Attempts to categorize each item using keyword matching (stub)
3. Updates ItemNames with determined categories
4. Updates all ReceiptItems associated with categorized ItemNames

**Usage (for scheduled jobs):**
```csharp
// To be called by Hangfire, Quartz, or similar
await _categorizationJobService.RunCategorizationJobAsync(userId);
```

#### `ManuallyCategorizItem(string itemName, Guid categoryId, string userId)`
Manually categorize a specific item:
1. Find ItemName by name
2. Update CategoryId
3. Update all ReceiptItems with matching ItemId

**Example:**
```csharp
// User manually categorizes "Milk" as "Groceries"
await _categorizationJobService.ManuallyCategorizItem(
    "Milk", 
    groceriesCategoryId, 
    userId
);
// All future and past receipts with "Milk" will now be categorized as Groceries
```

#### `BulkCategorizeItemsAsync(Dictionary<string, Guid> itemCategoryMappings, string userId)`
Batch categorize multiple items at once.

**Example:**
```csharp
var mappings = new Dictionary<string, Guid>
{
    { "Milk", groceriesId },
    { "Bread", groceriesId },
    { "Soap", personalCareId }
};
await _categorizationJobService.BulkCategorizeItemsAsync(mappings, userId);
```

## API Endpoints

### ItemNamesController
**Location:** `src/ReceiptScanner.API/Controllers/ItemNamesController.cs`

#### GET /api/itemnames
Get all item names with their categories.

**Response:**
```json
[
  {
    "id": 1,
    "name": "Milk",
    "categoryId": "guid-here",
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-16T14:20:00Z"
  }
]
```

#### GET /api/itemnames/uncategorized
Get items without categories (CategoryId is null).

**Response:**
```json
[
  {
    "id": 2,
    "name": "Unknown Product",
    "createdAt": "2024-01-15T10:30:00Z"
  }
]
```

#### GET /api/itemnames/{id}
Get a specific item name by ID with category details.

**Response:**
```json
{
  "id": 1,
  "name": "Milk",
  "categoryId": "guid-here",
  "categoryName": "Groceries",
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-01-16T14:20:00Z"
}
```

#### PUT /api/itemnames/categorize
Manually categorize an item.

**Request:**
```json
{
  "itemName": "Milk",
  "categoryId": "guid-here"
}
```

**Response:**
```json
{
  "message": "Item 'Milk' has been categorized successfully"
}
```

#### POST /api/itemnames/run-categorization-job
Manually trigger the background categorization job.

**Response:**
```json
{
  "message": "Categorization job completed successfully"
}
```

## Integration with Receipt Processing

### Receipt Creation Flow
1. User uploads receipt image
2. Azure Document Intelligence extracts items
3. For each item:
   - `ReceiptItemService.AddReceiptItemAsync()` is called
   - Service checks if item name exists in ItemNames
   - If exists, reuses Id and CategoryId
   - If not, creates new ItemName with null CategoryId
   - Creates ReceiptItem with appropriate foreign key values
4. Receipt is saved with all items

### Receipt Update Flow
1. User updates receipt items
2. Old items are deleted
3. For each new item:
   - `ReceiptItemService.AddReceiptItemAsync()` is called
   - Same lookup logic as creation flow
4. Updated receipt is saved

## Background Job Setup (Future Implementation)

### Option 1: Hangfire
```csharp
// Startup configuration
services.AddHangfire(config => 
    config.UseSqlServerStorage(connectionString));

// Schedule daily categorization job
RecurringJob.AddOrUpdate<ItemCategorizationJobService>(
    "categorize-items",
    service => service.RunCategorizationJobAsync(userId),
    Cron.Daily);
```

### Option 2: Quartz.NET
```csharp
// Job definition
public class CategorizationJob : IJob
{
    private readonly ItemCategorizationJobService _service;
    
    public async Task Execute(IJobExecutionContext context)
    {
        var userId = context.MergedJobDataMap.GetString("userId");
        await _service.RunCategorizationJobAsync(userId);
    }
}

// Schedule
var trigger = TriggerBuilder.Create()
    .WithDailyTimeIntervalSchedule(s => 
        s.OnEveryDay().StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(2, 0)))
    .Build();
```

## Testing Workflow

### Test File Location
`TestItemNamesAndCategorization.http`

### Workflow Steps:

1. **Create a receipt with items**
   ```http
   POST https://localhost:7216/api/receipts
   Authorization: Bearer {token}
   
   {
     "merchantName": "Grocery Store",
     "items": [
       { "itemName": "Milk", "quantity": 2, "pricePerUnit": 3.50 }
     ]
   }
   ```

2. **Check uncategorized items**
   ```http
   GET https://localhost:7216/api/itemnames/uncategorized
   ```

3. **Get category ID**
   ```http
   GET https://localhost:7216/api/categories
   ```

4. **Manually categorize an item**
   ```http
   PUT https://localhost:7216/api/itemnames/categorize
   
   {
     "itemName": "Milk",
     "categoryId": "{groceries-category-id}"
   }
   ```

5. **Create another receipt with same item**
   - Item should automatically be categorized

6. **Run auto-categorization job**
   ```http
   POST https://localhost:7216/api/itemnames/run-categorization-job
   ```

7. **Verify all items are categorized**
   ```http
   GET https://localhost:7216/api/itemnames
   ```

## Benefits

### 1. Automatic Categorization
Once an item is categorized, all future receipts with the same item name are automatically categorized.

### 2. Retroactive Categorization
When an ItemName is categorized, the service can update all past ReceiptItems with that ItemName.

### 3. Duplicate Prevention
The unique index on `ItemNames.Name` ensures no duplicate item names are created.

### 4. Flexible Categorization
- Manual categorization via API
- Bulk categorization for efficiency
- Background job for automatic categorization using keyword matching or ML

### 5. Data Integrity
- Foreign keys use SET NULL to prevent cascade delete issues
- ItemName can exist without a category (uncategorized state)
- ReceiptItem can exist without an ItemName (legacy support)

## Migration History

**Migration:** `20251107080043_AddItemNamesTableAndItemIdToReceiptItems`

**Changes:**
- Created ItemNames table with Id, Name, CategoryId
- Added ItemId column to ReceiptItems
- Created unique index on ItemNames.Name
- Added foreign keys with SET NULL behavior
- Set up navigation properties in EF Core

## Future Enhancements

1. **AI-Powered Categorization**
   - Integrate with GPT service for smarter categorization
   - Use historical purchase patterns
   - Implement confidence scores

2. **Item Suggestions**
   - Suggest categories for uncategorized items
   - Learn from user corrections
   - Provide similar items recommendations

3. **Analytics**
   - Most frequently purchased items
   - Category spending trends
   - Seasonal purchase patterns

4. **Multi-Language Support**
   - Normalize item names across languages
   - Support regional variations (e.g., "Milk" vs "Leche")

5. **Item Variants**
   - Handle brand variations (e.g., "Coca Cola" vs "Coke")
   - Manage size variations (e.g., "Milk 1L" vs "Milk 2L")
   - Merge similar items

## Troubleshooting

### Issue: Duplicate item names with different casing
**Solution:** The repository uses case-insensitive comparison (`StringComparison.OrdinalIgnoreCase`), and the database uses a case-insensitive collation.

### Issue: Items not getting categorized automatically
**Solution:** Check if ItemName exists and has a CategoryId set. Run the categorization job manually to update uncategorized items.

### Issue: Category not updating for existing receipts
**Solution:** Use `UpdateReceiptItemsCategoryAsync()` to update all ReceiptItems when an ItemName's category changes.

### Issue: Foreign key violations
**Solution:** All foreign keys use SET NULL, so deleting a Category or ItemName will set the references to NULL rather than failing.
