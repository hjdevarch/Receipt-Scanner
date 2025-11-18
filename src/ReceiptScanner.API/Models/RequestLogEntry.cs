using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ReceiptScanner.API.Models;

/// <summary>
/// Represents a single HTTP request/response log entry written to disk.
/// </summary>
public sealed class RequestLogEntry
{
    public DateTime TimestampUtc { get; init; }
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string? QueryString { get; init; }
    public int StatusCode { get; set; }
    public string? RequestBody { get; init; }
    public string? ResponseBody { get; set; }
    public string? RemoteIpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? UserId { get; init; }
    public string? UserName { get; init; }

    public Dictionary<string, string> RequestHeaders { get; set; } = new();
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();

    public string? ExceptionMessage { get; set; }
    public string? ExceptionStackTrace { get; set; }

    [JsonIgnore]
    public string FileSafePathSegment { get; init; } = string.Empty;
}
