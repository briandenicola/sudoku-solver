using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Pointing Pair/Triple: When a candidate within a box is confined to a single row or column,
/// that candidate can be eliminated from the rest of that row or column outside the box.
/// </summary>
public class PointingPairTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.PointingPair;

    public SolveStep? TryApply(Grid grid)
    {
        for (var box = 0; box < 9; box++)
        {
            var boxCells = grid.Box(box).Where(c => !c.IsSolved).ToList();

            for (var digit = 1; digit <= 9; digit++)
            {
                var cellsWithDigit = boxCells.Where(c => c.Candidates.Contains(digit)).ToList();
                if (cellsWithDigit.Count < 2)
                    continue;

                // Check if all confined to one row
                if (cellsWithDigit.Select(c => c.Row).Distinct().Count() == 1)
                {
                    var row = cellsWithDigit[0].Row;
                    var result = TryEliminate(grid, digit, cellsWithDigit,
                        grid.Row(row).Where(c => !c.IsSolved && c.Box != box),
                        $"box {box + 1}", $"row {row + 1}");
                    if (result != null) return result;
                }

                // Check if all confined to one column
                if (cellsWithDigit.Select(c => c.Column).Distinct().Count() == 1)
                {
                    var col = cellsWithDigit[0].Column;
                    var result = TryEliminate(grid, digit, cellsWithDigit,
                        grid.Column(col).Where(c => !c.IsSolved && c.Box != box),
                        $"box {box + 1}", $"column {col + 1}");
                    if (result != null) return result;
                }
            }
        }

        return null;
    }

    private SolveStep? TryEliminate(Grid grid, int digit, List<Cell> patternCells,
        IEnumerable<Cell> targetCells, string boxName, string lineName)
    {
        var eliminations = new List<Elimination>();
        foreach (var cell in targetCells)
        {
            if (cell.Candidates.Contains(digit))
                eliminations.Add(new Elimination(cell, digit));
        }

        if (eliminations.Count == 0)
            return null;

        foreach (var elim in eliminations)
            elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

        var cellLabels = string.Join(", ", patternCells.Select(c => c.Label));
        var countName = patternCells.Count == 2 ? "Pair" : "Triple";

        return new SolveStep
        {
            Technique = Technique.PointingPair,
            Eliminations = eliminations,
            PatternCells = patternCells,
            AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
            Summary = $"Pointing {countName}: {digit} in {boxName} points along {lineName}",
            Explanation = $"In {boxName}, the digit {digit} can only appear in cells {cellLabels}, " +
                          $"which all lie in {lineName}. Since {digit} must appear somewhere in {boxName}, " +
                          $"and all its possible positions are in {lineName}, we know {digit} in {lineName} " +
                          $"must come from {boxName}. Therefore, {digit} can be eliminated from all other " +
                          $"cells in {lineName} outside {boxName}."
        };
    }
}
