namespace ReceiptScanner.Application.DTOs;

/// <summary>
/// Request DTO for Ollama API
/// </summary>
public class OllamaRequest
{
    public string Model { get; set; } = "llama3";
    public string Prompt { get; set; } = string.Empty;
    public bool Stream { get; set; } = false;
}

/// <summary>
/// Response DTO from Ollama API
/// </summary>
public class OllamaResponse
{
    public string Model { get; set; } = string.Empty;
    public DateTime Created_At { get; set; }
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
    public string Done_Reason { get; set; } = string.Empty;
    public List<int> Context { get; set; } = new();
    public long Total_Duration { get; set; }
    public long Load_Duration { get; set; }
    public int Prompt_Eval_Count { get; set; }
    public long Prompt_Eval_Duration { get; set; }
    public int Eval_Count { get; set; }
    public long Eval_Duration { get; set; }
}
