# Swagger API Documentation

The Receipt Scanner API now includes comprehensive Swagger/OpenAPI documentation for easy testing and integration.

## Accessing Swagger UI

When running the API in development mode, Swagger UI is available at:

**Root URL**: http://localhost:5091/

This provides an interactive interface where you can:
- View all available endpoints
- Test API calls directly from the browser
- See request/response schemas
- Download the OpenAPI specification

## Swagger JSON Specification

The raw OpenAPI specification is available at:

**Swagger JSON**: http://localhost:5091/swagger/v1/swagger.json

## Key Features

### 📋 **Complete API Documentation**
- All endpoints documented with descriptions
- Request/response examples
- Parameter validation info
- Error response codes

### 📸 **File Upload Support**
- Special handling for receipt image uploads
- Multipart/form-data documentation
- File type and size restrictions clearly documented

### 💰 **Currency Information**
- Both currency codes (GBP, USD, EUR) and symbols (£, $, €) documented
- All GET endpoints now return currency symbols

### 🏷️ **Organized by Tags**
- All receipt-related endpoints grouped under "Receipts" tag
- Clean, organized interface

## API Endpoints Overview

### Receipt Management
- `POST /api/receipts/upload` - Upload and process receipt images
- `GET /api/receipts` - Get all receipts with currency symbols
- `GET /api/receipts/{id}` - Get specific receipt by ID
- `GET /api/receipts/merchant/{merchantId}` - Get receipts by merchant
- `GET /api/receipts/date-range` - Get receipts by date range
- `PUT /api/receipts/{id}` - Update existing receipt
- `DELETE /api/receipts/{id}` - Delete receipt

### New Currency Symbol Feature
All GET endpoints now return both:
- `currency`: Currency code (e.g., "GBP", "USD", "EUR")
- `currencySymbol`: Currency symbol (e.g., "£", "$", "€")

## Development Commands

```bash
# Start the API with Swagger
cd "src/ReceiptScanner.API"
dotnet run

# API will be available at:
# - API: http://localhost:5091/api/receipts
# - Swagger UI: http://localhost:5091/
# - Swagger JSON: http://localhost:5091/swagger/v1/swagger.json
```

## Example Response with Currency Symbols

```json
{
  "id": "12345678-1234-1234-1234-123456789012",
  "receiptNumber": "R-001",
  "receiptDate": "2024-10-29T10:30:00Z",
  "subTotal": 21.25,
  "taxAmount": 4.25,
  "totalAmount": 25.50,
  "currency": "GBP",
  "currencySymbol": "£",
  "status": "Processed",
  "merchant": {
    "name": "Sample Store",
    "address": "123 High Street, London"
  },
  "items": [
    {
      "name": "Coffee",
      "quantity": 2,
      "unitPrice": 3.50,
      "totalPrice": 7.00
    }
  ]
}
```

## Testing with Swagger UI

1. Open http://localhost:5091/ in your browser
2. Expand any endpoint to see details
3. Click "Try it out" to test the endpoint
4. For file uploads, use the file selection interface
5. View responses with proper currency formatting

## Integration

The Swagger specification can be used to:
- Generate client SDKs in various languages
- Import into Postman or other API tools
- Auto-generate documentation
- Validate API contracts