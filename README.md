# Receipt Scanner

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

## Development

This project is ready for business logic implementation. The next steps will involve:
1. Defining specific domain entities for receipt processing
2. Implementing use cases in the Application layer
3. Setting up data persistence in the Infrastructure layer
4. Creating API endpoints for receipt scanning functionality