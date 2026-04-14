using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SudokuSolver.Vision;

/// <summary>
/// HTTP client for the Ollama REST API. Supports vision (image) requests.
/// Uses IHttpClientFactory pattern — inject HttpClient via DI.
/// </summary>
public class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;

    public OllamaClient(HttpClient httpClient, OllamaSettings settings)
    {
        _settings = settings;
        _settings.Validate();

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    /// <summary>
    /// Sends a prompt with an optional image to the Ollama generate endpoint.
    /// </summary>
    /// <param name="prompt">The text prompt.</param>
    /// <param name="imageBase64">Optional base64-encoded image data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model's text response.</returns>
    public async Task<string> GenerateAsync(string prompt, string? imageBase64 = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var request = new OllamaGenerateRequest
        {
            Model = _settings.Model,
            Prompt = prompt,
            Stream = false,
            Images = imageBase64 != null ? [imageBase64] : null
        };

        var response = await _httpClient.PostAsJsonAsync("api/generate", request,
            JsonOptions, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
            JsonOptions, cancellationToken).ConfigureAwait(false);

        return result?.Response ?? throw new InvalidOperationException("Empty response from Ollama.");
    }

    /// <summary>
    /// Checks if the Ollama server is reachable and the model is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the list of model names available on the Ollama server.
    /// </summary>
    public async Task<List<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
            JsonOptions, cancellationToken).ConfigureAwait(false);

        return result?.Models?.Select(m => m.Name ?? "").Where(n => n.Length > 0).ToList() ?? [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal class OllamaGenerateRequest
{
    public required string Model { get; set; }
    public required string Prompt { get; set; }
    public bool Stream { get; set; }
    public List<string>? Images { get; set; }
}

internal class OllamaGenerateResponse
{
    public string? Response { get; set; }
}

internal class OllamaTagsResponse
{
    public List<OllamaModelInfo>? Models { get; set; }
}

internal class OllamaModelInfo
{
    public string? Name { get; set; }
}
