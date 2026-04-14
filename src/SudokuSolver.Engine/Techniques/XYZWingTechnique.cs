using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// XYZ-Wing: Extension of XY-Wing with a tri-value hinge.
/// The hinge cell (XYZ) sees two wing cells (XZ and YZ).
/// Z can be eliminated from any cell that sees all three (hinge + both wings).
///
/// Unlike XY-Wing, the hinge also contains Z, so eliminations must see the hinge too.
/// </summary>
public class XYZWingTechnique : ISolvingTechnique
{
    public Technique Technique => Technique.XYZWing;

    public SolveStep? TryApply(Grid grid)
    {
        var triValueCells = grid.AllCells()
            .Where(c => !c.IsSolved && c.Candidates.Count == 3)
            .ToList();

        var biValueCells = grid.AllCells()
            .Where(c => !c.IsSolved && c.Candidates.Count == 2)
            .ToList();

        foreach (var hinge in triValueCells)
        {
            var hingeDigits = hinge.Candidates.ToList();
            var hingePeers = grid.Peers(hinge).Where(c => biValueCells.Contains(c)).ToList();

            // Try all pairs of digits from the hinge as X,Y; the third is Z
            for (var i = 0; i < 3; i++)
            {
                var z = hingeDigits[i];
                var x = hingeDigits[(i + 1) % 3];
                var y = hingeDigits[(i + 2) % 3];

                // Look for wing1=XZ among hinge peers
                var xzSet = CandidateSet.Of(x, z);
                var yzSet = CandidateSet.Of(y, z);

                foreach (var wing1 in hingePeers)
                {
                    if (wing1.Candidates != xzSet) continue;

                    foreach (var wing2 in hingePeers)
                    {
                        if (wing2 == wing1) continue;
                        if (wing2.Candidates != yzSet) continue;

                        // Found XYZ-Wing: hinge=XYZ, wing1=XZ, wing2=YZ
                        // Z eliminated from cells seeing all three
                        var hingePeerSet = new HashSet<Cell>(grid.Peers(hinge));
                        var wing1PeerSet = new HashSet<Cell>(grid.Peers(wing1));
                        var wing2PeerSet = new HashSet<Cell>(grid.Peers(wing2));

                        var eliminations = new List<Elimination>();
                        foreach (var cell in hingePeerSet)
                        {
                            if (cell != wing1 && cell != wing2 &&
                                !cell.IsSolved && cell.Candidates.Contains(z) &&
                                wing1PeerSet.Contains(cell) && wing2PeerSet.Contains(cell))
                            {
                                eliminations.Add(new Elimination(cell, z));
                            }
                        }

                        if (eliminations.Count == 0) continue;

                        foreach (var elim in eliminations)
                            elim.Cell.Candidates = elim.Cell.Candidates.Remove(elim.Digit);

                        var highlights = new List<CandidateHighlight>
                        {
                            new(hinge, z),
                            new(wing1, z),
                            new(wing2, z)
                        };

                        return new SolveStep
                        {
                            Technique = Technique,
                            Eliminations = eliminations,
                            HighlightedCandidates = highlights,
                            PatternCells = [hinge, wing1, wing2],
                            AffectedCells = eliminations.Select(e => e.Cell).Distinct().ToList(),
                            Summary = $"XYZ-Wing: hinge {hinge.Label}{{{x},{y},{z}}}, wings {wing1.Label}{{{x},{z}}} and {wing2.Label}{{{y},{z}}} → eliminate {z}",
                            Explanation = $"An XYZ-Wing pattern was found. The hinge cell {hinge.Label} contains candidates {{{x},{y},{z}}}. " +
                                $"It sees two wing cells: {wing1.Label} with {{{x},{z}}} and {wing2.Label} with {{{y},{z}}}. " +
                                $"The digit {z} appears in all three cells. " +
                                $"If the hinge is {x}, wing1 must be {z}. If the hinge is {y}, wing2 must be {z}. " +
                                $"If the hinge is {z}, then {z} is already in the hinge. " +
                                $"In every case, at least one of these three cells is {z}. " +
                                $"Any cell that sees ALL three pattern cells cannot be {z}."
                        };
                    }
                }
            }
        }

        return null;
    }
}
