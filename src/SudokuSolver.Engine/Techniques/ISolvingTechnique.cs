using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Techniques;

/// <summary>
/// A sudoku solving technique that can be applied to a grid.
/// Each implementation looks for a specific pattern and returns
/// a SolveStep describing what was found and changed.
/// </summary>
public interface ISolvingTechnique
{
    /// <summary>The technique this implementation represents.</summary>
    Technique Technique { get; }

    /// <summary>
    /// Attempts to find and apply the technique to the grid.
    /// Returns a SolveStep if the technique was applicable, null otherwise.
    /// The grid is mutated in place when a step is found.
    /// </summary>
    SolveStep? TryApply(Grid grid);
}
