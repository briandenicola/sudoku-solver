namespace SudokuSolver.Engine.Models;

/// <summary>
/// Represents a single step in the solving process, including the technique used,
/// what changed, and a detailed human-readable explanation.
/// </summary>
public class SolveStep
{
    /// <summary>The technique that was applied.</summary>
    public required Technique Technique { get; init; }

    /// <summary>
    /// The cell that was solved (value placed), if any.
    /// Null for elimination-only steps.
    /// </summary>
    public Cell? SolvedCell { get; init; }

    /// <summary>The value that was placed, or 0 for elimination-only steps.</summary>
    public int PlacedValue { get; init; }

    /// <summary>
    /// Candidates that were eliminated (cell → removed digits).
    /// For placement steps, this includes the peer eliminations caused by the placement.
    /// </summary>
    public IReadOnlyList<Elimination> Eliminations { get; init; } = [];

    /// <summary>
    /// Cells that form the pattern (e.g., the two cells in an X-Wing).
    /// Used for UI highlighting.
    /// </summary>
    public IReadOnlyList<Cell> PatternCells { get; init; } = [];

    /// <summary>
    /// Cells that are affected by the step (eliminations happened here).
    /// Used for UI highlighting.
    /// </summary>
    public IReadOnlyList<Cell> AffectedCells { get; init; } = [];

    /// <summary>
    /// Specific candidate digits within pattern cells to highlight (green circles).
    /// Each entry identifies a cell and the digit that is relevant to the pattern.
    /// For example, in an XY-Wing with Z=7, this would contain the 7s in the wing cells.
    /// </summary>
    public IReadOnlyList<CandidateHighlight> HighlightedCandidates { get; init; } = [];

    /// <summary>
    /// Detailed explanation of how the technique was identified and applied.
    /// This is the core teaching content.
    /// </summary>
    public required string Explanation { get; init; }

    /// <summary>
    /// Short summary suitable for a step list (e.g., "Naked Single: R3C7 = 5").
    /// </summary>
    public required string Summary { get; init; }
}

/// <summary>
/// Records the removal of a candidate digit from a specific cell.
/// </summary>
public record Elimination(Cell Cell, int Digit);

/// <summary>
/// Identifies a specific candidate digit in a cell to highlight as part of the pattern.
/// </summary>
public record CandidateHighlight(Cell Cell, int Digit);
