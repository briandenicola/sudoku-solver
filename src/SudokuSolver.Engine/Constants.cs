namespace SudokuSolver.Engine;

/// <summary>
/// Constants used throughout the Sudoku solver.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The size of the Sudoku grid (9x9).
    /// </summary>
    public const int GridSize = 9;

    /// <summary>
    /// Maximum number of steps allowed in a single solve operation.
    /// Prevents infinite loops on pathological puzzles.
    /// </summary>
    public const int MaxSolveSteps = 1000;

    /// <summary>
    /// Maximum number of AI assistance attempts when stuck.
    /// </summary>
    public const int MaxAiAttempts = 50;
}