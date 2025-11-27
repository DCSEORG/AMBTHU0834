namespace ExpenseManagement.Models;

public class GenAISettings
{
    public string? Endpoint { get; set; }
    public string? DeploymentName { get; set; }
    public string? SearchEndpoint { get; set; }
    public string? SearchIndexName { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage>? History { get; set; }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
