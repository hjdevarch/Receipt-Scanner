using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ReceiptScanner.API.Extensions;
using ReceiptScanner.API.Filters;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using ReceiptScanner.API.Middleware;
using ReceiptScanner.Application.Settings;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to include console output
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ReceiptScanner_";
});
builder.Services.AddApplicationServices(builder.Configuration);

// Configure JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// Configure Email Settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Configure Application Settings
builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetSection("ApplicationSettings"));

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings!.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure Rate Limiting
var rateLimitSettings = builder.Configuration.GetSection("RateLimiting");
var enableRateLimiting = rateLimitSettings.GetValue<bool>("Enabled");

if (enableRateLimiting)
{
    builder.Services.AddRateLimiter(options =>
    {
        // Default policy - Fixed window per user
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var userId = httpContext.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: userId,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitSettings.GetValue<int>("PermitLimit", 100),
                    Window = TimeSpan.FromMinutes(rateLimitSettings.GetValue<int>("WindowMinutes", 1)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = rateLimitSettings.GetValue<int>("QueueLimit", 0)
                });
        });

        // Policy for authentication endpoints (more lenient)
        options.AddPolicy("auth", httpContext =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: ipAddress,
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitSettings.GetValue<int>("AuthPermitLimit", 20),
                    Window = TimeSpan.FromMinutes(rateLimitSettings.GetValue<int>("AuthWindowMinutes", 15)),
                    SegmentsPerWindow = 3,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        // Policy for file upload endpoints (more restrictive)
        options.AddPolicy("upload", httpContext =>
        {
            var userId = httpContext.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: userId,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitSettings.GetValue<int>("UploadPermitLimit", 10),
                    Window = TimeSpan.FromMinutes(rateLimitSettings.GetValue<int>("UploadWindowMinutes", 1)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        // Policy for read-only operations (more lenient)
        options.AddPolicy("readonly", httpContext =>
        {
            var userId = httpContext.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous"
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: userId,
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitSettings.GetValue<int>("ReadOnlyPermitLimit", 200),
                    Window = TimeSpan.FromMinutes(rateLimitSettings.GetValue<int>("ReadOnlyWindowMinutes", 1)),
                    SegmentsPerWindow = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";

            var retryAfterSeconds = 0.0;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                retryAfterSeconds = retryAfter.TotalSeconds;
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfterSeconds).ToString();
            }

            var response = new
            {
                error = "Too Many Requests",
                message = "Rate limit exceeded. Please try again later.",
                retryAfter = retryAfterSeconds > 0 ? retryAfterSeconds : (double?)null
            };

            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        };
    });
}

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Receipt Scanner API",
        Version = "v1",
        Description = "An API for processing receipt images using Azure Document Intelligence with JWT authentication",
        Contact = new OpenApiContact
        {
            Name = "Receipt Scanner",
            Email = "support@receiptscanner.com"
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Enable annotations
    c.EnableAnnotations();

    // Configure multipart/form-data for file uploads
    c.OperationFilter<FileUploadOperationFilter>();

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Receipt Scanner API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
        c.EnableValidator();
    });
    app.UseCors("AllowAll");
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();

// Enable rate limiting if configured
if (enableRateLimiting)
{
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ReceiptScanner.Infrastructure.Data.ReceiptScannerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        context.Database.Migrate();
        logger.LogInformation("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database");
        throw;
    }
}

app.Run();
