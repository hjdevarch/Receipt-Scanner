using System.Text.Json;

namespace ReceiptScanner.API.Helpers;

/// <summary>
/// Helper class for logging models to text files
/// </summary>
public static class FileLogger
{
    private static readonly string _logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "RequestPayloads");

    static FileLogger()
    {
        // Ensure log directory exists
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    /// <summary>
    /// Generic method to log any model to a text file
    /// </summary>
    /// <typeparam name="T">Type of the model to log</typeparam>
    /// <param name="model">The model instance to log</param>
    /// <param name="logger">ILogger instance for logging messages</param>
    /// <param name="fileName">Optional custom file name (without extension)</param>
    /// <param name="additionalInfo">Optional dictionary of additional information to include in the log</param>
    public static async Task LogModelToFileAsync<T>(T model, ILogger logger, string? fileName = null, Dictionary<string, string>? additionalInfo = null)
    {
        try
        {
            // Generate file name with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var modelType = typeof(T).Name;
            var logFileName = fileName ?? $"{modelType}_{timestamp}.txt";
            var logFilePath = Path.Combine(_logDirectory, logFileName);

            // Serialize model to JSON
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var modelJson = JsonSerializer.Serialize(model, jsonOptions);

            // Build log content
            var logContent = new System.Text.StringBuilder();
            logContent.AppendLine("=".PadRight(80, '='));
            logContent.AppendLine($"Log Entry: {typeof(T).Name}");
            logContent.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}");
            logContent.AppendLine($"Timestamp (Local): {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            logContent.AppendLine("=".PadRight(80, '='));
            logContent.AppendLine();

            // Add additional information if provided
            if (additionalInfo != null && additionalInfo.Any())
            {
                logContent.AppendLine("Additional Information:");
                foreach (var kvp in additionalInfo)
                {
                    logContent.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
                logContent.AppendLine();
            }

            logContent.AppendLine("Model Data (JSON):");
            logContent.AppendLine(modelJson);
            logContent.AppendLine();
            logContent.AppendLine("=".PadRight(80, '='));

            // Write to file asynchronously
            await File.WriteAllTextAsync(logFilePath, logContent.ToString());

            logger.LogInformation("Model logged to file: {FilePath}", logFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error logging model to file");
        }
    }
}
