using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Tests;

public class SolverTests
{
    [Fact]
    public void Solve_EasyPuzzle_Completes()
    {
        // Easy puzzle solvable with just naked and hidden singles
        var puzzle = "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
        var grid = Grid.Parse(puzzle);
        var solver = new Solver();

        var result = solver.Solve(grid);

        Assert.True(result.IsSolved);
        Assert.True(grid.IsSolved);
        Assert.True(result.Steps.Count > 0);
    }

    [Fact]
    public void Solve_AllStepsHaveExplanations()
    {
        var puzzle = "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
        var grid = Grid.Parse(puzzle);
        var solver = new Solver();

        var result = solver.Solve(grid);

        foreach (var step in result.Steps)
        {
            Assert.False(string.IsNullOrWhiteSpace(step.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(step.Summary));
        }
    }

    [Fact]
    public void Solve_AllStepsHaveTechnique()
    {
        var puzzle = "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
        var grid = Grid.Parse(puzzle);
        var solver = new Solver();

        var result = solver.Solve(grid);

        foreach (var step in result.Steps)
        {
            Assert.True(Enum.IsDefined(step.Technique));
        }
    }

    [Fact]
    public void Solve_SolvedGrid_ReturnsNoSteps()
    {
        // A fully solved grid
        var solved = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
        var grid = Grid.Parse(solved);
        var solver = new Solver();

        var result = solver.Solve(grid);

        Assert.True(result.IsSolved);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void Solve_MediumPuzzle()
    {
        // A medium puzzle that may require pairs
        var puzzle = "000000680000073009060800100007060000100508006000040200001006050400390000089000000";
        var grid = Grid.Parse(puzzle);
        var solver = new Solver();

        var result = solver.Solve(grid);

        // Should make progress even if stuck
        Assert.True(result.Steps.Count > 0);
    }
}
