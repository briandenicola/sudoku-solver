using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine.Tests;

public class GridTests
{
    [Fact]
    public void Parse_CreatesValidGrid()
    {
        // A well-known easy puzzle
        var puzzle = "530070000600195000098000060800060003400803001700020006060000280000419005000080079";
        var grid = Grid.Parse(puzzle);

        Assert.Equal(5, grid[0, 0].Value);
        Assert.True(grid[0, 0].IsGiven);
        Assert.Equal(0, grid[0, 2].Value);
        Assert.False(grid[0, 2].IsSolved);
    }

    [Fact]
    public void Parse_InvalidLength_Throws()
    {
        Assert.Throws<FormatException>(() => Grid.Parse("123"));
    }

    [Fact]
    public void Peers_Returns20Cells()
    {
        var grid = new Grid();
        var peers = grid.Peers(grid[0, 0]).ToList();
        Assert.Equal(20, peers.Count);
    }

    [Fact]
    public void Clone_IsDeepCopy()
    {
        var grid = Grid.Parse("530070000600195000098000060800060003400803001700020006060000280000419005000080079");
        var clone = grid.Clone();

        clone[0, 0].Value = 0;
        Assert.Equal(5, grid[0, 0].Value); // original unaffected
    }

    [Fact]
    public void InitializeCandidates_RemovesSolvedValues()
    {
        var grid = Grid.Parse("530070000600195000098000060800060003400803001700020006060000280000419005000080079");

        // Cell R1C3 (row 0, col 2) should not have 5 as candidate (5 is in row 0)
        Assert.False(grid[0, 2].Candidates.Contains(5));
        // Cell R1C3 should not have 9 as candidate (9 is in box)
        Assert.False(grid[0, 2].Candidates.Contains(9));
    }

    [Fact]
    public void PlaceValue_UpdatesPeers()
    {
        var grid = new Grid();
        grid.PlaceValue(grid[0, 0], 5);
        
        // All peers should no longer have 5 as candidate
        foreach (var peer in grid.Peers(grid[0, 0]))
        {
            if (!peer.IsSolved)
                Assert.False(peer.Candidates.Contains(5));
        }
    }
}
