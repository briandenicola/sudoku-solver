using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Fish techniques (X-Wing and Swordfish):
/// When a candidate appears in exactly N rows, and all occurrences fall in the same N columns
/// (or vice versa), the candidate can be eliminated from those columns (or rows) in other rows (or columns).
///
/// X-Wing: N=2, Swordfish: N=3
/// </summary>
public class FishTechnique : ISolvingTechnique
{
    private readonly int _size;

    public Technique Technique => _size switch
    {
        2 => Technique.XWing,
        3 => Technique.Swordfish,
        _ => throw new InvalidOperationException()
    };

    public FishTechnique(int size)
    {
        if (size is < 2 or > 3)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be 2 (X-Wing) or 3 (Swordfish).");
        _size = size;
    }

    public SolveStep? TryApply(Grid grid)
    {
        // Try row-based fish (eliminate from columns)
        var result = TryFish(grid, isRowBased: true);
        if (result != null) return result;

        // Try column-based fish (eliminate from rows)
        return TryFish(grid, isRowBased: false);
    }

    private SolveStep? TryFish(Grid grid, bool isRowBased)
    {
        for (var digit = 1; digit <= 9; digit++)
        {
            // Build a map of base-line-index → set of cover-line positions
            var linePositions = new Dictionary<int, List<int>>();

            for (var line = 0; line < 9; line++)
            {
                var cells = isRowBased ? grid.Row(line).ToList() : grid.Column(line).ToList();
                var positions = new List<int>();
                foreach (var cell in cells)
                {
                    if (!cell.IsSolved && cell.Candidates.Contains(digit))
                        positions.Add(isRowBased ? cell.Column : cell.Row);
                }

                if (positions.Count >= 2 && positions.Count <= _size)
                    linePositions[line] = positions;
            }

            if (linePositions.Count < _size)
                continue;

            var lines = linePositions.Keys.ToList();
            foreach (var lineCombo in Combinations(lines, _size))
            {
                // Union of cover positions
                var coverPositions = new HashSet<int>();
                foreach (var line in lineCombo)
                    foreach (var pos in linePositions[line])
                        coverPositions.Add(pos);

                if (coverPositions.Count != _size)
                    continue;

                // Found a fish — collect eliminations from cover lines outside base lines
                var eliminations = new List<Elimination>();
                var patternCells = new List<Cell>();
                var baseLineSet = new HashSet<int>(lineCombo);

                foreach (var coverPos in coverPositions)
                {
                    for (var i = 0; i < 9; i++)
                    {
                        var cell = isRowBased ? grid[i, coverPos] : grid[coverPos, i];

                        if (!cell.IsSolved && cell.Candidates.Contains(digit))
                        {
                            var baseLine = isRowBased ? cell.Row : cell.Column;
                            if (baseLineSet.Contains(baseLine))
                                patternCells.Add(cell);
                            else
                                eliminations.Add(new Elimination(cell, digit));
                        }
                    }
                }

                if (eliminations.Count == 0)
                    continue;

                foreach (var elim in eliminations)
                    elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

                var fishName = _size == 2 ? "X-Wing" : "Swordfish";
                var baseType = isRowBased ? "rows" : "columns";
                var coverType = isRowBased ? "columns" : "rows";
                var baseLabels = string.Join(", ", lineCombo.Select(l => l + 1));
                var coverLabels = string.Join(", ", coverPositions.OrderBy(p => p).Select(p => p + 1));
                var patternLabels = string.Join(", ", patternCells.Select(c => c.Label));

                return new SolveStep
                {
                    Technique = Technique,
                    Eliminations = eliminations,
                    PatternCells = patternCells,
                    AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
                    Summary = $"{fishName}: digit {digit} in {baseType} {baseLabels} / {coverType} {coverLabels}",
                    Explanation = BuildExplanation(fishName, digit, baseType, coverType, baseLabels, coverLabels, patternLabels)
                };
            }
        }

        return null;
    }

    private static string BuildExplanation(string fishName, int digit, string baseType, string coverType,
        string baseLabels, string coverLabels, string patternLabels)
    {
        return $"The digit {digit} forms an {fishName} pattern. Looking at {baseType} {baseLabels}, " +
               $"the digit {digit} only appears in {coverType} {coverLabels}. " +
               $"The pattern cells are: {patternLabels}. " +
               $"Since {digit} in these {baseType} is restricted to these {coverType}, " +
               $"one of the pattern cells in each {baseType[..^1]} must contain {digit}. " +
               $"This means {digit} cannot appear in any other cell in {coverType} {coverLabels} " +
               $"outside of {baseType} {baseLabels}. " +
               $"Think of it as {digit} forming a rectangular pattern — no matter which diagonal " +
               $"arrangement {digit} takes, it 'covers' all of {coverType} {coverLabels}.";
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
}
