using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReceiptScanner.Application.Interfaces;
using ReceiptScanner.Application.Services;
using ReceiptScanner.Domain.Entities;
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

        // Add Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 6;
            options.Password.RequiredUniqueChars = 1;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ReceiptScannerDbContext>()
        .AddDefaultTokenProviders();

        // Register repositories
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IItemNameRepository, ItemNameRepository>();

        // Register application services
        services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
        services.AddScoped<IDocumentIntelligenceService, AzureDocumentIntelligenceService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ReceiptItemService>();
        services.AddScoped<ItemCategorizationJobService>();
        
        // Register GPT/Ollama service with HttpClient
        services.AddHttpClient<IGPTHelperService, GPTHelperService>();

        return services;
    }
}