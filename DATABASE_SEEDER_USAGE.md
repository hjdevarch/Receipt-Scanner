# Database Seeder for Testing

## Overview
A test data generator that creates realistic dummy receipts and items using the **Bogus** library.

## API Endpoints

### 1. Seed Dummy Data
**POST** `/api/DataSeeder/seed`

Creates fake receipts with items for testing purposes.

**Parameters:**
- `userId` (required): User ID to assign the data to
- `receiptsCount` (optional, default: 100): Number of receipts to create
- `maxReceiptItemsCount` (optional, default: 20): Maximum items per receipt

**Example Request:**
```http
POST http://192.168.0.68:5091/api/DataSeeder/seed?userId=d3b95bf5-4c0e-46d0-9753-1acf3df0f44d&receiptsCount=1000&maxReceiptItemsCount=15
Authorization: Bearer YOUR_JWT_TOKEN
```

**Example Response:**
```json
{
  "message": "Dummy data seeded successfully",
  "userId": "d3b95bf5-4c0e-46d0-9753-1acf3df0f44d",
  "receiptsCreated": 1000,
  "maxItemsPerReceipt": 15
}
```

---

### 2. Clear User Data
**DELETE** `/api/DataSeeder/clear`

Removes all receipts, items, and merchants for a specific user.

**Parameters:**
- `userId` (required): User ID to clear data for

**Example Request:**
```http
DELETE http://192.168.0.68:5091/api/DataSeeder/clear?userId=d3b95bf5-4c0e-46d0-9753-1acf3df0f44d
Authorization: Bearer YOUR_JWT_TOKEN
```

**Example Response:**
```json
{
  "message": "User data cleared successfully",
  "userId": "d3b95bf5-4c0e-46d0-9753-1acf3df0f44d"
}
```

---

## Generated Data

### Receipts
- **Receipt Number**: Random alphanumeric (e.g., "A3F5G8H2K1")
- **Receipt Date**: Random date within last 2 years
- **Currency**: Random (USD, EUR, GBP, CAD, AUD)
- **Amounts**: Realistic sub-total, tax (10%), and totals
- **Reward**: 30% chance of having reward points (1-50)
- **Status**: All set to "Processed"

### Merchants
- **Name**: Realistic company names (e.g., "Acme Corp", "Tech Solutions Inc")
- **Address**: Full addresses with street, city, state, zip
- **Phone**: Formatted phone numbers
- **Email**: Valid email format
- **Website**: Valid URLs
- **Note**: Creates ~20 merchants and reuses them across receipts

### Receipt Items
- **Name**: Realistic product names (e.g., "Ergonomic Granite Shoes", "Handcrafted Wooden Pizza")
- **Quantity**: Random 1-10 units
- **Unit Price**: Random $1-$100
- **Total Price**: Calculated (quantity × unit price)
- **Category**: Random from 15 categories (Groceries, Electronics, Clothing, etc.)
- **SKU**: Random alphanumeric code
- **Quantity Unit**: Random (pcs, kg, lb, L, gal, box, pack)
- **Description**: Product descriptions

---

## Use Cases

### 1. Performance Testing
```http
POST /api/DataSeeder/seed?userId=test-user&receiptsCount=10000&maxReceiptItemsCount=20
```
Creates 10,000 receipts to test pagination and query performance.

### 2. SerialId Pagination Testing
```http
# Seed large dataset
POST /api/DataSeeder/seed?userId=test-user&receiptsCount=5000&maxReceiptItemsCount=10

# Test pagination endpoints
GET /api/Receipts/paged?pageNumber=1&pageSize=50
GET /api/Receipts/paged?pageNumber=100&pageSize=50  # Deep pagination test
```

### 3. Cache Testing
```http
# Seed data
POST /api/DataSeeder/seed?userId=test-user&receiptsCount=1000&maxReceiptItemsCount=15

# First call - should take ~10 seconds
GET /api/Receipts/summary

# Second call - should return from Redis cache (<10ms)
GET /api/Receipts/summary
```

### 4. Data Cleanup
```http
DELETE /api/DataSeeder/clear?userId=test-user
```

---

## SQL Verification

