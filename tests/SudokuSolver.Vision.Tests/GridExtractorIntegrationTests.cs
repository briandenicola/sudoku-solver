using SudokuSolver.Vision;

namespace SudokuSolver.Vision.Tests;

/// <summary>
/// Known puzzle strings for test images (81 digits, 0=empty).
/// </summary>
internal static class TestPuzzles
{
    /// <summary>IMG_6274.jpeg — photo of a printed easy puzzle with perspective distortion.</summary>
    internal const string IMG6274 =
        "216000835" +
        "834010900" +
        "090000002" +
        "000508006" +
        "405620090" +
        "308790201" +
        "000346100" +
        "040100500" +
        "000000670";

    /// <summary>nightmare-clean.png — screenshot of a digital puzzle with no distortion.</summary>
    internal const string NightmareClean =
        "730000601" +
        "502900000" +
        "000070000" +
        "007430506" +
        "800207910" +
        "000100700" +
        "268740195" +
        "070000004" +
        "413000800";

    internal static (double accuracy, List<string> mismatches) CompareGrid(
        SudokuSolver.Engine.Models.Grid grid, string expectedPuzzle)
    {
        var mismatches = new List<string>();
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                var expected = expectedPuzzle[r * 9 + c] - '0';
                var actual = grid[r, c].Value;
                if (expected != actual)
                    mismatches.Add($"  R{r + 1}C{c + 1}: expected {expected}, got {actual}");
            }
        }
        var accuracy = (81 - mismatches.Count) / 81.0 * 100;
        return (accuracy, mismatches);
    }
}

/// <summary>
/// Integration tests that call a real Ollama instance to validate image extraction.
/// These tests require network access and a running Ollama server.
/// Set environment variable OLLAMA_BASE_URL to override the default URL.
/// Filter by trait: dotnet test --filter "Category=Integration&Subsystem=LLM"
///                  dotnet test --filter "Category=Integration&Subsystem=Hybrid"
/// </summary>
[Trait("Category", "Integration")]
[Trait("Subsystem", "LLM")]
public class LlmExtractionTests : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly GridExtractor _extractor;

    public LlmExtractionTests()
    {
        var settings = new OllamaSettings
        {
            BaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
                ?? "https://ai.denicolafamily.com",
            Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL")
                ?? "qwen3-vl:30b",
            TimeoutSeconds = 120
        };

        var client = new OllamaClient(_httpClient, settings);
        _extractor = new GridExtractor(client);
    }

    [Theory]
    [InlineData("IMG_6274.jpeg", TestPuzzles.IMG6274)]
    [InlineData("nightmare-clean.png", TestPuzzles.NightmareClean)]
    public async Task ExtractFromBase64_ReturnsValidGrid(string filename, string expectedPuzzle)
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestImages", filename);
        Assert.True(File.Exists(imagePath), $"Test image not found at: {imagePath}");

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var imageBase64 = Convert.ToBase64String(imageBytes);

        var result = await _extractor.ExtractFromBase64Async(imageBase64);

        Assert.True(result.Success, $"[{filename}] Extraction failed: {result.ErrorMessage}");
        Assert.NotNull(result.Grid);

        var (accuracy, mismatches) = TestPuzzles.CompareGrid(result.Grid!, expectedPuzzle);
        Console.WriteLine($"[{filename}] LLM full-image accuracy: {accuracy:F1}% ({mismatches.Count} mismatches)");
        Console.WriteLine($"[{filename}] Raw response:\n{result.RawResponse}");
        if (mismatches.Count > 0)
            foreach (var m in mismatches) Console.WriteLine(m);
        if (result.Warning != null)
            Console.WriteLine($"[{filename}] Warning: {result.Warning}");

        Assert.Empty(mismatches);
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>
/// Tests the hybrid pipeline: OpenCV grid detection + LLM cell classification.
/// Requires both OpenCV and a running Ollama server.
/// Filter: dotnet test --filter "Category=Integration&Subsystem=Hybrid"
/// </summary>
[Trait("Category", "Integration")]
[Trait("Subsystem", "Hybrid")]
public class HybridExtractionTests : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly GridExtractor _extractor;

    public HybridExtractionTests()
    {
        var settings = new OllamaSettings
        {
            BaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
                ?? "https://ai.denicolafamily.com",
            Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL")
                ?? "qwen3-vl:30b",
            TimeoutSeconds = 120
        };

        var client = new OllamaClient(_httpClient, settings);
        _extractor = new GridExtractor(client);
    }

    [Theory]
    [InlineData("IMG_6274.jpeg", TestPuzzles.IMG6274)]
    [InlineData("nightmare-clean.png", TestPuzzles.NightmareClean)]
    public async Task ExtractFromFile_ReturnsValidGrid(string filename, string expectedPuzzle)
    {
        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestImages", filename);
        Assert.True(File.Exists(imagePath), $"Test image not found at: {imagePath}");

        var result = await _extractor.ExtractFromFileAsync(imagePath);

        Assert.True(result.Success, $"[{filename}] Extraction failed: {result.ErrorMessage}");
        Assert.NotNull(result.Grid);

        var (accuracy, mismatches) = TestPuzzles.CompareGrid(result.Grid!, expectedPuzzle);
        if (mismatches.Count > 0)
        {
            Console.WriteLine($"[{filename}] Hybrid accuracy: {accuracy:F1}% ({mismatches.Count} mismatches):");
            foreach (var m in mismatches) Console.WriteLine(m);
        }

        Assert.Empty(mismatches);
        Assert.Null(result.Warning);
    }

    public void Dispose() => _httpClient.Dispose();
}

