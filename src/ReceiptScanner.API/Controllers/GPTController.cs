using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptScanner.Application.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace ReceiptScanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GPTController : ControllerBase
{
    private readonly IGPTHelperService _gptHelperService;
    private readonly ILogger<GPTController> _logger;

    public GPTController(IGPTHelperService gptHelperService, ILogger<GPTController> logger)
    {
        _gptHelperService = gptHelperService;
        _logger = logger;
    }

    /// <summary>
    /// Check if Ollama service is available
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "Check Ollama service status",
        Description = "Checks if the Ollama service is running and accessible.",
        OperationId = "CheckOllamaStatus"
    )]
    public async Task<ActionResult<object>> CheckStatus()
    {
        var isAvailable = await _gptHelperService.IsAvailableAsync();
        return Ok(new
        {
            available = isAvailable,
            message = isAvailable ? "Ollama service is running" : "Ollama service is not available"
        });
    }

    /// <summary>
    /// Send a prompt to Ollama
    /// </summary>
    /// <param name="request">The prompt request</param>
    [HttpPost("prompt")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Send a prompt to Ollama",
        Description = "Sends a text prompt to Ollama LLM and returns the response.",
        OperationId = "SendPrompt"
    )]
    public async Task<ActionResult<object>> SendPrompt([FromBody] PromptRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest("Prompt cannot be empty");
            }

            _logger.LogInformation("Sending prompt to Ollama. Model: {Model}", request.Model ?? "llama3");

            var response = await _gptHelperService.SendPromptAsync(request.Prompt, request.Model ?? "llama3");

            return Ok(new
            {
                prompt = request.Prompt,
                model = request.Model ?? "llama3",
                response = response
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Ollama service not available");
            return StatusCode(503, new { error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Ollama request timed out");
            return StatusCode(408, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing prompt");
            return StatusCode(500, new { error = "An error occurred while processing the prompt" });
        }
    }

    /// <summary>
    /// Send a prompt to Ollama and get detailed response
    /// </summary>
    /// <param name="request">The prompt request</param>
    [HttpPost("prompt/detailed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [SwaggerOperation(
        Summary = "Send a prompt to Ollama with detailed response",
        Description = "Sends a text prompt to Ollama LLM and returns the full response including timing and context information.",
        OperationId = "SendPromptDetailed"
    )]
    public async Task<ActionResult> SendPromptDetailed([FromBody] PromptRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return BadRequest("Prompt cannot be empty");
            }

            _logger.LogInformation("Sending detailed prompt to Ollama. Model: {Model}", request.Model ?? "llama3");

            var response = await _gptHelperService.SendPromptDetailedAsync(request.Prompt, request.Model ?? "llama3");

            if (response == null)
            {
                return StatusCode(500, new { error = "No response received from Ollama" });
            }

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Ollama service not available");
            return StatusCode(503, new { error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Ollama request timed out");
            return StatusCode(408, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing prompt");
            return StatusCode(500, new { error = "An error occurred while processing the prompt" });
        }
    }
}

/// <summary>
/// Request model for GPT prompts
/// </summary>
public class PromptRequest
{
    /// <summary>
    /// The text prompt to send to the LLM
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// The model to use (default: llama3)
    /// </summary>
    public string? Model { get; set; }
}
