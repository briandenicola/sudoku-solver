using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Hidden Single: A candidate digit appears in only one cell within a unit (row, column, or box).
/// Even though that cell may have other candidates, this digit must go here.
/// </summary>
public class HiddenSingleTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.HiddenSingle;

    public SolveStep? TryApply(Grid grid)
    {
        // Check each unit type for hidden singles
        for (var i = 0; i < 9; i++)
        {
            var result = FindInUnit(grid, grid.Row(i).ToList(), UnitType.Row, i)
                      ?? FindInUnit(grid, grid.Column(i).ToList(), UnitType.Column, i)
                      ?? FindInUnit(grid, grid.Box(i).ToList(), UnitType.Box, i);
            if (result != null) return result;
        }

        return null;
    }

    private static SolveStep? FindInUnit(Grid grid, List<Cell> cells, UnitType unitType, int unitIndex)
    {
        for (var digit = 1; digit <= 9; digit++)
        {
            var possibleCells = cells.Where(c => !c.IsSolved && c.Candidates.Contains(digit)).ToList();
            if (possibleCells.Count != 1)
                continue;

            var cell = possibleCells[0];
            // Only report if this cell has multiple candidates (otherwise NakedSingle handles it)
            if (cell.Candidates.Count <= 1)
                continue;

            var unitName = FormatUnitName(unitType, unitIndex);
            var otherCandidates = cell.Candidates.Remove(digit);
            var eliminations = new List<Elimination>();

            // Eliminating other candidates from this cell
            foreach (var other in otherCandidates)
                eliminations.Add(new Elimination(cell, other));

            // Eliminating this digit from peers
            foreach (var peer in grid.Peers(cell))
            {
                if (!peer.IsSolved && peer.Candidates.Contains(digit))
                    eliminations.Add(new Elimination(peer, digit));
            }

            grid.PlaceValue(cell, digit);

            return new SolveStep
            {
                Technique = Technique.HiddenSingle,
                SolvedCell = cell,
                PlacedValue = digit,
                Eliminations = eliminations,
                PatternCells = [cell],
                AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
                Summary = $"Hidden Single: {cell.Label} = {digit} (only place in {unitName})",
                Explanation = $"In {unitName}, the digit {digit} can only go in one cell: {cell.Label}. " +
                              $"Looking at the other cells in this {unitType.ToString().ToLowerInvariant()}, " +
                              $"they either already contain a value or have had {digit} eliminated from their " +
                              $"candidates. Since every {unitType.ToString().ToLowerInvariant()} must contain " +
                              $"each digit exactly once, {digit} must be placed at {cell.Label}."
            };
        }

        return null;
    }

    private static string FormatUnitName(UnitType unitType, int index) => unitType switch
    {
        UnitType.Row => $"row {index + 1}",
        UnitType.Column => $"column {index + 1}",
        UnitType.Box => $"box {index + 1}",
        _ => throw new ArgumentOutOfRangeException(nameof(unitType))
    };
}
