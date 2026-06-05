using OpenCvSharp;
using SudokuSolver.Engine.Models;

namespace SudokuSolver.Vision;

/// <summary>
/// Extracts a sudoku grid from an image using OpenCV for grid detection
/// and a local ONNX CNN model for digit classification.
/// No LLM/network dependency required for extraction.
/// </summary>
public class GridExtractor
{
    private readonly DigitClassifier _classifier;

    /// <summary>
    /// Creates a GridExtractor using a local CNN digit classifier.
    /// </summary>
    public GridExtractor(DigitClassifier classifier)
    {
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
    }

    /// <summary>
    /// Extracts a sudoku grid from an image file.
    /// Uses OpenCV for grid detection and CNN for digit classification.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted grid.</returns>
    public Task<GridExtractionResult> ExtractFromFileAsync(string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        // Run extraction on a background thread since OpenCV + CNN are CPU-bound
        return Task.Run(() => ExtractFromFile(imagePath), cancellationToken);
    }

    /// <summary>
    /// Synchronous extraction from an image file.
    /// </summary>
    public GridExtractionResult ExtractFromFile(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        try
        {
            Mat? warpedGrid = null;
            try
            {
                warpedGrid = OpenCVGridDetector.FindAndWarpGrid(imagePath);
            }
            catch (Exception ex)
            {
                return GridExtractionResult.Failed(
                    $"Grid detection failed: {ex.Message}. " +
                    "Ensure the image contains a clearly visible sudoku grid.",
                    string.Empty);
            }

            if (warpedGrid == null)
            {
                return GridExtractionResult.Failed(
                    "Could not detect a sudoku grid in the image. " +
                    "Ensure the puzzle is clearly visible with good contrast and minimal obstructions.",
                    string.Empty);
            }

            try
            {
                var cells = OpenCVGridDetector.ExtractCells(warpedGrid);
                return ClassifyCells(cells);
            }
            finally
            {
                warpedGrid.Dispose();
            }
        }
        catch (Exception ex)
        {
            return GridExtractionResult.Failed(
                $"Unexpected error during extraction: {ex.GetType().Name}: {ex.Message}",
                string.Empty);
        }
    }

    /// <summary>
    /// Classifies individual cells using the CNN digit classifier.
    /// </summary>
    private GridExtractionResult ClassifyCells(Mat[][] cells)
    {
        var values = new int[81];
        var diagnostics = new List<string>(81);
        var lowConfidenceCells = new List<string>();

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                var cell = cells[row][col];
                var preprocessed = CellExtractor.PreprocessCell(cell);

                if (!CellExtractor.HasDigit(preprocessed))
                {
                    values[row * 9 + col] = 0;
                    diagnostics.Add($"R{row + 1}C{col + 1}: empty");
                    continue;
                }

                var (digit, confidence) = _classifier.Classify(preprocessed);
                values[row * 9 + col] = digit;
                diagnostics.Add($"R{row + 1}C{col + 1}: {digit} ({confidence:P0})");

                if (digit > 0 && confidence < 0.8f)
                {
                    lowConfidenceCells.Add($"R{row + 1}C{col + 1}={digit} ({confidence:P0})");
                }
            }
        }

        var rawResponse = string.Join("\n", diagnostics);

        try
        {
            var grid = Grid.FromValues(values);

            var conflict = FindConflict(values);
            if (conflict is not null)
            {
                return GridExtractionResult.SucceededWithWarning(
                    grid,
                    $"The extracted puzzle has a conflict ({conflict}). " +
                    "The digit classifier may have misread some cells. " +
                    "Use Manual Entry to correct any misread cells.",
                    rawResponse);
            }

            if (lowConfidenceCells.Count > 0)
            {
                return GridExtractionResult.SucceededWithWarning(
                    grid,
                    $"Low confidence on {lowConfidenceCells.Count} cell(s): " +
                    string.Join(", ", lowConfidenceCells.Take(5)) +
                    (lowConfidenceCells.Count > 5 ? "..." : "") +
                    ". Please verify these cells are correct.",
                    rawResponse);
            }

            return GridExtractionResult.Succeeded(grid, rawResponse);
        }
        catch (Exception ex)
        {
            return GridExtractionResult.Failed(
                $"Classified digits but failed to create grid: {ex.Message}", rawResponse);
        }
    }

    internal static string? FindConflict(int[] values)
    {
        for (var i = 0; i < 9; i++)
        {
            if (HasDuplicate(values, RowIndices(i), out var dup))
                return $"row {i + 1} has duplicate {dup}";
            if (HasDuplicate(values, ColumnIndices(i), out dup))
                return $"column {i + 1} has duplicate {dup}";
            if (HasDuplicate(values, BoxIndices(i), out dup))
                return $"box {i + 1} has duplicate {dup}";
        }
        return null;
    }

    private static bool HasDuplicate(int[] values, IEnumerable<int> indices, out int duplicate)
    {
        var seen = 0;
        foreach (var idx in indices)
        {
            var v = values[idx];
            if (v == 0) continue;
            var bit = 1 << v;
            if ((seen & bit) != 0)
            {
                duplicate = v;
                return true;
            }
            seen |= bit;
        }
        duplicate = 0;
        return false;
    }

    private static IEnumerable<int> RowIndices(int row)
    {
        for (var c = 0; c < 9; c++) yield return row * 9 + c;
    }

    private static IEnumerable<int> ColumnIndices(int col)
    {
        for (var r = 0; r < 9; r++) yield return r * 9 + col;
    }

    private static IEnumerable<int> BoxIndices(int box)
    {
        var startRow = (box / 3) * 3;
        var startCol = (box % 3) * 3;
        for (var r = startRow; r < startRow + 3; r++)
            for (var c = startCol; c < startCol + 3; c++)
                yield return r * 9 + c;
    }
}

public class GridExtractionResult
{
    public bool Success { get; private init; }
    public Grid? Grid { get; private init; }
    public string? ErrorMessage { get; private init; }
    public string? Warning { get; private init; }
    public string RawResponse { get; private init; } = "";

    public static GridExtractionResult Succeeded(Grid grid, string rawResponse) => new()
    {
        Success = true,
        Grid = grid,
        RawResponse = rawResponse
    };

    public static GridExtractionResult SucceededWithWarning(Grid grid, string warning, string rawResponse) => new()
    {
        Success = true,
        Grid = grid,
        Warning = string.IsNullOrWhiteSpace(warning) ? "Possible extraction issues." : warning,
        RawResponse = rawResponse
    };

    public static GridExtractionResult Failed(string error, string rawResponse) => new()
    {
        Success = false,
        ErrorMessage = string.IsNullOrWhiteSpace(error) ? "Unknown error occurred." : error,
        RawResponse = rawResponse
    };
}