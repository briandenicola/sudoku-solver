using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Simple Coloring (Singles Chains): For a single candidate digit, if that digit appears
/// in exactly two cells in a unit (a conjugate pair), those cells form a strong link.
/// Following chains of strong links, cells alternate between two "colors."
///
/// Rule 1 (Color Twice in a Unit): If two cells of the same color appear in the same unit,
/// that color is false — eliminate the digit from all cells of that color.
///
/// Rule 2 (Two Colors Elsewhere): If an uncolored cell sees cells of both colors,
/// the digit can be eliminated from that cell (one of the two colors must be true).
/// </summary>
public class SimpleColoringTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.SimpleColoring;

    public SolveStep? TryApply(Grid grid)
    {
        for (var digit = 1; digit <= 9; digit++)
        {
            var result = TryColorDigit(grid, digit);
            if (result != null) return result;
        }
        return null;
    }

    private SolveStep? TryColorDigit(Grid grid, int digit)
    {
        // Build conjugate pairs: units where the digit appears in exactly 2 cells
        var conjugatePairs = new List<(Cell A, Cell B)>();
        foreach (var unit in grid.AllUnits())
        {
            var cells = unit.Where(c => !c.IsSolved && c.Candidates.Contains(digit)).ToList();
            if (cells.Count == 2)
                conjugatePairs.Add((cells[0], cells[1]));
        }

        if (conjugatePairs.Count == 0) return null;

        // Build adjacency graph from conjugate pairs
        var adjacency = new Dictionary<Cell, List<Cell>>();
        foreach (var (a, b) in conjugatePairs)
        {
            if (!adjacency.ContainsKey(a)) adjacency[a] = [];
            if (!adjacency.ContainsKey(b)) adjacency[b] = [];
            adjacency[a].Add(b);
            adjacency[b].Add(a);
        }

        // Color each connected component
        var colored = new Dictionary<Cell, bool>(); // true = color A, false = color B
        foreach (var startCell in adjacency.Keys)
        {
            if (colored.ContainsKey(startCell)) continue;

            // BFS to color this component
            var queue = new Queue<Cell>();
            queue.Enqueue(startCell);
            colored[startCell] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in adjacency[current])
                {
                    if (colored.ContainsKey(neighbor)) continue;
                    colored[neighbor] = !colored[current];
                    queue.Enqueue(neighbor);
                }
            }

            // Analyze this component
            var colorA = colored.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
            var colorB = colored.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();

            if (colorA.Count < 1 || colorB.Count < 1) continue;

            // Rule 1: Color appears twice in same unit → that color is false
            var eliminateColor = CheckColorConflict(grid, colorA, colorB);
            if (eliminateColor != null)
            {
                var (falseColor, trueColor, conflictUnit) = eliminateColor.Value;
                var eliminations = new List<Elimination>();
                foreach (var cell in falseColor)
                {
                    if (cell.Candidates.Contains(digit))
                        eliminations.Add(new Elimination(cell, digit));
                }

                if (eliminations.Count > 0)
                {
                    foreach (var elim in eliminations)
                        elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

                    var falseLabels = string.Join(", ", falseColor.Select(c => c.Label));
                    var trueLabels = string.Join(", ", trueColor.Select(c => c.Label));

                    return new SolveStep
                    {
                        Technique = Technique,
                        Eliminations = eliminations,
                        PatternCells = trueColor.Concat(falseColor).ToList(),
                        AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
                        Summary = $"Simple Coloring (Rule 1): digit {digit}, color conflict in {conflictUnit}",
                        Explanation = $"Using Simple Coloring for digit {digit}: cells are connected by conjugate pairs " +
                            $"(units where {digit} appears in exactly two places). Following the chain, cells alternate " +
                            $"between two colors. Color A: {trueLabels}. Color B: {falseLabels}. " +
                            $"Two cells of the same color appear in the same {conflictUnit}, which is impossible " +
                            $"(a unit can't have the same digit twice). Therefore, that color is false — " +
                            $"digit {digit} is eliminated from all cells of that color."
                    };
                }
            }

            // Rule 2: Uncolored cell sees both colors → eliminate
            var rule2Result = CheckRule2(grid, digit, colorA, colorB);
            if (rule2Result != null) return rule2Result;
        }

        return null;
    }

    private static (List<Cell> FalseColor, List<Cell> TrueColor, string Unit)?
        CheckColorConflict(Grid grid, List<Cell> colorA, List<Cell> colorB)
    {
        if (HasConflictInUnit(grid, colorA))
            return (colorA, colorB, FindConflictUnit(grid, colorA));
        if (HasConflictInUnit(grid, colorB))
            return (colorB, colorA, FindConflictUnit(grid, colorB));
        return null;
    }

    private static bool HasConflictInUnit(Grid grid, List<Cell> colorCells)
    {
        var cellSet = new HashSet<Cell>(colorCells);
        foreach (var unit in grid.AllUnits())
        {
            if (unit.Count(c => cellSet.Contains(c)) > 1)
                return true;
        }
        return false;
    }

    private static string FindConflictUnit(Grid grid, List<Cell> colorCells)
    {
        var cellSet = new HashSet<Cell>(colorCells);
        for (var i = 0; i < 9; i++)
        {
            if (grid.Row(i).Count(c => cellSet.Contains(c)) > 1) return $"row {i + 1}";
            if (grid.Column(i).Count(c => cellSet.Contains(c)) > 1) return $"column {i + 1}";
            if (grid.Box(i).Count(c => cellSet.Contains(c)) > 1) return $"box {i + 1}";
        }
        return "a unit";
    }

    private static SolveStep? CheckRule2(Grid grid, int digit, List<Cell> colorA, List<Cell> colorB)
    {
        var colorACells = new HashSet<Cell>(colorA);
        var colorBCells = new HashSet<Cell>(colorB);
        var allColored = new HashSet<Cell>(colorA.Concat(colorB));

        var eliminations = new List<Elimination>();
        foreach (var cell in grid.AllCells())
        {
            if (cell.IsSolved || allColored.Contains(cell) || !cell.Candidates.Contains(digit))
                continue;

            var peers = new HashSet<Cell>(grid.Peers(cell));
            var seesA = colorACells.Any(c => peers.Contains(c));
            var seesB = colorBCells.Any(c => peers.Contains(c));

            if (seesA && seesB)
                eliminations.Add(new Elimination(cell, digit));
        }

        if (eliminations.Count == 0) return null;

        foreach (var elim in eliminations)
            elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

        var aLabels = string.Join(", ", colorA.Select(c => c.Label));
        var bLabels = string.Join(", ", colorB.Select(c => c.Label));

        return new SolveStep
        {
            Technique = Technique.SimpleColoring,
            Eliminations = eliminations,
            PatternCells = colorA.Concat(colorB).ToList(),
            AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
            Summary = $"Simple Coloring (Rule 2): digit {digit}, {eliminations.Count} elimination(s) seeing both colors",
            Explanation = $"Using Simple Coloring for digit {digit}: cells connected by conjugate pairs alternate " +
                $"between two colors. Color A: {aLabels}. Color B: {bLabels}. " +
                $"One of these colors must be true (contain digit {digit}). " +
                $"Any uncolored cell that can see BOTH a Color A cell and a Color B cell " +
                $"will always see at least one cell that contains {digit}. " +
                $"Therefore, {digit} can be eliminated from those cells."
        };
    }
}
