using SudokuSolver.Vision;

namespace SudokuSolver.Vision.Tests;

/// <summary>
/// Integration tests that call a real Ollama instance to validate image extraction.
/// These tests require network access and a running Ollama server.
/// Set environment variable OLLAMA_BASE_URL to override the default URL.
/// </summary>
[Trait("Category", "Integration")]
public class GridExtractorIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly OllamaSettings _settings;
    private readonly OllamaClient _client;
    private readonly GridExtractor _extractor;

    public GridExtractorIntegrationTests()
    {
        _settings = new OllamaSettings
        {
            BaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
                ?? "https://ai.denicolafamily.com",
            Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL")
                ?? "qwen3-vl:30b",
            TimeoutSeconds = 120
        };

        _client = new OllamaClient(_httpClient, _settings);
        _extractor = new GridExtractor(_client);
    }

    // The known correct puzzle from IMG_6274.jpeg (81 digits, 0=empty)
    private const string ExpectedPuzzle =
        "216000835" +
        "834010900" +
        "090000002" +
        "000508006" +
        "405620090" +
        "308790201" +
        "000346100" +
        "040100500" +
        "000000670";

    [Fact]
    public async Task ExtractFromFile_IMG6274_ReturnsValidGrid()
    {
        // Arrange
        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestImages", "IMG_6274.jpeg");
        Assert.True(File.Exists(imagePath), $"Test image not found at: {imagePath}");

        // Act
        var result = await _extractor.ExtractFromFileAsync(imagePath);

        // Assert — extraction must succeed
        Assert.True(result.Success, $"Extraction failed: {result.ErrorMessage}");
        Assert.NotNull(result.Grid);

        // Compare against known puzzle and report accuracy
        var grid = result.Grid!;
        var mismatches = new List<string>();
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                var expected = ExpectedPuzzle[r * 9 + c] - '0';
                var actual = grid[r, c].Value;
                if (expected != actual)
                    mismatches.Add($"  R{r + 1}C{c + 1}: expected {expected}, got {actual}");
            }
        }

        if (mismatches.Count > 0)
        {
            var accuracy = (81 - mismatches.Count) / 81.0 * 100;
            Console.WriteLine($"Extraction accuracy: {accuracy:F1}% ({mismatches.Count} mismatches):");
            foreach (var m in mismatches) Console.WriteLine(m);
        }

        // The extraction must be perfect (0 mismatches)
        Assert.Empty(mismatches);

        // No conflict warning expected for a valid puzzle
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task ExtractFromBase64_IMG6274_ReturnsValidGrid()
    {
        // Tests the legacy (full-image LLM) path directly
        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestImages", "IMG_6274.jpeg");
        Assert.True(File.Exists(imagePath), $"Test image not found at: {imagePath}");

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var imageBase64 = Convert.ToBase64String(imageBytes);

        var result = await _extractor.ExtractFromBase64Async(imageBase64);

        Assert.True(result.Success, $"Extraction failed: {result.ErrorMessage}");
        Assert.NotNull(result.Grid);

        // Compare against known puzzle
        var grid = result.Grid!;
        var mismatches = new List<string>();
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                var expected = ExpectedPuzzle[r * 9 + c] - '0';
                var actual = grid[r, c].Value;
                if (expected != actual)
                    mismatches.Add($"  R{r + 1}C{c + 1}: expected {expected}, got {actual}");
            }
        }

        var accuracy = (81 - mismatches.Count) / 81.0 * 100;
        Console.WriteLine($"LLM full-image accuracy: {accuracy:F1}% ({mismatches.Count} mismatches)");
        Console.WriteLine($"Raw response:\n{result.RawResponse}");
        if (mismatches.Count > 0)
            foreach (var m in mismatches) Console.WriteLine(m);
        if (result.Warning != null)
            Console.WriteLine($"Warning: {result.Warning}");

        // The extraction must be perfect (0 mismatches)
        Assert.Empty(mismatches);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
