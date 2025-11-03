# GPTHelper Service Documentation

## Overview
The `GPTHelperService` provides integration with Ollama, a local LLM (Large Language Model) service. It allows you to send prompts to various AI models (like Llama3) and receive responses.

## Architecture

### Files Created
1. **DTOs** - `src/ReceiptScanner.Application/DTOs/OllamaDtos.cs`
   - `OllamaRequest` - Request model for Ollama API
   - `OllamaResponse` - Response model from Ollama API

2. **Interface** - `src/ReceiptScanner.Application/Interfaces/IGPTHelperService.cs`
   - `SendPromptAsync()` - Send prompt and get simple text response
   - `SendPromptDetailedAsync()` - Send prompt and get full response object
   - `IsAvailableAsync()` - Check if Ollama is running

3. **Implementation** - `src/ReceiptScanner.Application/Services/GPTHelperService.cs`
   - Full implementation with error handling, logging, and timeout support

4. **Controller** - `src/ReceiptScanner.API/Controllers/GPTController.cs`
   - REST API endpoints for testing and using the GPT service

## Configuration

### appsettings.json
```json
{
  "Ollama": {
    "BaseUrl": "http://127.0.0.1:11434"
  }
}
```

### Service Registration (Already configured)
In `ServiceCollectionExtensions.cs`:
```csharp
services.AddHttpClient<IGPTHelperService, GPTHelperService>();
```

## API Endpoints

### 1. Check Ollama Status
```http
GET /api/gpt/status
Authorization: Bearer {token}
```

**Response:**
```json
{
  "available": true,
  "message": "Ollama service is running"
}
```

### 2. Send Simple Prompt
```http
POST /api/gpt/prompt
Authorization: Bearer {token}
Content-Type: application/json

{
  "prompt": "What is the capital of France?",
  "model": "llama3"
}
```

**Response:**
```json
{
  "prompt": "What is the capital of France?",
  "model": "llama3",
  "response": "The capital of France is Paris."
}
```

### 3. Send Prompt with Detailed Response
```http
POST /api/gpt/prompt/detailed
Authorization: Bearer {token}
Content-Type: application/json

{
  "prompt": "Explain quantum computing in simple terms",
  "model": "llama3"
}
```

**Response:**
```json
{
  "model": "llama3",
  "created_at": "2025-11-02T20:00:52.4830492Z",
  "response": "Quantum computing is...",
  "done": true,
  "done_reason": "stop",
  "context": [128006, 882, ...],
  "total_duration": 8754855300,
  "load_duration": 687650600,
  "prompt_eval_count": 275,
  "prompt_eval_duration": 467400300,
  "eval_count": 240,
  "eval_duration": 6925428800
}
```

## Usage in Code

### Example 1: Simple Usage
```csharp
public class MyService
{
    private readonly IGPTHelperService _gptHelper;

    public MyService(IGPTHelperService gptHelper)
    {
        _gptHelper = gptHelper;
    }

    public async Task<string> AnalyzeReceipt(string receiptText)
    {
        var prompt = $"Analyze this receipt and extract key information:\n{receiptText}";
        var response = await _gptHelper.SendPromptAsync(prompt);
        return response;
    }
}
```

### Example 2: Detailed Response with Metrics
```csharp
public async Task<AnalysisResult> DetailedAnalysis(string text)
{
    var prompt = $"Categorize these items: {text}";
    var response = await _gptHelper.SendPromptDetailedAsync(prompt);
    
    return new AnalysisResult
    {
        Text = response.Response,
        ProcessingTimeMs = response.Total_Duration / 1_000_000,
        TokensUsed = response.Eval_Count
    };
}
```

### Example 3: Check Availability Before Use
```csharp
public async Task<string> SafePrompt(string text)
{
    if (!await _gptHelper.IsAvailableAsync())
    {
        return "Ollama service is not available";
    }
    
    return await _gptHelper.SendPromptAsync(text);
}
```

### Example 4: Using Different Models
```csharp
// Use different Ollama models
var response1 = await _gptHelper.SendPromptAsync("Hello", "llama3");
var response2 = await _gptHelper.SendPromptAsync("Hello", "mistral");
var response3 = await _gptHelper.SendPromptAsync("Hello", "codellama");
```

## Use Cases in Receipt Scanner

### 1. Receipt Item Categorization
```csharp
var prompt = $@"Categorize these receipt items into food, drinks, household, etc:
{string.Join("\n", items.Select(i => i.Name))}

Return only the category names separated by commas.";

var categories = await _gptHelper.SendPromptAsync(prompt);
```

### 2. Merchant Name Normalization
```csharp
var prompt = $@"Normalize this merchant name to a standard format:
Input: '{merchantName}'

Return only the normalized name.";

var normalizedName = await _gptHelper.SendPromptAsync(prompt);
```

