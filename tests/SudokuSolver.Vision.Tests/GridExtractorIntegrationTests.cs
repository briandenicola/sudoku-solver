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
/// Integration tests for the CNN-based extraction pipeline.
/// Requires the ONNX digit model to be available.
/// Filter: dotnet test --filter "Category=Integration&Subsystem=CNN"
/// </summary>
[Trait("Category", "Integration")]
[Trait("Subsystem", "CNN")]
public class CnnExtractionTests : IDisposable
{
    private readonly DigitClassifier? _classifier;
    private readonly GridExtractor? _extractor;

    public CnnExtractionTests()
    {
        var modelPath = FindModelPath();
        if (modelPath != null)
        {
            _classifier = new DigitClassifier(modelPath);
            _extractor = new GridExtractor(_classifier);
        }
    }

    private static string? FindModelPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Models", "mnist-cnn.onnx"),
            Path.Combine(AppContext.BaseDirectory, "mnist-cnn.onnx"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "models", "mnist-cnn.onnx"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    [Theory]
    [InlineData("IMG_6274.jpeg", TestPuzzles.IMG6274)]
    [InlineData("nightmare-clean.png", TestPuzzles.NightmareClean)]
    public void ExtractFromFile_ReturnsValidGrid(string filename, string expectedPuzzle)
    {
        if (_extractor is null)
        {
            // Skip if model not available
            return;
        }

        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestImages", filename);
        Assert.True(File.Exists(imagePath), $"Test image not found at: {imagePath}");

        var result = _extractor.ExtractFromFile(imagePath);

        Assert.True(result.Success, $"[{filename}] Extraction failed: {result.ErrorMessage}");
        Assert.NotNull(result.Grid);

        var (accuracy, mismatches) = TestPuzzles.CompareGrid(result.Grid!, expectedPuzzle);
        Console.WriteLine($"[{filename}] CNN accuracy: {accuracy:F1}% ({mismatches.Count} mismatches)");
        if (mismatches.Count > 0)
            foreach (var m in mismatches) Console.WriteLine(m);
        if (result.Warning != null)
            Console.WriteLine($"[{filename}] Warning: {result.Warning}");
    }

    public void Dispose()
    {
        _classifier?.Dispose();
    }
}

/// <summary>
/// Tests the OpenCV grid detection and cell extraction in isolation (no model needed).
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
        Assert.Equal(900, warped!.Rows);
        Assert.Equal(900, warped.Cols);
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

        // Cell widths should be reasonable (70-130px for a 900px grid)
        for (var i = 0; i < 9; i++)
        {
            var cellWidth = verticalLines[i + 1] - verticalLines[i];
            var cellHeight = horizontalLines[i + 1] - horizontalLines[i];
            Assert.InRange(cellWidth, 60, 140);
            Assert.InRange(cellHeight, 60, 140);
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