Check seeded data in SQL Server:
```sql
-- Count receipts
SELECT COUNT(*) FROM Receipts WHERE UserId = 'd3b95bf5-4c0e-46d0-9753-1acf3df0f44d'

-- Count items
SELECT COUNT(*) FROM ReceiptItems WHERE UserId = 'd3b95bf5-4c0e-46d0-9753-1acf3df0f44d'

-- Count merchants
SELECT COUNT(*) FROM Merchants WHERE UserId = 'd3b95bf5-4c0e-46d0-9753-1acf3df0f44d'

-- Check SerialId distribution
SELECT MIN(SerialId) as MinSerial, MAX(SerialId) as MaxSerial, COUNT(*) as Total
FROM Receipts
WHERE UserId = 'd3b95bf5-4c0e-46d0-9753-1acf3df0f44d'

-- Sample data
SELECT TOP 10 
    r.ReceiptNumber,
    r.SerialId,
    r.ReceiptDate,
    r.TotalAmount,
    r.Currency,
    m.Name as MerchantName,
    COUNT(ri.Id) as ItemCount
FROM Receipts r
INNER JOIN Merchants m ON r.MerchantId = m.Id
LEFT JOIN ReceiptItems ri ON r.Id = ri.ReceiptId
WHERE r.UserId = 'd3b95bf5-4c0e-46d0-9753-1acf3df0f44d'
GROUP BY r.Id, r.ReceiptNumber, r.SerialId, r.ReceiptDate, r.TotalAmount, r.Currency, m.Name
ORDER BY r.SerialId DESC
```

---

## Performance Characteristics

### Seeding Speed
- **100 receipts**: ~3-5 seconds
- **1,000 receipts**: ~30-45 seconds
- **10,000 receipts**: ~5-8 minutes

Progress is logged every 100 receipts:
```
Seeded 100/1000 receipts...
Seeded 200/1000 receipts...
Seeded 300/1000 receipts...
```

### Database Impact
- Creates realistic foreign key relationships
- SerialId auto-increments sequentially
- All data properly validated through domain entities
- No orphaned records (proper cascade relationships)

---

## Notes

- **Authentication Required**: Both endpoints require valid JWT token
- **User Validation**: Verifies user exists before seeding
- **Transaction Safety**: Uses EF Core SaveChanges for atomicity
- **Bogus Library**: Generates realistic, locale-aware test data
- **No Duplicates**: Each receipt gets unique receipt number
- **Realistic Relationships**: Merchants are reused across receipts (real-world scenario)

---

## Troubleshooting

### Error: "User with ID 'xxx' does not exist"
**Solution**: User must exist in `AspNetUsers` table before seeding.
```sql
-- Check if user exists
SELECT * FROM AspNetUsers WHERE Id = 'your-user-id'
```

### Seeding Too Slow
**Solution**: Reduce batch size or optimize database:
- Disable foreign key checks temporarily (not recommended for production)
- Ensure indexes exist (especially on SerialId after migration)
- Run seeding during off-hours

### Out of Memory
**Solution**: Seed in batches:
```http
# Instead of 10,000 at once
POST /api/DataSeeder/seed?userId=test&receiptsCount=10000

# Seed 5 batches of 2,000
POST /api/DataSeeder/seed?userId=test&receiptsCount=2000
POST /api/DataSeeder/seed?userId=test&receiptsCount=2000
POST /api/DataSeeder/seed?userId=test&receiptsCount=2000
POST /api/DataSeeder/seed?userId=test&receiptsCount=2000
POST /api/DataSeeder/seed?userId=test&receiptsCount=2000
```

---

## Example: Complete Test Workflow

```bash
# 1. Clear existing data
DELETE http://192.168.0.68:5091/api/DataSeeder/clear?userId=test-user

# 2. Seed fresh dataset
POST http://192.168.0.68:5091/api/DataSeeder/seed?userId=test-user&receiptsCount=5000&maxReceiptItemsCount=15

# 3. Test pagination performance
GET http://192.168.0.68:5091/api/Receipts/paged?pageNumber=1&pageSize=50
GET http://192.168.0.68:5091/api/Receipts/paged?pageNumber=50&pageSize=50  # Page 50
GET http://192.168.0.68:5091/api/Receipts/paged?pageNumber=100&pageSize=50 # Page 100 (deep pagination)

# 4. Test summary cache
GET http://192.168.0.68:5091/api/Receipts/summary  # First call (slow)
GET http://192.168.0.68:5091/api/Receipts/summary  # Second call (instant)

# 5. Cleanup
DELETE http://192.168.0.68:5091/api/DataSeeder/clear?userId=test-user
```
