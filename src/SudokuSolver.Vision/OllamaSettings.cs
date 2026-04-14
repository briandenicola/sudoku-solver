namespace SudokuSolver.Vision;

/// <summary>
/// Configuration for the Ollama API connection.
/// </summary>
public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "gemma4";

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Validates that settings are reasonable before use.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException("Ollama base URL is required.");
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new InvalidOperationException($"Invalid Ollama base URL: {BaseUrl}. Must be an HTTP(S) URL.");
        if (string.IsNullOrWhiteSpace(Model))
            throw new InvalidOperationException("Ollama model name is required.");
        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("Timeout must be positive.");
    }
}
