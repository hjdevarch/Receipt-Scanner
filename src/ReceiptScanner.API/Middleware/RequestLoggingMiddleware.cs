using System.Security.Claims;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using ReceiptScanner.API.Helpers;
using ReceiptScanner.API.Models;

namespace ReceiptScanner.API.Middleware;

/// <summary>
/// Middleware that logs HTTP requests and responses to disk when enabled.
/// </summary>
public class RequestLoggingMiddleware
{
    private const int DefaultMaxBodyLength = 10_000; // Avoid excessively large log files

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isEnabled = _configuration.GetValue<bool>("RequestLogging:Enabled");
        if (!isEnabled)
        {
            await _next(context);
            return;
        }

        var maxBodyLength = _configuration.GetValue<int?>("RequestLogging:MaxBodyLength") ?? DefaultMaxBodyLength;

        context.Request.EnableBuffering();
        var requestBody = await ReadBodyAsync(context.Request, maxBodyLength);

        var logEntry = CreateLogEntry(context, requestBody);

        var originalResponseBodyStream = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            logEntry.ExceptionMessage = ex.Message;
            logEntry.ExceptionStackTrace = ex.StackTrace;
            logEntry.StatusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseText = await ReadStreamAsync(context.Response.Body, maxBodyLength);
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            logEntry.StatusCode = context.Response.StatusCode;
            logEntry.ResponseBody = responseText;
            logEntry.ResponseHeaders = context.Response.Headers.ToDictionary(
                h => h.Key,
                h => string.Join(",", h.Value.ToArray()));

            await WriteLogAsync(logEntry, context);

            await responseBody.CopyToAsync(originalResponseBodyStream);
            context.Response.Body = originalResponseBodyStream;
        }
    }

    private static RequestLogEntry CreateLogEntry(HttpContext context, string requestBody)
    {
        var user = context.User;
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user?.Identity?.IsAuthenticated == true ? user.Identity.Name : null;

        var remoteIp = context.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers["User-Agent"].ToString();

        var sanitizedPathSegment = SanitizeForFileName(context.Request.Path.HasValue ? context.Request.Path.Value! : "root");

        return new RequestLogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Method = context.Request.Method,
            Path = context.Request.Path,
            QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
            RequestBody = requestBody,
            RemoteIpAddress = remoteIp,
            UserAgent = userAgent,
            UserId = userId,
            UserName = userName,
            RequestHeaders = context.Request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value.ToArray())),
            FileSafePathSegment = sanitizedPathSegment
        };
    }

    private async Task WriteLogAsync(RequestLogEntry logEntry, HttpContext context)
    {
        try
        {
            var timestamp = logEntry.TimestampUtc.ToString("yyyyMMdd_HHmmss_fff");
            var fileName = $"HttpRequest_{timestamp}_{logEntry.Method}_{logEntry.FileSafePathSegment}.txt";

            var additionalInfo = new Dictionary<string, string>
            {
                { "RequestPath", logEntry.Path },
                { "StatusCode", logEntry.StatusCode.ToString() },
                { "RemoteIp", logEntry.RemoteIpAddress ?? "Unknown" }
            };

            if (!string.IsNullOrWhiteSpace(logEntry.UserId))
            {
                additionalInfo.Add("UserId", logEntry.UserId);
            }

            if (!string.IsNullOrWhiteSpace(logEntry.UserName))
            {
                additionalInfo.Add("UserName", logEntry.UserName!);
            }

            await FileLogger.LogModelToFileAsync(logEntry, _logger, fileName, additionalInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist HTTP request log entry");
        }
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request, int maxLength)
    {
        if (request.ContentLength == null || request.ContentLength == 0)
        {
            return string.Empty;
        }

        request.Body.Seek(0, SeekOrigin.Begin);
        var bodyText = await ReadStreamAsync(request.Body, maxLength);
        request.Body.Seek(0, SeekOrigin.Begin);
        return bodyText;
    }

    private static async Task<string> ReadStreamAsync(Stream stream, int maxLength)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var text = await reader.ReadToEndAsync();

        if (text.Length > maxLength)
        {
            return text.Substring(0, maxLength) + "... [truncated]";
        }

        return text;
    }

    private static string SanitizeForFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "root";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            if (invalidChars.Contains(c) || c == '/' || c == '\\')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(c);
            }
        }

        var sanitized = builder.ToString();
        return string.IsNullOrWhiteSpace(sanitized) ? "root" : sanitized;
    }
}
