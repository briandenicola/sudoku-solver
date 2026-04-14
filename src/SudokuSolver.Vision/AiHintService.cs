using SudokuSolver.Engine.Models;

namespace SudokuSolver.Vision;

/// <summary>
/// Uses an Ollama language model to suggest the next solving step when
/// deterministic techniques are exhausted. The AI analyzes the current
/// grid state and candidate information to identify a move.
/// </summary>
public class AiHintService
{
    private readonly OllamaClient _client;

    public AiHintService(OllamaClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Asks the AI to identify the next cell to solve and which value to place.
    /// Returns a SolveStep if the AI provides a valid suggestion, null otherwise.
    /// </summary>
    public async Task<SolveStep?> GetHintAsync(Grid grid, CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(grid);

        try
        {
            var response = await _client.GenerateAsync(prompt, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return ParseAiResponse(grid, response);
        }
        catch
        {
            return null;
        }
    }

    internal static string BuildPrompt(Grid grid)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("You are a sudoku solving expert. Analyze this puzzle and find the next cell to solve.");
        lines.AppendLine();
        lines.AppendLine("Current grid (0 = empty):");
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                lines.Append(grid[r, c].Value);
                if (c < 8) lines.Append(' ');
            }
            lines.AppendLine();
        }

        lines.AppendLine();
        lines.AppendLine("Unsolved cells and their remaining candidates:");
        foreach (var cell in grid.AllCells().Where(c => !c.IsSolved))
        {
            lines.AppendLine($"  {cell.Label}: {cell.Candidates}");
        }

        lines.AppendLine();
        lines.AppendLine("Find the next cell that can be solved. Use advanced techniques like:");
        lines.AppendLine("- Chains (X-Chain, XY-Chain, AIC)");
        lines.AppendLine("- Almost Locked Sets (ALS)");
        lines.AppendLine("- Sue de Coq");
        lines.AppendLine("- Finned/Sashimi Fish");
        lines.AppendLine("- Any other valid sudoku technique");
        lines.AppendLine();
        lines.AppendLine("Respond in EXACTLY this format (3 lines only):");
        lines.AppendLine("CELL: R<row>C<col>");
        lines.AppendLine("VALUE: <digit>");
        lines.AppendLine("REASON: <brief explanation of the technique and reasoning>");

        return lines.ToString();
    }

    internal static SolveStep? ParseAiResponse(Grid grid, string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        // Parse CELL: R<row>C<col>
        var cellMatch = System.Text.RegularExpressions.Regex.Match(
            response, @"CELL:\s*R(\d)C(\d)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!cellMatch.Success) return null;

        var row = int.Parse(cellMatch.Groups[1].Value) - 1;
        var col = int.Parse(cellMatch.Groups[2].Value) - 1;
        if (row is < 0 or > 8 || col is < 0 or > 8) return null;

        // Parse VALUE: <digit>
        var valueMatch = System.Text.RegularExpressions.Regex.Match(
            response, @"VALUE:\s*(\d)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!valueMatch.Success) return null;

        var value = int.Parse(valueMatch.Groups[1].Value);
        if (value is < 1 or > 9) return null;

        // Parse REASON
        var reasonMatch = System.Text.RegularExpressions.Regex.Match(
            response, @"REASON:\s*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var reason = reasonMatch.Success ? reasonMatch.Groups[1].Value.Trim() : "AI-suggested move.";

        // Validate: cell must be unsolved and value must be a candidate
        var cell = grid[row, col];
        if (cell.IsSolved) return null;
        if (!cell.Candidates.Contains(value)) return null;

        // Validate: placing this value doesn't conflict with peers
        foreach (var peer in grid.Peers(cell))
        {
            if (peer.Value == value) return null;
        }

        grid.PlaceValue(cell, value);

        return new SolveStep
        {
            Technique = Technique.AiAssisted,
            SolvedCell = cell,
            PlacedValue = value,
            PatternCells = [cell],
            AffectedCells = [cell],
            Summary = $"AI-Assisted: {cell.Label} = {value}",
            Explanation = $"The deterministic solver techniques were unable to make further progress. " +
                $"An AI model was consulted to find the next move.\n\n" +
                $"AI reasoning: {reason}\n\n" +
                $"Note: AI suggestions are verified to be valid (the value is a legal candidate " +
                $"and doesn't conflict with any peer cells), but the explanation may not always " +
                $"reflect the most efficient technique available."
        };
    }
}
