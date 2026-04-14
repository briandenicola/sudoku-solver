using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Backtracking solver as a last resort. Uses depth-first search with constraint propagation
/// to find the solution when all logical techniques have been exhausted.
///
/// Each cell solved by backtracking produces a step explaining the trial-and-error process.
/// The technique finds all remaining values at once, then produces one step per placement.
/// </summary>
public class BacktrackingTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.Backtracking;

    public SolveStep? TryApply(Grid grid)
    {
        // Snapshot the current state so we can find just the next cell to place
        var unsolved = grid.AllCells().Where(c => !c.IsSolved).ToList();
        if (unsolved.Count == 0) return null;

        // Build a value array for backtracking
        var values = new int[9, 9];
        foreach (var cell in grid.AllCells())
            values[cell.Row, cell.Column] = cell.Value;

        if (!SolveBacktrack(values))
            return null;

        // Find the first unsolved cell and place its value
        var target = unsolved
            .OrderBy(c => c.Candidates.Count) // prefer cells with fewer candidates (more constrained)
            .First();

        var value = values[target.Row, target.Column];
        if (value == 0) return null;

        grid.PlaceValue(target, value);

        var remainingCount = unsolved.Count;
        return new SolveStep
        {
            Technique = Technique,
            SolvedCell = target,
            PlacedValue = value,
            PatternCells = [target],
            AffectedCells = [target],
            Summary = $"Backtracking: {target.Label} = {value} ({remainingCount} cells remaining)",
            Explanation = $"All logical techniques have been exhausted. Using backtracking (trial and error) to continue. " +
                $"The cell {target.Label} had candidates {{{string.Join(",", target.Candidates.Union(CandidateSet.Of(value)))}}}. " +
                $"By systematically trying each candidate and checking if it leads to a valid solution, " +
                $"we determined that {target.Label} must be {value}. " +
                $"While backtracking doesn't use elegant logic, it guarantees finding the correct answer. " +
                $"This puzzle may require techniques beyond the ones currently implemented in the solver " +
                $"(such as chains, ALSs, or other advanced strategies)."
        };
    }

    private static bool SolveBacktrack(int[,] values)
    {
        // Find the next empty cell with the fewest possibilities (MRV heuristic)
        int bestRow = -1, bestCol = -1, bestCount = 10;
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                if (values[r, c] != 0) continue;
                var count = CountPossible(values, r, c);
                if (count < bestCount)
                {
                    bestRow = r;
                    bestCol = c;
                    bestCount = count;
                }
            }
        }

        if (bestRow == -1) return true; // All cells filled
        if (bestCount == 0) return false; // Dead end

        for (var digit = 1; digit <= 9; digit++)
        {
            if (!IsValid(values, bestRow, bestCol, digit)) continue;

            values[bestRow, bestCol] = digit;
            if (SolveBacktrack(values))
                return true;
            values[bestRow, bestCol] = 0;
        }

        return false;
    }

    private static int CountPossible(int[,] values, int row, int col)
    {
        var count = 0;
        for (var d = 1; d <= 9; d++)
        {
            if (IsValid(values, row, col, d))
                count++;
        }
        return count;
    }

    private static bool IsValid(int[,] values, int row, int col, int digit)
    {
        // Check row
        for (var c = 0; c < 9; c++)
            if (values[row, c] == digit) return false;

        // Check column
        for (var r = 0; r < 9; r++)
            if (values[r, col] == digit) return false;

        // Check box
        var boxRow = (row / 3) * 3;
        var boxCol = (col / 3) * 3;
        for (var r = boxRow; r < boxRow + 3; r++)
            for (var c = boxCol; c < boxCol + 3; c++)
                if (values[r, c] == digit) return false;

        return true;
    }
}