/// <summary>
/// Tests the OpenCV grid detection and cell extraction in isolation (no LLM needed).
/// These tests run offline and verify the image processing pipeline.
/// Filter: dotnet test --filter "Subsystem=OpenCV"
/// </summary>
[Trait("Subsystem", "OpenCV")]
public class OpenCVDetectionTests
{
    public static IEnumerable<object[]> TestImages =>
    [
        ["IMG_6274.jpeg"],
        ["nightmare-clean.png"],
    ];

    private static string GetTestImagePath(string filename) =>
        Path.Combine(AppContext.BaseDirectory, "TestImages", filename);

    [Theory]
    [MemberData(nameof(TestImages))]
    public void FindAndWarpGrid_DetectsGrid(string filename)
    {
        var imagePath = GetTestImagePath(filename);
        Assert.True(File.Exists(imagePath), $"Not found: {imagePath}");

        using var warped = OpenCVGridDetector.FindAndWarpGrid(imagePath);

        Assert.NotNull(warped);
        Assert.Equal(450, warped!.Rows);
        Assert.Equal(450, warped.Cols);
    }

    [Theory]
    [MemberData(nameof(TestImages))]
    public void ExtractCells_Returns81Cells(string filename)
    {
        var imagePath = GetTestImagePath(filename);
        using var warped = OpenCVGridDetector.FindAndWarpGrid(imagePath);
        Assert.NotNull(warped);

        var cells = OpenCVGridDetector.ExtractCells(warped!);

        Assert.Equal(9, cells.Length);
        for (var row = 0; row < 9; row++)
        {
            Assert.Equal(9, cells[row].Length);
            for (var col = 0; col < 9; col++)
            {
                Assert.False(cells[row][col].Empty(),
                    $"Cell [{row},{col}] is empty");
                Assert.True(cells[row][col].Rows > 5,
                    $"Cell [{row},{col}] too small: {cells[row][col].Rows}px");
                Assert.True(cells[row][col].Cols > 5,
                    $"Cell [{row},{col}] too small: {cells[row][col].Cols}px");
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestImages))]
    public void DetectGridLines_FindsReasonableLines(string filename)
    {
        var imagePath = GetTestImagePath(filename);
        using var warped = OpenCVGridDetector.FindAndWarpGrid(imagePath);
        Assert.NotNull(warped);

        var verticalLines = OpenCVGridDetector.DetectGridLines(warped!, horizontal: false);
        var horizontalLines = OpenCVGridDetector.DetectGridLines(warped!, horizontal: true);

        // Should find 10 boundary lines in each direction
        Assert.Equal(10, verticalLines.Length);
        Assert.Equal(10, horizontalLines.Length);

        // Lines should be monotonically increasing
        for (var i = 1; i < 10; i++)
        {
            Assert.True(verticalLines[i] > verticalLines[i - 1],
                $"Vertical lines not monotonic at index {i}: {verticalLines[i - 1]} >= {verticalLines[i]}");
            Assert.True(horizontalLines[i] > horizontalLines[i - 1],
                $"Horizontal lines not monotonic at index {i}: {horizontalLines[i - 1]} >= {horizontalLines[i]}");
        }

        // Cell widths should be reasonable (35-65px for a 450px grid)
        for (var i = 0; i < 9; i++)
        {
            var cellWidth = verticalLines[i + 1] - verticalLines[i];
            var cellHeight = horizontalLines[i + 1] - horizontalLines[i];
            Assert.InRange(cellWidth, 30, 70);
            Assert.InRange(cellHeight, 30, 70);
        }

        // Log the detected positions for diagnostics
        Console.WriteLine($"[{filename}] Vertical lines:   {string.Join(", ", verticalLines)}");
        Console.WriteLine($"[{filename}] Horizontal lines: {string.Join(", ", horizontalLines)}");
        Console.WriteLine($"[{filename}] Cell widths:  {string.Join(", ", Enumerable.Range(0, 9).Select(i => verticalLines[i + 1] - verticalLines[i]))}");
        Console.WriteLine($"[{filename}] Cell heights: {string.Join(", ", Enumerable.Range(0, 9).Select(i => horizontalLines[i + 1] - horizontalLines[i]))}");
    }

    [Theory]
    [MemberData(nameof(TestImages))]
    public void ExtractCells_CellsHaveConsistentSizes(string filename)
    {
        var imagePath = GetTestImagePath(filename);
        using var warped = OpenCVGridDetector.FindAndWarpGrid(imagePath);
        Assert.NotNull(warped);

        var cells = OpenCVGridDetector.ExtractCells(warped!);

        // All cells in the same row should have the same height
        for (var row = 0; row < 9; row++)
        {
            var heights = cells[row].Select(c => c.Rows).Distinct().ToList();
            Assert.True(heights.Count <= 2,
                $"Row {row} has inconsistent cell heights: {string.Join(", ", heights)}");
        }

        // All cells in the same column should have the same width
        for (var col = 0; col < 9; col++)
        {
            var widths = Enumerable.Range(0, 9).Select(r => cells[r][col].Cols).Distinct().ToList();
            Assert.True(widths.Count <= 2,
                $"Column {col} has inconsistent cell widths: {string.Join(", ", widths)}");
        }
    }
}
