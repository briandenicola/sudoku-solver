using System.Net.Http;

namespace SudokuSolver.Vision;

/// <summary>
/// Creates OllamaClient instances with properly configured HttpClients.
/// Used to decouple HttpClient creation from the calling code.
/// </summary>
public interface IOllamaClientFactory
{
    OllamaClient CreateClient(string baseUrl, string model, int timeoutSeconds);
}

/// <summary>
/// Default implementation that creates OllamaClient instances.
/// </summary>
public class OllamaClientFactory : IOllamaClientFactory
{
    public OllamaClient CreateClient(string baseUrl, string model, int timeoutSeconds)
    {
        var settings = new OllamaSettings
        {
            BaseUrl = baseUrl,
            Model = model,
            TimeoutSeconds = timeoutSeconds
        };
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        return new OllamaClient(httpClient, settings);
    }
}