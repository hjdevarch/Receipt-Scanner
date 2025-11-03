using ReceiptScanner.Application.DTOs;

namespace ReceiptScanner.Application.Interfaces;

/// <summary>
/// Interface for GPT/LLM helper service using Ollama
/// </summary>
public interface IGPTHelperService
{
    /// <summary>
    /// Send a prompt to Ollama and get the response
    /// </summary>
    /// <param name="prompt">The prompt to send</param>
    /// <param name="model">The model to use (default: llama3)</param>
    /// <returns>The response from Ollama</returns>
    Task<string> SendPromptAsync(string prompt, string model = "llama3");

    /// <summary>
    /// Send a prompt to Ollama and get the full response object
    /// </summary>
    /// <param name="prompt">The prompt to send</param>
    /// <param name="model">The model to use (default: llama3)</param>
    /// <returns>The full Ollama response object</returns>
    Task<OllamaResponse?> SendPromptDetailedAsync(string prompt, string model = "llama3");

    /// <summary>
    /// Check if Ollama service is available
    /// </summary>
    /// <returns>True if Ollama is running and accessible</returns>
    Task<bool> IsAvailableAsync();
}
