using System.Text.RegularExpressions;
using SudokuSolver.Engine.Models;

namespace SudokuSolver.Vision;

/// <summary>
/// Extracts a sudoku grid from an image using an Ollama vision model.
/// Sends the image with a carefully crafted prompt and parses the response into a Grid.
/// </summary>
public partial class GridExtractor
{
    private readonly OllamaClient _client;

    public GridExtractor(OllamaClient client, string? customPrompt = null)
    {
        _client = client;
        _customPrompt = customPrompt;
    }

    private readonly string? _customPrompt;

    /// <summary>
    /// Extracts a sudoku grid from an image file.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted grid.</returns>
    public async Task<GridExtractionResult> ExtractFromFileAsync(string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken).ConfigureAwait(false);
        var imageBase64 = Convert.ToBase64String(imageBytes);

        return await ExtractFromBase64Async(imageBase64, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts a sudoku grid from a base64-encoded image.
    /// </summary>
    public async Task<GridExtractionResult> ExtractFromBase64Async(string imageBase64,
        CancellationToken cancellationToken = default)
    {
        var prompt = !string.IsNullOrWhiteSpace(_customPrompt) ? _customPrompt : DefaultPrompt;
        var rawResponse = await _client.GenerateAsync(prompt, imageBase64, cancellationToken)
            .ConfigureAwait(false);

        return ParseResponse(rawResponse);
    }

    internal static GridExtractionResult ParseResponse(string response)
    {
        // Look for 9 lines of 9 digits each
        var lines = DigitLineRegex().Matches(response);

        if (lines.Count < 9)
            return GridExtractionResult.Failed(
                $"Could not parse grid from response. Expected 9 rows of 9 digits. Found {lines.Count} matching lines.",
                response);

        var values = new int[81];
        for (var row = 0; row < 9; row++)
        {
            var digits = lines[row].Value.Where(char.IsDigit).ToArray();
            for (var col = 0; col < 9; col++)
                values[row * 9 + col] = digits[col] - '0';
        }

        try
        {
            var grid = Grid.FromValues(values);
            return GridExtractionResult.Succeeded(grid, response);
        }
        catch (Exception ex)
        {
            return GridExtractionResult.Failed($"Parsed digits but failed to create grid: {ex.Message}", response);
        }
    }

    public const string DefaultPrompt = """
        Analyze this sudoku puzzle image. Extract all digits from the 9x9 grid.
        
        Output EXACTLY 9 lines, each containing EXACTLY 9 digits separated by spaces.
        Use 0 for empty cells.
        
        Start from the top-left corner, read left to right, top to bottom.
        Output ONLY the 9 lines of digits, nothing else.
        """;

    [GeneratedRegex(@"[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9]")]
    private static partial Regex DigitLineRegex();
}

public class GridExtractionResult
{
    public bool Success { get; private init; }
    public Grid? Grid { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string RawResponse { get; private init; } = "";

    public static GridExtractionResult Succeeded(Grid grid, string rawResponse) => new()
    {
        Success = true,
        Grid = grid,
        RawResponse = rawResponse
    };

    public static GridExtractionResult Failed(string error, string rawResponse) => new()
    {
        Success = false,
        ErrorMessage = error,
        RawResponse = rawResponse
    };
}
