# Receipt Scanner API

A .NET Core Clean Architecture application for uploading and processing receipt images using Azure Document Intelligence.

## Features

- 📷 **Receipt Image Upload**: Support for JPEG, PNG, BMP, TIFF, and PDF files
- 🤖 **AI Text Extraction**: Azure Document Intelligence for accurate receipt parsing
- 🧠 **AI-Powered Categorization**: Automatic categorization of receipt items using GPT/Ollama
- � **JWT Authentication**: Secure multi-tenant user authentication and authorization
- �📊 **Data Storage**: SQL Server database with Entity Framework Core
- 🏗️ **Clean Architecture**: Domain-driven design with proper separation of concerns
- 🔍 **RESTful API**: Comprehensive REST endpoints for receipt management
- 💬 **GPT Integration**: Local LLM integration via Ollama for intelligent features

## Architecture

This project follows Clean Architecture principles with the following layers:

```
src/
├── ReceiptScanner.Domain/          # Core business logic and entities
├── ReceiptScanner.Application/     # Application services and DTOs  
├── ReceiptScanner.Infrastructure/  # Data access and external services
└── ReceiptScanner.API/            # Web API controllers and configuration
```

## Prerequisites

- .NET 10.0 (Preview)
- SQL Server LocalDB or SQL Server instance
- Azure Document Intelligence service (Cognitive Services)
- Ollama (for GPT/AI categorization features)

## Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd "Receipt Scanner"
   ```

2. **Configure Azure Document Intelligence**
   
   Update `appsettings.json` with your Azure credentials:
   ```json
   {
     "AzureDocumentIntelligence": {
       "Endpoint": "https://your-service.cognitiveservices.azure.com/",
       "ApiKey": "your-api-key"
     }
   }
   ```

3. **Configure Database Connection**
   
   The application uses SQL Server LocalDB by default. Update the connection string in `appsettings.json` if needed:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ReceiptScannerDB_Dev;Trusted_Connection=true;MultipleActiveResultSets=true;"
     }
   }
   ```

4. **Build and Run**
   ```bash
   dotnet build
   dotnet run --project src/ReceiptScanner.API
   ```

   The API will be available at `http://localhost:5091`

## API Endpoints

### Receipts

- `POST /api/receipts/upload` - Upload and process a receipt image
  - **Body**: Multipart form data with `receiptImage` file
  - **Optional**: `receiptNumber`, `receiptDate` form fields
  
- `GET /api/receipts` - Get all receipts
- `GET /api/receipts/{id}` - Get receipt by ID
- `GET /api/receipts/date-range?startDate={start}&endDate={end}` - Get receipts by date range
- `GET /api/receipts/merchant/{merchantId}` - Get receipts by merchant
- `DELETE /api/receipts/{id}` - Delete a receipt

### Merchants

- `GET /api/merchants` - Get all merchants
- `GET /api/merchants/{id}` - Get merchant by ID
- `GET /api/merchants/search?name={name}` - Search merchants by name

## Data Models

### Receipt
- Receipt number, date, totals
- Merchant information
- Receipt items with quantities and prices
- Processing status and metadata

### Merchant
- Name, address, contact information
- Automatically created from receipt processing

### Receipt Item
- Item name, description, category
- Quantity, unit price, total price
- SKU information

## Technologies Used

- **.NET 10.0** - Latest framework version
- **Entity Framework Core 9.0.10** - ORM and database access
- **Azure Document Intelligence** - Receipt text extraction and analysis
- **SQL Server** - Database storage
- **Clean Architecture** - Application structure and design patterns

## Testing

Use the provided `ReceiptScanner.API.http` file with VS Code REST Client extension to test the API endpoints:

```http
### Get all receipts
GET http://localhost:5091/api/receipts

### Upload receipt
POST http://localhost:5091/api/receipts/upload
Content-Type: multipart/form-data
```

## Database

The application automatically creates the database and applies migrations on startup. The following tables are created:

- **Merchants** - Store merchant information
- **Receipts** - Store receipt headers with totals
- **ReceiptItems** - Store individual line items

## Development Notes

- **Swagger Documentation**: Temporarily disabled due to .NET 10.0 preview compatibility issues
- **CORS**: Enabled for development with "AllowAll" policy
- **Logging**: Comprehensive logging throughout the application
- **Error Handling**: Proper exception handling and user-friendly error messages

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License.

A .NET Core application built with Clean Architecture principles for scanning and processing receipts.

## Architecture

This project follows Clean Architecture principles with the following layers:

### Core (src/ReceiptScanner.Domain)
- **Entities**: Business entities with business rules
- **Value Objects**: Immutable objects that describe aspects of the domain
- **Interfaces**: Contracts for repositories and services

### Application (src/ReceiptScanner.Application)
- **Services**: Application business logic and use cases
- **DTOs**: Data Transfer Objects for communication between layers
- **Interfaces**: Application service contracts

### Infrastructure (src/ReceiptScanner.Infrastructure)
- **Data**: Database context and configurations
- **Repositories**: Data access implementations
- **Services**: External service implementations

### Presentation (src/ReceiptScanner.API)
- **Controllers**: API endpoints
- **Configuration**: Application configuration and startup

## Getting Started

### Prerequisites
- .NET 10.0 SDK
- Visual Studio Code or Visual Studio

### Building the Project
```bash
dotnet build
```

### Running the API
```bash
dotnet run --project src/ReceiptScanner.API
```

## Project Structure
```
├── src/
│   ├── ReceiptScanner.Domain/          # Core business logic
│   ├── ReceiptScanner.Application/     # Application services
│   ├── ReceiptScanner.Infrastructure/  # Data access and external services
│   └── ReceiptScanner.API/            # Web API
├── tests/                             # Test projects
└── ReceiptScanner.sln                 # Solution file
```

## Documentation

- 📚 **[Swagger Documentation Guide](SWAGGER_DOCUMENTATION.md)** - API documentation and testing with Swagger/OpenAPI
- 🔐 **[Authentication Guide](AUTHENTICATION_GUIDE.md)** - JWT authentication and multi-tenant setup
- 🏷️ **[Category System Documentation](CATEGORY_SYSTEM_DOCUMENTATION.md)** - AI-powered automatic categorization
- 💬 **[GPT Helper Documentation](GPTHELPER_DOCUMENTATION.md)** - GPT/Ollama integration guide
- 📝 **[File Logger Usage](FILELOGGER_USAGE.md)** - Generic file logging helper

## Development

This project is ready for business logic implementation. The next steps will involve:
1. Defining specific domain entities for receipt processing
2. Implementing use cases in the Application layer
3. Setting up data persistence in the Infrastructure layer
4. Creating API endpoints for receipt scanning functionality