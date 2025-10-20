using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Application.Interfaces;
using ReceiptScanner.Application.Services;
using ReceiptScanner.Domain.Interfaces;
using ReceiptScanner.Infrastructure.Data;
using ReceiptScanner.Infrastructure.Repositories;

namespace ReceiptScanner.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add Entity Framework
        services.AddDbContext<ReceiptScannerDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Register repositories
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IMerchantRepository, MerchantRepository>();

        // Register application services
        services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
        services.AddScoped<IDocumentIntelligenceService, AzureDocumentIntelligenceService>();

        return services;
    }
}