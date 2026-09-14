using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Tests;

public class PuzzleGeneratorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Generate_ReturnsPuzzleWithRequestedDifficulty(int difficulty)
    {
        var generator = new PuzzleGenerator(seed: 1234 + difficulty);

        var generated = generator.Generate(difficulty, maxAttempts: 5);

        Assert.Equal(difficulty, generated.Difficulty.Stars);
        Assert.True(generated.SolveResult.IsSolved);
        Assert.False(generated.Grid.IsSolved);
        Assert.InRange(generated.Grid.AllCells().Count(c => c.IsGiven), 17, 80);

        var grid = generated.Grid.Clone();
        var result = new Solver().Solve(grid);
        Assert.True(result.IsSolved);
        Assert.True(grid.IsSolved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Generate_InvalidDifficulty_Throws(int difficulty)
    {
        var generator = new PuzzleGenerator(seed: 1234);

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(difficulty));
    }
}
