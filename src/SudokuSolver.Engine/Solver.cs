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
        new FishTechnique(4), // Jellyfish
        new XYWingTechnique(),
        new XYZWingTechnique(),
        new UniqueRectangleTechnique(),
        new SimpleColoringTechnique(),
        new BacktrackingTechnique(), // Last resort — guarantees a solution
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

    /// <summary>
    /// Rates the puzzle difficulty based on the techniques required to solve it.
    /// </summary>
    public DifficultyRating GetDifficulty()
    {
        if (Steps.Count == 0)
            return new DifficultyRating("Unknown", 0, "No steps were produced.");

        var techniqueCounts = Steps
            .GroupBy(s => s.Technique)
            .ToDictionary(g => g.Key, g => g.Count());

        var hardestTechnique = Steps
            .Select(s => s.Technique)
            .Max();

        var score = Steps.Sum(s => GetTechniqueWeight(s.Technique));
        var totalSteps = Steps.Count;

        var (label, stars) = ClassifyDifficulty(hardestTechnique, score, totalSteps);
        var breakdown = BuildBreakdown(techniqueCounts, hardestTechnique, score, totalSteps);

        return new DifficultyRating(label, stars, breakdown);
    }

    private static int GetTechniqueWeight(Technique technique) => technique switch
    {
        Technique.NakedSingle => 1,
        Technique.HiddenSingle => 2,
        Technique.NakedPair => 4,
        Technique.NakedTriple => 5,
        Technique.NakedQuad => 6,
        Technique.HiddenPair => 5,
        Technique.HiddenTriple => 6,
        Technique.HiddenQuad => 7,
        Technique.PointingPair => 4,
        Technique.BoxLineReduction => 4,
        Technique.XWing => 8,
        Technique.Swordfish => 10,
        Technique.Jellyfish => 12,
        Technique.XYWing => 10,
        Technique.XYZWing => 11,
        Technique.UniqueRectangle => 9,
        Technique.SimpleColoring => 12,
        Technique.AiAssisted => 15,
        Technique.Backtracking => 15,
        _ => 5
    };

    private static (string Label, int Stars) ClassifyDifficulty(Technique hardest, int score, int totalSteps)
    {
        // Primary factor: hardest technique used
        // Secondary factor: cumulative score for volume of work
        if (hardest <= Technique.HiddenSingle && score < 80)
            return ("Beginner", 1);

        if (hardest <= Technique.BoxLineReduction && score < 150)
            return ("Easy", 2);

        if (hardest <= Technique.BoxLineReduction)
            return ("Medium", 3);

        if (hardest <= Technique.Swordfish && score < 250)
            return ("Hard", 4);

        if (hardest <= Technique.SimpleColoring)
            return ("Expert", 5);

        // AI or backtracking needed
        return ("Diabolical", 5);
    }

    private static string BuildBreakdown(Dictionary<Technique, int> counts, Technique hardest, int score, int totalSteps)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Total steps: {totalSteps}  |  Difficulty score: {score}");
        lines.AppendLine($"Hardest technique: {FormatTechniqueName(hardest)}");
        lines.AppendLine();
        lines.AppendLine("Technique breakdown:");

        foreach (var (technique, count) in counts.OrderBy(kv => kv.Key))
        {
            lines.AppendLine($"  {FormatTechniqueName(technique)}: {count} step(s)");
        }

        return lines.ToString().TrimEnd();
    }

    private static string FormatTechniqueName(Technique t) => t switch
    {
        Technique.NakedSingle => "Naked Single",
        Technique.HiddenSingle => "Hidden Single",
        Technique.NakedPair => "Naked Pair",
        Technique.NakedTriple => "Naked Triple",
        Technique.NakedQuad => "Naked Quad",
        Technique.HiddenPair => "Hidden Pair",
        Technique.HiddenTriple => "Hidden Triple",
        Technique.HiddenQuad => "Hidden Quad",
        Technique.PointingPair => "Pointing Pair",
        Technique.BoxLineReduction => "Box/Line Reduction",
        Technique.XWing => "X-Wing",
        Technique.Swordfish => "Swordfish",
        Technique.Jellyfish => "Jellyfish",
        Technique.XYWing => "XY-Wing",
        Technique.XYZWing => "XYZ-Wing",
        Technique.UniqueRectangle => "Unique Rectangle",
        Technique.SimpleColoring => "Simple Coloring",
        Technique.AiAssisted => "AI-Assisted",
        Technique.Backtracking => "Backtracking",
        _ => t.ToString()
    };
}

public record DifficultyRating(string Label, int Stars, string Breakdown)
{
    /// <summary>Returns star display like "★★★☆☆"</summary>
    public string StarsDisplay => new string('★', Stars) + new string('☆', 5 - Stars);
}
