using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Hidden Pair/Triple/Quad: When N candidates in a unit appear in exactly N cells,
/// all other candidates can be eliminated from those cells.
/// </summary>
public class HiddenSubsetTechnique : ISolvingTechnique
{
    private readonly int _size;

    public Technique Technique => _size switch
    {
        2 => Technique.HiddenPair,
        3 => Technique.HiddenTriple,
        4 => Technique.HiddenQuad,
        _ => throw new InvalidOperationException()
    };

    public HiddenSubsetTechnique(int size)
    {
        if (size is < 2 or > 4)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be 2, 3, or 4.");
        _size = size;
    }

    public SolveStep? TryApply(Grid grid)
    {
        foreach (var (cells, unitType, unitIndex) in GetAllUnits(grid))
        {
            var unsolved = cells.Where(c => !c.IsSolved).ToList();
            if (unsolved.Count <= _size)
                continue;

            // Build digit→cells map for this unit
            var digitCells = new Dictionary<int, List<Cell>>();
            for (var d = 1; d <= 9; d++)
            {
                var matching = unsolved.Where(c => c.Candidates.Contains(d)).ToList();
                if (matching.Count >= 2 && matching.Count <= _size)
                    digitCells[d] = matching;
            }

            if (digitCells.Count < _size)
                continue;

            var digits = digitCells.Keys.ToList();
            foreach (var digitCombo in Combinations(digits, _size))
            {
                // Union of cells that contain these digits
                var cellSet = new HashSet<Cell>();
                foreach (var d in digitCombo)
                    foreach (var c in digitCells[d])
                        cellSet.Add(c);

                if (cellSet.Count != _size)
                    continue;

                // Found a hidden subset — remove other candidates from these cells
                var subsetDigits = CandidateSet.Empty;
                foreach (var d in digitCombo)
                    subsetDigits = subsetDigits.Add(d);

                var eliminations = new List<Elimination>();
                foreach (var cell in cellSet)
                {
                    var toRemove = cell.Candidates.Except(subsetDigits);
                    foreach (var d in toRemove)
                        eliminations.Add(new Elimination(cell, d));
                }

                if (eliminations.Count == 0)
                    continue;

                // Apply eliminations
                foreach (var elim in eliminations)
                    elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

                var subsetName = _size switch { 2 => "Pair", 3 => "Triple", _ => "Quad" };
                var cellLabels = string.Join(", ", cellSet.Select(c => c.Label));
                var digitStr = string.Join(", ", digitCombo);
                var unitName = FormatUnitName(unitType, unitIndex);

                return new SolveStep
                {
                    Technique = Technique,
                    Eliminations = eliminations,
                    PatternCells = cellSet.ToList(),
                    AffectedCells = cellSet.ToList(),
                    Summary = $"Hidden {subsetName}: {{{digitStr}}} in {cellLabels} ({unitName})",
                    Explanation = $"In {unitName}, the digits {{{digitStr}}} only appear as candidates in " +
                                  $"the cells {cellLabels}. Since these {_size} digits must go in exactly " +
                                  $"these {_size} cells, all other candidates in those cells can be eliminated. " +
                                  $"This is the 'hidden' version because these digits are hidden among other " +
                                  $"candidates in those cells."
                };
            }
        }

        return null;
    }

    private static IEnumerable<(List<Cell> Cells, UnitType Type, int Index)> GetAllUnits(Grid grid)
    {
        for (var i = 0; i < 9; i++)
        {
            yield return (grid.Row(i).ToList(), UnitType.Row, i);
            yield return (grid.Column(i).ToList(), UnitType.Column, i);
            yield return (grid.Box(i).ToList(), UnitType.Box, i);
        }
    }

    private static IEnumerable<List<int>> Combinations(List<int> source, int count)
    {
        if (count == 0)
        {
            yield return [];
            yield break;
        }

        for (var i = 0; i <= source.Count - count; i++)
        {
            foreach (var rest in Combinations(source[(i + 1)..], count - 1))
            {
                rest.Insert(0, source[i]);
                yield return rest;
            }
        }
    }

    private static string FormatUnitName(UnitType unitType, int index) => unitType switch
    {
        UnitType.Row => $"row {index + 1}",
        UnitType.Column => $"column {index + 1}",
        UnitType.Box => $"box {index + 1}",
        _ => throw new ArgumentOutOfRangeException(nameof(unitType))
    };
}
