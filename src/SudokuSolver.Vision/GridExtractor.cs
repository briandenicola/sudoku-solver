using System.Text.RegularExpressions;
using OpenCvSharp;
using SudokuSolver.Engine.Models;

namespace SudokuSolver.Vision;

/// <summary>
/// Extracts a sudoku grid from an image using a hybrid approach:
/// 1. OpenCV for robust grid detection and perspective correction
/// 2. Individual cell extraction and LLM classification for digits
/// 3. Falls back to legacy full-image LLM method if OpenCV fails
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

    private const string CellDigitPrompt = "What digit (0-9) is written in this image? " +
        "Reply with ONLY a single digit, no other text. Use 0 for empty cells.";

    /// <summary>
    /// Extracts a sudoku grid from an image file using the hybrid approach.
    /// </summary>
    /// <param name="imagePath">Path to the image file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted grid.</returns>
    public async Task<GridExtractionResult> ExtractFromFileAsync(string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        try
        {
            // Try OpenCV approach first
            Mat? warpedGrid = null;
            try
            {
                warpedGrid = OpenCVGridDetector.FindAndWarpGrid(imagePath);
            }
            catch (Exception ex)
            {
                // OpenCV failed - log and fall back to full image
                System.Diagnostics.Debug.WriteLine($"OpenCV grid detection failed: {ex.Message}");
            }

            GridExtractionResult? result = null;

            if (warpedGrid != null)
            {
                try
                {
                    // OpenCV found the grid - extract cells and classify each
                    var cells = OpenCVGridDetector.ExtractCells(warpedGrid);
                    result = await ExtractUsingCellClassification(cells, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Cell extraction failed - fall back to full image
                    System.Diagnostics.Debug.WriteLine($"Cell extraction failed: {ex.Message}");
                }
                finally
                {
                    warpedGrid.Dispose();
                }
            }

            if (result is not null && !result.Success)
            {
                // Hybrid extraction failed, but we still want to try fallback
                result = null;
            }

            if (result is null)
            {
                // Fallback: send full image to LLM
                var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
                var imageBase64 = Convert.ToBase64String(imageBytes);
                result = await ExtractFromBase64Async(imageBase64, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (Exception ex)
        {
            // Any unexpected exception - return a clear error
            return GridExtractionResult.Failed(
                $"Unexpected error during extraction: {ex.GetType().Name}: {ex.Message}",
                string.Empty);
        }
    }

    /// <summary>
    /// Extracts a sudoku grid from a base64-encoded image using legacy LLM method.
    /// This is used as a fallback when OpenCV cannot find the grid.
    /// </summary>
    public async Task<GridExtractionResult> ExtractFromBase64Async(string imageBase64,
        CancellationToken cancellationToken = default)
    {
        var prompt = !string.IsNullOrWhiteSpace(_customPrompt) ? _customPrompt : DefaultPrompt;
        var rawResponse = await _client.GenerateAsync(prompt, imageBase64, cancellationToken)
            .ConfigureAwait(false);

        return ParseResponse(rawResponse);
    }

    /// <summary>
    /// Uses LLM to classify individual preprocessed cells.
    /// This is the hybrid approach that provides better accuracy.
    /// </summary>
    private async Task<GridExtractionResult> ExtractUsingCellClassification(
        Mat[][] cells, CancellationToken cancellationToken)
    {
        var values = new int[81];
        var responses = new List<string>(81);

        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 9; col++)
            {
                try
                {
                    var cell = cells[row][col];
                    var preprocessed = CellExtractor.PreprocessCell(cell);
                    var cellBase64 = CellExtractor.MatToBase64(preprocessed);

                    // Ask LLM to classify this single cell
                    var prompt = CellDigitPrompt;
                    var response = await _client.GenerateAsync(prompt, cellBase64, cancellationToken)
                        .ConfigureAwait(false);

                    var digit = ParseSingleDigit(response ?? string.Empty);
                    values[row * 9 + col] = digit;
                    responses.Add(response ?? string.Empty);
                }
                catch (Exception ex)
                {
                    // If any cell classification fails, use 0 and continue
                    values[row * 9 + col] = 0;
                    responses.Add($"Error: {ex.Message}");
                }
            }
        }

        // Parse and validate the extracted values
        var rawResponse = string.Join("\n", responses);
        if (!TryExtractValues(values, out var finalValues, out var parseError))
        {
            var effectiveError = string.IsNullOrWhiteSpace(parseError)
                ? "Failed to validate extracted values."
                : parseError;
            return GridExtractionResult.Failed(effectiveError, rawResponse);
        }

        try
        {
            var grid = Grid.FromValues(finalValues);

            var conflict = FindConflict(finalValues);
            if (conflict is not null)
            {
                return GridExtractionResult.SucceededWithWarning(
                    grid,
                    $"The extracted puzzle is not a valid sudoku ({conflict}). " +
                    "The vision model likely misread some cells. " +
                    "Please clear out any candidate/pencil marks from the puzzle and upload the image again, " +
                    "switch to a different vision model, or use Manual Entry to correct the misread cells.",
                    rawResponse);
            }

            return GridExtractionResult.Succeeded(grid, rawResponse);
        }
        catch (Exception ex)
        {
            return GridExtractionResult.Failed(
                $"Parsed digits but failed to create grid: {ex.Message}", rawResponse);
        }
    }

    /// <summary>
    /// Parses a single digit from LLM response for a cell.
    /// </summary>
    private static int ParseSingleDigit(string response)
    {
        var digitMatch = System.Text.RegularExpressions.Regex.Match(
            response, @"\d", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (digitMatch.Success)
        {
            var digit = digitMatch.Value[0] - '0';
            if (digit is >= 0 and <= 9)
                return digit;
        }
        return 0; // Empty cell
    }

    private static bool TryExtractValues(int[] initialValues, out int[] values, out string error)
    {
        values = initialValues;
        error = "";
        
        // Check for conflicts (duplicates in row/column/box)
        var conflict = FindConflict(initialValues);
        if (conflict is not null)
        {
            // Return with warning - the conflict check is done in ExtractUsingCellClassification
            error = conflict;
        }

        return true;
    }

    internal static GridExtractionResult ParseResponse(string response)
    {
        if (!TryExtractValuesLegacy(response, out var values, out var parseError))
        {
            var excerpt = Excerpt(response, 800);
            return GridExtractionResult.Failed(
                $"{parseError}\n\n--- Raw model response (first 800 chars) ---\n{excerpt}",
                response);
        }

        Grid grid;
        try
        {
            grid = Grid.FromValues(values!);
        }
        catch (Exception ex)
        {
            return GridExtractionResult.Failed($"Parsed digits but failed to create grid: {ex.Message}", response);
        }

        var conflict = FindConflict(values!);
        if (conflict is not null)
        {
            return GridExtractionResult.SucceededWithWarning(
                grid,
                $"The extracted puzzle is not a valid sudoku ({conflict}). " +
                "The vision model likely misread some cells. " +
                "Please clear out any candidate/pencil marks from the puzzle and upload the image again, " +
                "switch to a different vision model, or use Manual Entry to correct the misread cells.",
                response);
        }

        return GridExtractionResult.Succeeded(grid, response);
    }

    private static bool TryExtractValuesLegacy(string response, out int[]? values, out string error)
    {
        values = null;

        // Strategy 1: 9 lines that each contain 9 digits (current/strict format).
        var lines = DigitLineRegex().Matches(response);
        if (lines.Count >= 9)
        {
            var v = new int[81];
            for (var row = 0; row < 9; row++)
            {
                var digits = lines[row].Value.Where(char.IsDigit).ToArray();
                for (var col = 0; col < 9; col++)
                    v[row * 9 + col] = digits[col] - '0';
            }
            values = v;
            error = "";
            return true;
        }

        // Strategy 2: markdown / pipe table — split by lines, drop separator rows, keep
        // any line with at least 9 non-empty cells (digit or blank between pipes).
        var tableValues = TryParsePipeTable(response);
        if (tableValues is not null)
        {
            values = tableValues;
            error = "";
            return true;
        }

        // Strategy 3: any 81 digits in the response, in order. Common with OCR models
        // that don't preserve row structure or dump everything on one line.
        var allDigits = response.Where(char.IsDigit).ToArray();
        if (allDigits.Length == 81)
        {
            var v = new int[81];
            for (var i = 0; i < 81; i++) v[i] = allDigits[i] - '0';
            values = v;
            error = "";
            return true;
        }

        error = $"Could not parse a 9x9 grid from response. " +
            $"Strict 9-rows-of-9 match found {lines.Count} matching lines; " +
            $"total digits in response: {allDigits.Length} (need exactly 81).";
        return false;
    }

    private static int[]? TryParsePipeTable(string response)
    {
        // Pipe-table row example: `| 5 | 3 |   |   | 7 |   |   |   |   |`
        var rows = new List<int[]>();
        foreach (var rawLine in response.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.Contains('|')) continue;

            // Skip markdown separator rows like |---|---|...
            if (line.Replace("|", "").Replace("-", "").Replace(":", "").Replace(" ", "") == "")
                continue;

            var cells = line.Trim('|').Split('|');
            if (cells.Length < 9) continue;

            var rowValues = new int[9];
            var ok = true;
            for (var c = 0; c < 9; c++)
            {
                var cell = cells[c].Trim();
                if (cell.Length == 0 || cell == "_" || cell == ".")
                {
                    rowValues[c] = 0;
                }
                else if (cell.Length == 1 && char.IsDigit(cell[0]))
                {
                    rowValues[c] = cell[0] - '0';
                }
                else
                {
                    ok = false;
                    break;
                }
            }
            if (ok) rows.Add(rowValues);
            if (rows.Count == 9) break;
        }

        if (rows.Count != 9) return null;

        var v = new int[81];
        for (var r = 0; r < 9; r++)
            for (var c = 0; c < 9; c++)
                v[r * 9 + c] = rows[r][c];
        return v;
    }

    private static string Excerpt(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "(empty)";
        return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
    }

    private static string? FindConflict(int[] values)
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

    public const string DefaultPrompt = """
        Read the digits from this sudoku puzzle image.

        Output EXACTLY 9 lines, each containing EXACTLY 9 digits separated by spaces.
        Use 0 for empty cells. Small candidate/pencil marks in cell corners are NOT digits — treat those cells as empty (0).
        Do not solve the puzzle. Only report what is printed as a large digit in the cell.

        Read left-to-right, top-to-bottom from the top-left corner.
        Output ONLY the 9 lines of digits — no prose, no explanation, no code fences.
        """;

    [GeneratedRegex(@"[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9][\s,]*[0-9]")]
    private static partial Regex DigitLineRegex();
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