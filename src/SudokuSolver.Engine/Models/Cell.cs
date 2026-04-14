namespace SudokuSolver.Engine.Models;

/// <summary>
/// Represents a single cell on the sudoku grid.
/// </summary>
public class Cell
{
    public int Row { get; }
    public int Column { get; }
    public int Box => (Row / 3) * 3 + (Column / 3);

    /// <summary>Solved value (1-9), or 0 if unsolved.</summary>
    public int Value { get; set; }

    /// <summary>Whether this cell was part of the original puzzle (given clue).</summary>
    public bool IsGiven { get; set; }

    /// <summary>Remaining candidate digits for an unsolved cell.</summary>
    public CandidateSet Candidates { get; set; } = CandidateSet.All;

    public bool IsSolved => Value != 0;

    public Cell(int row, int column)
    {
        if (row is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(row));
        if (column is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(column));
        Row = row;
        Column = column;
    }

    /// <summary>Row-Column label, e.g. "R3C7".</summary>
    public string Label => $"R{Row + 1}C{Column + 1}";

    public override string ToString() => IsSolved ? $"{Label}={Value}" : $"{Label}{Candidates}";
}
