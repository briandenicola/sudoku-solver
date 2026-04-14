using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Naked Single: A cell has only one candidate remaining, so it must be that value.
/// This is the most basic solving technique.
/// </summary>
public class NakedSingleTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.NakedSingle;

    public SolveStep? TryApply(Grid grid)
    {
        foreach (var cell in grid.AllCells())
        {
            if (cell.IsSolved || cell.Candidates.Count != 1)
                continue;

            var value = cell.Candidates.Single();
            var eliminations = CollectPeerEliminations(grid, cell, value);
            grid.PlaceValue(cell, value);

            return new SolveStep
            {
                Technique = Technique.NakedSingle,
                SolvedCell = cell,
                PlacedValue = value,
                Eliminations = eliminations,
                PatternCells = [cell],
                AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
                Summary = $"Naked Single: {cell.Label} = {value}",
                Explanation = $"Cell {cell.Label} has only one candidate remaining: {value}. " +
                              $"All other digits (1-9) have been eliminated by values already placed " +
                              $"in the same row, column, or box. Since {value} is the only possibility, " +
                              $"it must go here."
            };
        }

        return null;
    }

    private static List<Elimination> CollectPeerEliminations(Grid grid, Cell cell, int value)
    {
        var eliminations = new List<Elimination>();
        foreach (var peer in grid.Peers(cell))
        {
            if (!peer.IsSolved && peer.Candidates.Contains(value))
                eliminations.Add(new Elimination(peer, value));
        }
        return eliminations;
    }
}