### 3. Receipt Data Validation
```csharp
var prompt = $@"Check if this receipt data looks correct:
Total: ${total}
Items: {itemCount}
Sum of items: ${itemsSum}

Is there a calculation error? Reply 'YES' or 'NO' and explain.";

var validation = await _gptHelper.SendPromptAsync(prompt);
```

### 4. Smart Search Suggestions
```csharp
var prompt = $@"The user searched for '{searchTerm}'. 
Suggest 3 related search terms for receipts/shopping.
Format: term1, term2, term3";

var suggestions = await _gptHelper.SendPromptAsync(prompt);
```

## Error Handling

The service handles various error scenarios:

### 1. Service Unavailable (503)
```csharp
catch (InvalidOperationException ex)
{
    // Ollama is not running or not accessible
    // Message: "Failed to connect to Ollama at http://127.0.0.1:11434"
}
```

### 2. Timeout (408)
```csharp
catch (TimeoutException ex)
{
    // Request took too long (default: 5 minutes)
    // Message: "The request to Ollama timed out"
}
```

### 3. HTTP Errors
```csharp
catch (HttpRequestException ex)
{
    // Network or HTTP-level errors
}
```

## Performance Considerations

### Response Times (from OllamaResponse)
- `total_duration` - Total time in nanoseconds
- `load_duration` - Model loading time
- `prompt_eval_duration` - Prompt processing time
- `eval_duration` - Response generation time

### Example Metrics:
```csharp
var response = await _gptHelper.SendPromptDetailedAsync(prompt);

Console.WriteLine($"Total time: {response.Total_Duration / 1_000_000}ms");
Console.WriteLine($"Load time: {response.Load_Duration / 1_000_000}ms");
Console.WriteLine($"Tokens generated: {response.Eval_Count}");
```

## Prerequisites

### 1. Install Ollama
```bash
# Download from: https://ollama.ai
# Or use package manager
winget install Ollama.Ollama
```

### 2. Pull a Model
```bash
ollama pull llama3
ollama pull mistral
ollama pull codellama
```

### 3. Start Ollama (if not running as service)
```bash
ollama serve
```

### 4. Verify Ollama is Running
```bash
curl http://localhost:11434/api/tags
```

## Testing with curl

```bash
# Test directly with Ollama
curl -X POST http://127.0.0.1:11434/api/generate \
  -H "Content-Type: application/json" \
  -d '{
    "model": "llama3",
    "prompt": "Why is the sky blue?",
    "stream": false
  }'
```

## Best Practices

1. **Always check availability** before critical operations:
   ```csharp
   if (!await _gptHelper.IsAvailableAsync())
   {
       // Handle offline scenario
   }
   ```

2. **Use appropriate timeouts** for long prompts:
   ```csharp
   // The service has a default 5-minute timeout
   // Adjust in constructor if needed
   ```

3. **Log performance metrics**:
   ```csharp
   var response = await _gptHelper.SendPromptDetailedAsync(prompt);
   _logger.LogInformation("LLM processing took {Duration}ms", 
       response.Total_Duration / 1_000_000);
   ```

4. **Handle errors gracefully**:
   ```csharp
   try
   {
       var response = await _gptHelper.SendPromptAsync(prompt);
       return response;
   }
   catch (Exception)
   {
       return "Using default behavior - LLM not available";
   }
   ```

5. **Keep prompts concise** for better performance

6. **Use streaming for long responses** (set `Stream = true` if needed)

## Security Notes

- ✅ All endpoints require JWT authentication (`[Authorize]` attribute)
- ✅ Ollama runs locally by default (no data sent to cloud)
- ⚠️ Be careful with sensitive receipt data in prompts
- ⚠️ Consider data retention policies for logged prompts

## Troubleshooting

### Ollama Not Available
**Symptom:** `IsAvailableAsync()` returns false

**Solutions:**
1. Check if Ollama is running: `ollama serve`
2. Verify the BaseUrl in appsettings.json
3. Check firewall settings
4. Ensure model is pulled: `ollama pull llama3`

### Timeout Errors
**Symptom:** TimeoutException after 5 minutes

**Solutions:**
1. Simplify the prompt
2. Use a faster model (e.g., `mistral` instead of `llama3`)
3. Check system resources (CPU/RAM)

### Empty Responses
**Symptom:** Response is empty or null

**Solutions:**
1. Check Ollama logs
2. Verify the model exists: `ollama list`
3. Test with a simple prompt first

## Future Enhancements

Potential improvements:
- [ ] Add support for streaming responses
- [ ] Implement response caching for common prompts
- [ ] Add retry logic with exponential backoff
- [ ] Support for multiple Ollama instances (load balancing)
- [ ] Add prompt templates for common use cases
- [ ] Implement rate limiting
- [ ] Add metrics/telemetry for monitoring
