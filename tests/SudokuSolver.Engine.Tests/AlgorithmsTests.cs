using SudokuSolver.Engine;

namespace SudokuSolver.Engine.Tests;

/// <summary>
/// Tests for shared algorithms and performance-critical code paths.
/// </summary>
public class AlgorithmsTests
{
    [Fact]
    public void Combinations_CountMatchesExpected()
    {
        // n choose k: 5 choose 2 = 10, 6 choose 3 = 20, 9 choose 4 = 126
        var source5 = new List<int> { 1, 2, 3, 4, 5 };
        var combos2 = Algorithms.Combinations(source5, 2).ToList();
        Assert.Equal(10, combos2.Count);

        var source6 = new List<int> { 1, 2, 3, 4, 5, 6 };
        var combos3 = Algorithms.Combinations(source6, 3).ToList();
        Assert.Equal(20, combos3.Count);

        var source9 = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var combos4 = Algorithms.Combinations(source9, 4).ToList();
        Assert.Equal(126, combos4.Count);
    }

    [Fact]
    public void Combinations_EmptyCount()
    {
        var source = new List<int>();
        var combos = Algorithms.Combinations(source, 2).ToList();
        Assert.Empty(combos);
    }

    [Fact]
    public void Combinations_ZeroCount()
    {
        var source = new List<int> { 1, 2, 3 };
        var combos = Algorithms.Combinations(source, 0).ToList();
        Assert.Single(combos); // One empty combination
        Assert.Empty(combos[0]);
    }

    [Fact]
    public void Combinations_AllUnique()
    {
        var source = new List<int> { 1, 2, 3, 4, 5 };
        var combos = Algorithms.Combinations(source, 2).ToList();
        
        // All combinations should be unique
        var uniqueCombos = combos.Select(c => string.Join(",", c.OrderBy(x => x))).Distinct().Count();
        Assert.Equal(combos.Count, uniqueCombos);
    }

    [Fact]
    public void Combinations_Performance_LargeSet()
    {
        // Test that combinations on a reasonable set complete quickly
        var source = Enumerable.Range(1, 9).ToList();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var combos = Algorithms.Combinations(source, 4).ToList();
        
        stopwatch.Stop();
        Assert.Equal(126, combos.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 100, 
            $"Combinations took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
    }

    [Fact]
    public void FormatUnitName_ReturnsCorrectFormat()
    {
        Assert.Equal("row 1", Algorithms.FormatUnitName(Models.UnitType.Row, 0));
        Assert.Equal("row 9", Algorithms.FormatUnitName(Models.UnitType.Row, 8));
        Assert.Equal("column 5", Algorithms.FormatUnitName(Models.UnitType.Column, 4));
        Assert.Equal("box 3", Algorithms.FormatUnitName(Models.UnitType.Box, 2));
    }
}