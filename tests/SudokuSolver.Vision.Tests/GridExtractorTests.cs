using SudokuSolver.Vision;

namespace SudokuSolver.Vision.Tests;

public class GridExtractorTests
{
    [Fact]
    public void ParseResponse_ValidGrid_Succeeds()
    {
        var response = """
            5 3 0 0 7 0 0 0 0
            6 0 0 1 9 5 0 0 0
            0 9 8 0 0 0 0 6 0
            8 0 0 0 6 0 0 0 3
            4 0 0 8 0 3 0 0 1
            7 0 0 0 2 0 0 0 6
            0 6 0 0 0 0 2 8 0
            0 0 0 4 1 9 0 0 5
            0 0 0 0 8 0 0 7 9
            """;

        var result = GridExtractor.ParseResponse(response);

        Assert.True(result.Success);
        Assert.NotNull(result.Grid);
        Assert.Equal(5, result.Grid![0, 0].Value);
        Assert.Equal(0, result.Grid[0, 2].Value);
    }

    [Fact]
    public void ParseResponse_InvalidText_Fails()
    {
        var response = "I cannot read this image";
        var result = GridExtractor.ParseResponse(response);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ParseResponse_CommaDelimited_Succeeds()
    {
        var response = """
            5,3,0,0,7,0,0,0,0
            6,0,0,1,9,5,0,0,0
            0,9,8,0,0,0,0,6,0
            8,0,0,0,6,0,0,0,3
            4,0,0,8,0,3,0,0,1
            7,0,0,0,2,0,0,0,6
            0,6,0,0,0,0,2,8,0
            0,0,0,4,1,9,0,0,5
            0,0,0,0,8,0,0,7,9
            """;

        var result = GridExtractor.ParseResponse(response);
        Assert.True(result.Success);
    }
}
