using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Box/Line Reduction: When a candidate in a row or column is confined to a single box,
/// that candidate can be eliminated from the rest of the box.
/// This is the inverse of Pointing Pairs.
/// </summary>
public class BoxLineReductionTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.BoxLineReduction;

    public SolveStep? TryApply(Grid grid)
    {
        // Check rows
        for (var row = 0; row < 9; row++)
        {
            var rowCells = grid.Row(row).Where(c => !c.IsSolved).ToList();
            for (var digit = 1; digit <= 9; digit++)
            {
                var cellsWithDigit = rowCells.Where(c => c.Candidates.Contains(digit)).ToList();
                if (cellsWithDigit.Count < 2)
                    continue;

                if (cellsWithDigit.Select(c => c.Box).Distinct().Count() == 1)
                {
                    var box = cellsWithDigit[0].Box;
                    var result = TryEliminate(digit, cellsWithDigit,
                        grid.Box(box).Where(c => !c.IsSolved && c.Row != row),
                        $"row {row + 1}", $"box {box + 1}");
                    if (result != null) return result;
                }
            }
        }

        // Check columns
        for (var col = 0; col < 9; col++)
        {
            var colCells = grid.Column(col).Where(c => !c.IsSolved).ToList();
            for (var digit = 1; digit <= 9; digit++)
            {
                var cellsWithDigit = colCells.Where(c => c.Candidates.Contains(digit)).ToList();
                if (cellsWithDigit.Count < 2)
                    continue;

                if (cellsWithDigit.Select(c => c.Box).Distinct().Count() == 1)
                {
                    var box = cellsWithDigit[0].Box;
                    var result = TryEliminate(digit, cellsWithDigit,
                        grid.Box(box).Where(c => !c.IsSolved && c.Column != col),
                        $"column {col + 1}", $"box {box + 1}");
                    if (result != null) return result;
                }
            }
        }

        return null;
    }

    private SolveStep? TryEliminate(int digit, List<Cell> patternCells,
        IEnumerable<Cell> targetCells, string lineName, string boxName)
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

        return new SolveStep
        {
            Technique = Technique.BoxLineReduction,
            Eliminations = eliminations,
            PatternCells = patternCells,
            AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
            Summary = $"Box/Line Reduction: {digit} in {lineName} confined to {boxName}",
            Explanation = $"In {lineName}, the digit {digit} can only appear in cells {cellLabels}, " +
                          $"which all lie within {boxName}. Since {digit} must appear somewhere in " +
                          $"{lineName}, and all its possible positions fall within {boxName}, the digit " +
                          $"{digit} in {boxName} must come from {lineName}. Therefore, {digit} can be " +
                          $"eliminated from all other cells in {boxName} outside {lineName}."
        };
    }
}
