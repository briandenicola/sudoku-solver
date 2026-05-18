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

    [Fact]
    public void ParseResponse_DuplicateInRow_SucceedsWithWarning()
    {
        // Row 4 and row 5 are identical (and each contains all 9 digits) — clearly bogus extraction.
        var response = """
            0 0 9 1 8 2 6 5 4
            6 4 8 9 5 7 8 5 3
            8 5 7 3 9 4 1 2 7
            9 7 4 8 2 1 5 3 6
            9 7 4 8 2 1 5 3 6
            4 2 6 7 1 8 9 5 4
            5 9 7 4 2 6 8 1 8
            5 8 1 4 6 9 7 6 2
            5 8 1 4 6 9 7 6 2
            """;

        var result = GridExtractor.ParseResponse(response);

        Assert.True(result.Success);
        Assert.NotNull(result.Grid);
        Assert.NotNull(result.Warning);
        Assert.Contains("not a valid sudoku", result.Warning);
    }

    [Fact]
    public void ParseResponse_DuplicateInColumn_SucceedsWithWarning()
    {
        var response = """
            5 0 0 0 0 0 0 0 0
            5 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            """;

        var result = GridExtractor.ParseResponse(response);

        Assert.True(result.Success);
        Assert.NotNull(result.Grid);
        Assert.NotNull(result.Warning);
        Assert.Contains("column", result.Warning);
    }

    [Fact]
    public void ParseResponse_DuplicateInBox_SucceedsWithWarning()
    {
        var response = """
            5 0 0 0 0 0 0 0 0
            0 5 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0
            """;

        var result = GridExtractor.ParseResponse(response);

        Assert.True(result.Success);
        Assert.NotNull(result.Grid);
        Assert.NotNull(result.Warning);
        Assert.Contains("box", result.Warning);
    }

    [Fact]
    public void ParseResponse_PipeTableWithBlanks_Succeeds()
    {
        var response = """
            | 5 | 3 |   |   | 7 |   |   |   |   |
            | 6 |   |   | 1 | 9 | 5 |   |   |   |
            |   | 9 | 8 |   |   |   |   | 6 |   |
            | 8 |   |   |   | 6 |   |   |   | 3 |
            | 4 |   |   | 8 |   | 3 |   |   | 1 |
            | 7 |   |   |   | 2 |   |   |   | 6 |
            |   | 6 |   |   |   |   | 2 | 8 |   |
            |   |   |   | 4 | 1 | 9 |   |   | 5 |
            |   |   |   |   | 8 |   |   | 7 | 9 |
            """;

        var result = GridExtractor.ParseResponse(response);

        Assert.True(result.Success);
        Assert.NotNull(result.Grid);
        Assert.Equal(5, result.Grid![0, 0].Value);
        Assert.Equal(0, result.Grid[0, 2].Value);
        Assert.Equal(9, result.Grid[8, 8].Value);
    }

    [Fact]
    public void ParseResponse_PipeTableWithMarkdownHeader_Succeeds()
    {
        // Markdown table with a header separator row that should be ignored.
        var response = """
            | C1| C2| C3| C4| C5| C6| C7| C8| C9|
            |---|---|---|---|---|---|---|---|---|
            | 5 | 3 | 0 | 0 | 7 | 0 | 0 | 0 | 0 |
            | 6 | 0 | 0 | 1 | 9 | 5 | 0 | 0 | 0 |
            | 0 | 9 | 8 | 0 | 0 | 0 | 0 | 6 | 0 |
            | 8 | 0 | 0 | 0 | 6 | 0 | 0 | 0 | 3 |
            | 4 | 0 | 0 | 8 | 0 | 3 | 0 | 0 | 1 |
            | 7 | 0 | 0 | 0 | 2 | 0 | 0 | 0 | 6 |
            | 0 | 6 | 0 | 0 | 0 | 0 | 2 | 8 | 0 |
            | 0 | 0 | 0 | 4 | 1 | 9 | 0 | 0 | 5 |
            | 0 | 0 | 0 | 0 | 8 | 0 | 0 | 7 | 9 |
            """;

        var result = GridExtractor.ParseResponse(response);

        // The header row contains "C1", "C2" etc. which are not single digits,
        // so it's rejected by the cell parser. The 9 data rows below should parse.
        // Note: the C1/C2 strategy 1 (DigitLineRegex) won't match because they
        // contain letters, so we fall through to the pipe-table strategy which
        // correctly identifies and parses the 9 data rows.
        Assert.True(result.Success);
        Assert.NotNull(result.Grid);
    }

    [Fact]
    public void ParseResponse_Exactly81DigitsOneLine_Succeeds()
    {
        // Common when an OCR model dumps everything as a single digit stream.
        var response = "530070000600195000098000060800060003400803001700020006060000280000419005000080079";

        var result = GridExtractor.ParseResponse(response);

        Assert.True(result.Success);
        Assert.NotNull(result.Grid);
        Assert.Equal(5, result.Grid![0, 0].Value);
        // Last row is "000080079" → [8,8]=9, [8,7]=7, [8,4]=8
        Assert.Equal(9, result.Grid[8, 8].Value);
        Assert.Equal(7, result.Grid[8, 7].Value);
    }

    [Fact]
    public void ParseResponse_DigitsScatteredAcrossLines_Succeeds()
    {
        // Same 81 digits spread over arbitrary lines with extra spaces.
        var response = """
            Here is the puzzle:
            5 3 0 0 7 0 0 0 0
            6 0 0
            1 9 5 0 0 0
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
    }

    [Fact]
    public void ParseResponse_NotEnoughDigits_FailsWithRawResponseExcerpt()
    {
        var response = "I see a sudoku puzzle but cannot read all the digits clearly.";

        var result = GridExtractor.ParseResponse(response);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Raw model response", result.ErrorMessage);
    }
}
