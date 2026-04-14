using SudokuSolver.Engine.Models;
using SudokuSolver.Engine.Techniques;

namespace SudokuSolver.Engine;

/// <summary>
/// Solves a sudoku puzzle step-by-step, trying techniques in order of difficulty.
/// Each step produces a detailed explanation of the technique and reasoning.
/// </summary>
public class Solver
{
    private readonly IReadOnlyList<ISolvingTechnique> _techniques;

    public Solver() : this(DefaultTechniques()) { }

    public Solver(IReadOnlyList<ISolvingTechnique> techniques)
    {
        _techniques = techniques;
    }

    /// <summary>
    /// Solves the puzzle step by step, returning all steps in order.
    /// The grid is mutated as solving progresses.
    /// </summary>
    /// <param name="grid">The puzzle grid (will be modified in place).</param>
    /// <param name="maxSteps">Safety limit to prevent infinite loops.</param>
    /// <returns>The ordered list of solve steps.</returns>
    public SolveResult Solve(Grid grid, int maxSteps = 1000)
    {
        var steps = new List<SolveStep>();

        for (var i = 0; i < maxSteps; i++)
        {
            if (grid.IsSolved)
                return new SolveResult(steps, SolveOutcome.Solved);

            var step = TryNextStep(grid);
            if (step == null)
                return new SolveResult(steps, SolveOutcome.Stuck);

            steps.Add(step);
        }

        return new SolveResult(steps, SolveOutcome.Stuck);
    }

    /// <summary>
    /// Tries each technique in order and returns the first step found.
    /// </summary>
    private SolveStep? TryNextStep(Grid grid)
    {
        foreach (var technique in _techniques)
        {
            var step = technique.TryApply(grid);
            if (step != null)
                return step;
        }
        return null;
    }

    private static List<ISolvingTechnique> DefaultTechniques() =>
    [
        new NakedSingleTechnique(),
        new HiddenSingleTechnique(),
        new NakedSubsetTechnique(2),
        new NakedSubsetTechnique(3),
        new NakedSubsetTechnique(4),
        new HiddenSubsetTechnique(2),
        new HiddenSubsetTechnique(3),
        new HiddenSubsetTechnique(4),
        new PointingPairTechnique(),
        new BoxLineReductionTechnique(),
        new FishTechnique(2), // X-Wing
        new FishTechnique(3), // Swordfish
    ];
}

public enum SolveOutcome
{
    Solved,
    Stuck
}

public record SolveResult(IReadOnlyList<SolveStep> Steps, SolveOutcome Outcome)
{
    public bool IsSolved => Outcome == SolveOutcome.Solved;
}
