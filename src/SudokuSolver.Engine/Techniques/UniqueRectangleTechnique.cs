using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// Unique Rectangle (Type 1): Exploits the constraint that a valid sudoku has exactly one solution.
///
/// If four cells in exactly two rows, two columns, and two boxes all share the same two candidates,
/// the puzzle would have two solutions (digits could be swapped). This is called a "deadly pattern."
///
/// Type 1: Three of the four rectangle cells are bi-value (only the two shared candidates).
/// The fourth cell has extra candidates. The shared candidates can be eliminated from the fourth cell
/// because keeping them would create the deadly pattern.
/// </summary>
public class UniqueRectangleTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.UniqueRectangle;

    public SolveStep? TryApply(Grid grid)
    {
        // Find all bi-value cells
        var biValueCells = grid.AllCells()
            .Where(c => !c.IsSolved && c.Candidates.Count == 2)
            .ToList();

        // Group by candidate pair
        var byPair = new Dictionary<(int, int), List<Cell>>();
        foreach (var cell in biValueCells)
        {
            var digits = cell.Candidates.ToList();
            var key = (Math.Min(digits[0], digits[1]), Math.Max(digits[0], digits[1]));
            if (!byPair.TryGetValue(key, out var list))
            {
                list = [];
                byPair[key] = list;
            }
            list.Add(cell);
        }

        foreach (var ((d1, d2), cells) in byPair)
        {
            if (cells.Count < 3) continue;

            // Try all triples of bi-value cells with the same pair
            for (var a = 0; a < cells.Count; a++)
            for (var b = a + 1; b < cells.Count; b++)
            for (var c = b + 1; c < cells.Count; c++)
            {
                var triple = new[] { cells[a], cells[b], cells[c] };

                // These three must span exactly 2 rows and 2 columns
                var rows = triple.Select(x => x.Row).Distinct().ToList();
                var cols = triple.Select(x => x.Column).Distinct().ToList();

                if (rows.Count != 2 || cols.Count != 2) continue;

                // Must span exactly 2 boxes
                var boxes = triple.Select(x => x.Box).Distinct().ToList();
                if (boxes.Count < 2) continue;

                // Find the fourth corner
                var occupiedPositions = triple.Select(x => (x.Row, x.Column)).ToHashSet();
                (int Row, int Col)? fourthPos = null;
                foreach (var r in rows)
                foreach (var col in cols)
                {
                    if (!occupiedPositions.Contains((r, col)))
                    {
                        fourthPos = (r, col);
                        break;
                    }
                }

                if (fourthPos == null) continue;

                var fourth = grid[fourthPos.Value.Row, fourthPos.Value.Col];

                // Fourth cell must be unsolved with extra candidates beyond d1,d2
                if (fourth.IsSolved) continue;
                if (!fourth.Candidates.Contains(d1) || !fourth.Candidates.Contains(d2)) continue;
                if (fourth.Candidates.Count <= 2) continue;

                // All four cells must span exactly 2 boxes
                var allBoxes = triple.Select(x => x.Box).Append(fourth.Box).Distinct().Count();
                if (allBoxes != 2) continue;

                // Type 1 UR found — eliminate d1 and d2 from the fourth cell
                var eliminations = new List<Elimination>();
                if (fourth.Candidates.Contains(d1))
                    eliminations.Add(new Elimination(fourth, d1));
                if (fourth.Candidates.Contains(d2))
                    eliminations.Add(new Elimination(fourth, d2));

                if (eliminations.Count == 0) continue;

                foreach (var elim in eliminations)
                    elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

                var rectCells = triple.Append(fourth).ToList();
                var tripleLabels = string.Join(", ", triple.Select(x => x.Label));

                // Highlight the shared candidates in the three bi-value corners
                var highlights = new List<CandidateHighlight>();
                foreach (var t in triple)
                {
                    highlights.Add(new CandidateHighlight(t, d1));
                    highlights.Add(new CandidateHighlight(t, d2));
                }

                return new SolveStep
                {
                    Technique = Technique,
                    Eliminations = eliminations,
                    HighlightedCandidates = highlights,
                    PatternCells = rectCells,
                    AffectedCells = [fourth],
                    Summary = $"Unique Rectangle (Type 1): {{{d1},{d2}}} in {string.Join(", ", rectCells.Select(x => x.Label))} → eliminate {{{d1},{d2}}} from {fourth.Label}",
                    Explanation = $"A Unique Rectangle (Type 1) was found. The cells {tripleLabels} each contain " +
                        $"only the candidates {{{d1},{d2}}}. Together with {fourth.Label}, they form a rectangle " +
                        $"spanning two rows, two columns, and two boxes. " +
                        $"If {fourth.Label} also contained only {{{d1},{d2}}}, all four cells would form a 'deadly pattern' — " +
                        $"the two digits could be swapped between them, giving the puzzle two solutions. " +
                        $"Since a valid sudoku must have exactly one solution, {fourth.Label} cannot be " +
                        $"limited to just {{{d1},{d2}}}. Therefore, {d1} and {d2} can be eliminated from {fourth.Label}, " +
                        $"leaving it with candidates {fourth.Candidates}."
                };
            }
        }

        return null;
    }
}
