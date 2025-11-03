using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReceiptScanner.Application.DTOs;
using ReceiptScanner.Application.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReceiptScanner.Application.Services;

/// <summary>
/// Service for interacting with Ollama LLM API
/// </summary>
public class GPTHelperService : IGPTHelperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GPTHelperService> _logger;
    private readonly string _ollamaBaseUrl;

    public GPTHelperService(HttpClient httpClient, ILogger<GPTHelperService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
        
                   // Configure HttpClient
        _httpClient.BaseAddress = new Uri(_ollamaBaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Ollama can take time for large prompts
    }

    /// <summary>
    /// Send a prompt to Ollama and get the response text
    /// </summary>
    public async Task<string> SendPromptAsync(string prompt, string model = "llama3")
    {
        try
        {
            var response = await SendPromptDetailedAsync(prompt, model);
            return response?.Response ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending prompt to Ollama");
            throw;
        }
    }

    /// <summary>
    /// Send a prompt to Ollama and get the full response object
    /// </summary>
    public async Task<OllamaResponse?> SendPromptDetailedAsync(string prompt, string model = "llama3")
    {
        try
        {
            _logger.LogInformation("Sending prompt to Ollama. Model: {Model}, Prompt length: {Length}", 
                model, prompt.Length);

            var request = new OllamaRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var httpResponse = await _httpClient.PostAsJsonAsync("/api/generate", request, jsonOptions);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorContent = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogError("Ollama API error. Status: {Status}, Content: {Content}", 
                    httpResponse.StatusCode, errorContent);
                throw new HttpRequestException($"Ollama API returned {httpResponse.StatusCode}: {errorContent}");
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<OllamaResponse>(jsonOptions);

            if (response != null)
            {
                _logger.LogInformation("Ollama response received. Length: {Length}, Duration: {Duration}ms", 
                    response.Response.Length, response.Total_Duration / 1_000_000);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error communicating with Ollama at {Url}", _ollamaBaseUrl);
            throw new InvalidOperationException($"Failed to connect to Ollama at {_ollamaBaseUrl}. Ensure Ollama is running.", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Ollama request timed out");
            throw new TimeoutException("The request to Ollama timed out. The prompt may be too complex.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending prompt to Ollama");
            throw;
        }
    }

    /// <summary>
    /// Check if Ollama service is available
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama service is not available at {Url}", _ollamaBaseUrl);
            return false;
        }
    }
}
