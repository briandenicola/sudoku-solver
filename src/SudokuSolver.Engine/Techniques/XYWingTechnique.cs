using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// XY-Wing: Three bi-value cells forming a hinge pattern.
/// The hinge cell (XY) sees two wing cells (XZ and YZ).
/// Any cell that sees both wings can have Z eliminated.
///
/// Example: Hinge has {3,5}, Wing1 has {3,7}, Wing2 has {5,7}.
/// Z=7 can be eliminated from any cell that sees both wings.
/// </summary>
public class XYWingTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.XYWing;

    public SolveStep? TryApply(Grid grid)
    {
        var biValueCells = grid.AllCells()
            .Where(c => !c.IsSolved && c.Candidates.Count == 2)
            .ToList();

        foreach (var hinge in biValueCells)
        {
            var hingeDigits = hinge.Candidates.ToList();
            var x = hingeDigits[0];
            var y = hingeDigits[1];

            var hingePeers = grid.Peers(hinge)
                .Where(c => biValueCells.Contains(c))
                .ToList();

            foreach (var wing1 in hingePeers)
            {
                if (!wing1.Candidates.Contains(x)) continue;
                var wing1Other = wing1.Candidates.Except(CandidateSet.Of(x));
                if (wing1Other.Count != 1) continue;
                var z = wing1Other.Single();
                if (z == y) continue;

                // wing1 is XZ — look for wing2 = YZ
                var targetCandidates = CandidateSet.Of(y, z);
                foreach (var wing2 in hingePeers)
                {
                    if (wing2 == wing1) continue;
                    if (wing2.Candidates != targetCandidates) continue;

                    // Found XY-Wing: hinge=XY, wing1=XZ, wing2=YZ
                    // Eliminate Z from cells that see both wings
                    var wing1Peers = new HashSet<Cell>(grid.Peers(wing1));
                    var wing2Peers = new HashSet<Cell>(grid.Peers(wing2));

                    var eliminations = new List<Elimination>();
                    foreach (var cell in wing1Peers)
                    {
                        if (cell != hinge && !cell.IsSolved &&
                            cell.Candidates.Contains(z) &&
                            wing2Peers.Contains(cell))
                        {
                            eliminations.Add(new Elimination(cell, z));
                        }
                    }

                    if (eliminations.Count == 0) continue;

                    foreach (var elim in eliminations)
                        elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

                    // Highlight the Z digit in both wings (the candidate being eliminated elsewhere)
                    var highlights = new List<CandidateHighlight>
                    {
                        new(wing1, z),
                        new(wing2, z),
                        new(hinge, x),
                        new(hinge, y)
                    };

                    return new SolveStep
                    {
                        Technique = Technique,
                        Eliminations = eliminations,
                        HighlightedCandidates = highlights,
                        PatternCells = [hinge, wing1, wing2],
                        AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
                        Summary = $"XY-Wing: hinge {hinge.Label}{{{x},{y}}}, wings {wing1.Label}{{{x},{z}}} and {wing2.Label}{{{y},{z}}} → eliminate {z}",
                        Explanation = $"An XY-Wing pattern was found. The hinge cell {hinge.Label} contains candidates {{{x},{y}}}. " +
                            $"It sees two wing cells: {wing1.Label} with candidates {{{x},{z}}} and {wing2.Label} with candidates {{{y},{z}}}. " +
                            $"The common digit between the two wings is {z}. " +
                            $"Here's the logic: if the hinge is {x}, then wing1 must be {z}. " +
                            $"If the hinge is {y}, then wing2 must be {z}. " +
                            $"Either way, at least one of the wings contains {z}. " +
                            $"Therefore, any cell that can see BOTH wing cells cannot contain {z}, " +
                            $"because one of those wings will definitely be {z}."
                    };
                }
            }
        }

        return null;
    }
}
