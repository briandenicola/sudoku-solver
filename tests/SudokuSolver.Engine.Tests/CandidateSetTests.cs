using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Tests;

public class CandidateSetTests
{
    [Fact]
    public void Empty_HasZeroCount()
    {
        Assert.Equal(0, CandidateSet.Empty.Count);
        Assert.True(CandidateSet.Empty.IsEmpty);
    }

    [Fact]
    public void All_HasNineDigits()
    {
        Assert.Equal(9, CandidateSet.All.Count);
        for (var d = 1; d <= 9; d++)
            Assert.True(CandidateSet.All.Contains(d));
    }

    [Fact]
    public void Of_CreatesCorrectSet()
    {
        var set = CandidateSet.Of(1, 5, 9);
        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(5));
        Assert.True(set.Contains(9));
        Assert.False(set.Contains(2));
    }

    [Fact]
    public void Add_And_Remove()
    {
        var set = CandidateSet.Empty.Add(3).Add(7);
        Assert.Equal(2, set.Count);
        set = set.Remove(3);
        Assert.Equal(1, set.Count);
        Assert.False(set.Contains(3));
        Assert.True(set.Contains(7));
    }

    [Fact]
    public void Single_ReturnsDigit_WhenCountIsOne()
    {
        var set = CandidateSet.Of(5);
        Assert.Equal(5, set.Single());
    }

    [Fact]
    public void Single_Throws_WhenCountIsNotOne()
    {
        Assert.Throws<InvalidOperationException>(() => CandidateSet.Of(1, 2).Single());
        Assert.Throws<InvalidOperationException>(() => CandidateSet.Empty.Single());
    }

    [Fact]
    public void SetOperations()
    {
        var a = CandidateSet.Of(1, 2, 3);
        var b = CandidateSet.Of(2, 3, 4);

        Assert.Equal(CandidateSet.Of(1, 2, 3, 4), a.Union(b));
        Assert.Equal(CandidateSet.Of(2, 3), a.Intersect(b));
        Assert.Equal(CandidateSet.Of(1), a.Except(b));
    }

    [Fact]
    public void InvalidDigit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CandidateSet.Of(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CandidateSet.Of(10));
    }
}
