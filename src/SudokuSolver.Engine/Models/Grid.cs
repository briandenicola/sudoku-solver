namespace SudokuSolver.Engine.Models;

/// <summary>
/// A 9×9 sudoku grid. Provides access to cells and unit queries (rows, columns, boxes).
/// </summary>
public class Grid
{
    private readonly Cell[,] _cells = new Cell[9, 9];

    public Grid()
    {
        for (var r = 0; r < 9; r++)
            for (var c = 0; c < 9; c++)
                _cells[r, c] = new Cell(r, c);
    }

    public Cell this[int row, int col] => _cells[row, col];

    /// <summary>
    /// Creates a grid from a flat array of 81 values (0 = empty, 1-9 = given).
    /// Row-major order: index = row * 9 + col.
    /// </summary>
    public static Grid FromValues(int[] values)
    {
        if (values.Length != 81)
            throw new ArgumentException("Expected exactly 81 values.", nameof(values));

        var grid = new Grid();
        for (var i = 0; i < 81; i++)
        {
            var v = values[i];
            if (v is < 0 or > 9)
                throw new ArgumentOutOfRangeException(nameof(values), v, $"Value at index {i} must be 0-9.");

            if (v != 0)
            {
                var cell = grid[i / 9, i % 9];
                cell.Value = v;
                cell.IsGiven = true;
                cell.Candidates = CandidateSet.Empty;
            }
        }

        grid.InitializeCandidates();
        return grid;
    }

    /// <summary>All cells in the given row (0-8).</summary>
    public IEnumerable<Cell> Row(int row)
    {
        for (var c = 0; c < 9; c++)
            yield return _cells[row, c];
    }

    /// <summary>All cells in the given column (0-8).</summary>
    public IEnumerable<Cell> Column(int col)
    {
        for (var r = 0; r < 9; r++)
            yield return _cells[r, col];
    }

    /// <summary>All cells in the given box (0-8), numbered left-to-right, top-to-bottom.</summary>
    public IEnumerable<Cell> Box(int box)
    {
        var startRow = (box / 3) * 3;
        var startCol = (box % 3) * 3;
        for (var r = startRow; r < startRow + 3; r++)
            for (var c = startCol; c < startCol + 3; c++)
                yield return _cells[r, c];
    }

    /// <summary>All 27 units (9 rows + 9 columns + 9 boxes).</summary>
    public IEnumerable<IEnumerable<Cell>> AllUnits()
    {
        for (var i = 0; i < 9; i++) yield return Row(i);
        for (var i = 0; i < 9; i++) yield return Column(i);
        for (var i = 0; i < 9; i++) yield return Box(i);
    }

    /// <summary>All cells that share a row, column, or box with the given cell (excluding itself).</summary>
    public IEnumerable<Cell> Peers(Cell cell) =>
        Row(cell.Row)
            .Union(Column(cell.Column))
            .Union(Box(cell.Box))
            .Where(c => c != cell)
            .Distinct();

    /// <summary>All 81 cells in row-major order.</summary>
    public IEnumerable<Cell> AllCells()
    {
        for (var r = 0; r < 9; r++)
            for (var c = 0; c < 9; c++)
                yield return _cells[r, c];
    }

    public bool IsSolved => AllCells().All(c => c.IsSolved);

    /// <summary>Removes solved values from candidates in peer cells.</summary>
    public void InitializeCandidates()
    {
        foreach (var cell in AllCells().Where(c => c.IsSolved))
        {
            foreach (var peer in Peers(cell))
            {
                if (!peer.IsSolved)
                    peer.Candidates = peer.Candidates.Remove(cell.Value);
            }
        }
    }

    /// <summary>
    /// Places a value in a cell and removes it from all peer candidates.
    /// </summary>
    public void PlaceValue(Cell cell, int value)
    {
        cell.Value = value;
        cell.Candidates = CandidateSet.Empty;

        foreach (var peer in Peers(cell))
        {
            if (!peer.IsSolved)
                peer.Candidates = peer.Candidates.Remove(value);
        }
    }

    /// <summary>Creates a deep copy of the grid.</summary>
    public Grid Clone()
    {
        var clone = new Grid();
        foreach (var cell in AllCells())
        {
            var target = clone[cell.Row, cell.Column];
            target.Value = cell.Value;
            target.IsGiven = cell.IsGiven;
            target.Candidates = cell.Candidates;
        }
        return clone;
    }

    /// <summary>
    /// Parses a single-line string of 81 characters (0 or . for empty).
    /// </summary>
    public static Grid Parse(string puzzle)
    {
        ArgumentNullException.ThrowIfNull(puzzle);

        var digits = puzzle.Where(c => c is (>= '0' and <= '9') or '.').ToArray();
        if (digits.Length != 81)
            throw new FormatException($"Expected 81 digits, got {digits.Length}.");

        var values = digits.Select(c => c == '.' ? 0 : c - '0').ToArray();
        return FromValues(values);
    }
}
