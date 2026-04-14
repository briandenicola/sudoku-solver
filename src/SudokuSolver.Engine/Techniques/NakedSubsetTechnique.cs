using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Naked Pair/Triple/Quad: When N cells in a unit share exactly N candidates between them,
/// those candidates can be eliminated from all other cells in the unit.
/// </summary>
public class NakedSubsetTechnique : ISolvingTechnique
{
    private readonly int _size;

    public Technique Technique => _size switch
    {
        2 => Technique.NakedPair,
        3 => Technique.NakedTriple,
        4 => Technique.NakedQuad,
        _ => throw new InvalidOperationException()
    };

    public NakedSubsetTechnique(int size)
    {
        if (size is < 2 or > 4)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be 2, 3, or 4.");
        _size = size;
    }

    public SolveStep? TryApply(Grid grid)
    {
        foreach (var (cells, unitType, unitIndex) in GetAllUnits(grid))
        {
            var unsolved = cells.Where(c => !c.IsSolved && c.Candidates.Count >= 2 && c.Candidates.Count <= _size).ToList();
            if (unsolved.Count < _size)
                continue;

            foreach (var combo in Combinations(unsolved, _size))
            {
                var union = CandidateSet.Empty;
                foreach (var cell in combo)
                    union = union.Union(cell.Candidates);

                if (union.Count != _size)
                    continue;

                // Found a naked subset — eliminate these candidates from other cells in the unit
                var eliminations = new List<Elimination>();
                var otherCells = cells.Where(c => !c.IsSolved && !combo.Contains(c)).ToList();

                foreach (var other in otherCells)
                {
                    var overlap = other.Candidates.Intersect(union);
                    foreach (var digit in overlap)
                        eliminations.Add(new Elimination(other, digit));
                }

                if (eliminations.Count == 0)
                    continue;

                // Apply eliminations
                foreach (var elim in eliminations)
                    elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

                var subsetName = _size switch { 2 => "Pair", 3 => "Triple", _ => "Quad" };
                var cellLabels = string.Join(", ", combo.Select(c => c.Label));
                var digits = string.Join(", ", union);
                var unitName = FormatUnitName(unitType, unitIndex);

                // Highlight the shared candidates in the subset cells
                var highlights = new List<CandidateHighlight>();
                foreach (var cell in combo)
                    foreach (var d in cell.Candidates.Intersect(union))
                        highlights.Add(new CandidateHighlight(cell, d));

                return new SolveStep
                {
                    Technique = Technique,
                    Eliminations = eliminations,
                    HighlightedCandidates = highlights,
                    PatternCells = combo.ToList(),
                    AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
                    Summary = $"Naked {subsetName}: {{{digits}}} in {cellLabels} ({unitName})",
                    Explanation = $"In {unitName}, the cells {cellLabels} together contain only the candidates " +
                                  $"{{{digits}}}. Since these {_size} cells share exactly {_size} candidates, " +
                                  $"those digits must go in these cells (in some order). Therefore, " +
                                  $"the digits {{{digits}}} can be eliminated from all other cells in this " +
                                  $"{unitType.ToString().ToLowerInvariant()}."
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

    private static IEnumerable<List<Cell>> Combinations(List<Cell> source, int count)
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
