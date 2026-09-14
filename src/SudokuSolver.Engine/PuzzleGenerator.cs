using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine;

/// <summary>
/// Generates sudoku puzzles with a unique solution and a target difficulty
/// aligned to <see cref="SolveResult.GetDifficulty"/>.
/// </summary>
public sealed class PuzzleGenerator
{
    private const int Cells = 81;
    private static readonly IReadOnlyDictionary<int, string> FallbackTemplates = new Dictionary<int, string>
    {
        [1] = "530070000600195000098000060800060003400803001700020006060000280000419005000080079",
        [2] = "030000900600100040108000060059701003406853701700904850060000204080009005005000070",
        [3] = "000000820107050600860000107010980706709060403306075010602000078008090301031000000",
        [4] = "318507060402009007570000000840090000051708340000050098000000075600100203080605419",
        [5] = "002040030064705080003020600700009000409000706000600005001090300040503190090010400",
    };

    private readonly Random _random;
    private readonly Solver _solver;

    public PuzzleGenerator() : this(Random.Shared, new Solver()) { }

    public PuzzleGenerator(int seed) : this(new Random(seed), new Solver()) { }

    private PuzzleGenerator(Random random, Solver solver)
    {
        _random = random;
        _solver = solver;
    }

    /// <summary>
    /// Generates a puzzle whose solved-step difficulty rating matches the requested star rating.
    /// </summary>
    /// <param name="targetStars">Difficulty from 1 (Beginner) to 5 (Expert/Diabolical).</param>
    /// <param name="maxAttempts">Maximum complete-grid attempts before giving up.</param>
    public GeneratedPuzzle Generate(int targetStars, int maxAttempts = 80)
    {
        if (targetStars is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(targetStars), targetStars, "Difficulty must be from 1 to 5.");
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Attempt count must be positive.");

        var profile = DifficultyProfile.ForStars(targetStars);

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var values = GenerateSolvedGrid();
            var puzzle = (int[])values.Clone();
            var pairs = SymmetricCellPairs().OrderBy(_ => _random.Next()).ToList();

            foreach (var pair in pairs)
            {
                var first = pair.First;
                var second = pair.Second;
                var removedFirst = puzzle[first];
                var removedSecond = puzzle[second];
                if (removedFirst == 0 && removedSecond == 0)
                    continue;

                puzzle[first] = 0;
                puzzle[second] = 0;

                if (CountClues(puzzle) < profile.MinClues || !HasUniqueSolution(puzzle))
                {
                    puzzle[first] = removedFirst;
                    puzzle[second] = removedSecond;
                    continue;
                }

                var generated = TryRatePuzzle(puzzle, targetStars, profile, enforceClueProfile: true);
                if (generated != null)
                    return generated;
            }

            var finalGenerated = TryRatePuzzle(puzzle, targetStars, profile, enforceClueProfile: true);
            if (finalGenerated != null)
                return finalGenerated;
        }

        return GenerateFromTemplate(targetStars);
    }

    private GeneratedPuzzle GenerateFromTemplate(int targetStars)
    {
        var template = FallbackTemplates[targetStars];

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var transformed = TransformPuzzle(Grid.Parse(template));
            var generated = TryRatePuzzle(
                transformed,
                targetStars,
                DifficultyProfile.ForStars(targetStars),
                enforceClueProfile: false);

            if (generated != null)
                return generated;
        }

        throw new InvalidOperationException($"Could not generate a difficulty {targetStars} puzzle. Try again.");
    }

    private GeneratedPuzzle? TryRatePuzzle(
        int[] puzzle,
        int targetStars,
        DifficultyProfile profile,
        bool enforceClueProfile)
    {
        var clueCount = CountClues(puzzle);
        if (enforceClueProfile && (clueCount < profile.MinClues || clueCount > profile.MaxClues))
            return null;

        var grid = Grid.FromValues(puzzle);
        var solveGrid = grid.Clone();
        var result = _solver.Solve(solveGrid);
        if (!result.IsSolved)
            return null;

        var difficulty = result.GetDifficulty();
        return difficulty.Stars == targetStars
            ? new GeneratedPuzzle(grid, difficulty, result)
            : null;
    }

    private int[] TransformPuzzle(Grid template)
    {
        var digitMap = ShuffledDigits();
        var rowMap = ShuffledUnitMap();
        var colMap = ShuffledUnitMap();
        var transpose = _random.Next(2) == 0;
        var values = new int[Cells];

        foreach (var cell in template.AllCells())
        {
            if (cell.Value == 0)
                continue;

            var sourceRow = transpose ? cell.Column : cell.Row;
            var sourceCol = transpose ? cell.Row : cell.Column;
            var targetRow = rowMap[sourceRow];
            var targetCol = colMap[sourceCol];
            values[targetRow * 9 + targetCol] = digitMap[cell.Value - 1];
        }

        return values;
    }

    private int[] ShuffledUnitMap()
    {
        var map = new int[9];
        var bands = Enumerable.Range(0, 3).OrderBy(_ => _random.Next()).ToArray();

        for (var bandIndex = 0; bandIndex < bands.Length; bandIndex++)
        {
            var rows = Enumerable.Range(0, 3).OrderBy(_ => _random.Next()).ToArray();
            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var source = bands[bandIndex] * 3 + rows[rowIndex];
                var target = bandIndex * 3 + rowIndex;
                map[source] = target;
            }
        }

        return map;
    }

    private int[] GenerateSolvedGrid()
    {
        var values = new int[Cells];
        if (!FillGrid(values))
            throw new InvalidOperationException("Failed to generate a complete sudoku grid.");
        return values;
    }

    private bool FillGrid(int[] values)
    {
        var empty = SelectMostConstrainedCell(values);
        if (empty < 0)
            return true;

        foreach (var digit in ShuffledDigits())
        {
            if (!CanPlace(values, empty, digit))
                continue;

            values[empty] = digit;
            if (FillGrid(values))
                return true;
            values[empty] = 0;
        }

        return false;
    }

    private int[] ShuffledDigits()
    {
        var digits = Enumerable.Range(1, 9).ToArray();
        for (var i = digits.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (digits[i], digits[j]) = (digits[j], digits[i]);
        }
        return digits;
    }

    private static int SelectMostConstrainedCell(int[] values)
    {
        var bestIndex = -1;
        var bestCount = 10;

        for (var i = 0; i < Cells; i++)
        {
            if (values[i] != 0)
                continue;

            var count = 0;
            for (var digit = 1; digit <= 9; digit++)
            {
                if (CanPlace(values, i, digit))
                    count++;
            }

            if (count < bestCount)
            {
                bestIndex = i;
                bestCount = count;
                if (bestCount == 1)
                    break;
            }
        }

        return bestIndex;
    }

    private static bool HasUniqueSolution(int[] puzzle)
    {
        var values = (int[])puzzle.Clone();
        var solutionCount = 0;
        CountSolutions(values, ref solutionCount, maxSolutions: 2);
        return solutionCount == 1;
    }

    private static void CountSolutions(int[] values, ref int solutionCount, int maxSolutions)
    {
        if (solutionCount >= maxSolutions)
            return;

        var empty = SelectMostConstrainedCell(values);
        if (empty < 0)
        {
            solutionCount++;
            return;
        }

        for (var digit = 1; digit <= 9; digit++)
        {
            if (!CanPlace(values, empty, digit))
                continue;

            values[empty] = digit;
            CountSolutions(values, ref solutionCount, maxSolutions);
            values[empty] = 0;

            if (solutionCount >= maxSolutions)
                return;
        }
    }

    private static bool CanPlace(int[] values, int index, int digit)
    {
        var row = index / 9;
        var col = index % 9;

        for (var c = 0; c < 9; c++)
        {
            if (values[row * 9 + c] == digit)
                return false;
        }

        for (var r = 0; r < 9; r++)
        {
            if (values[r * 9 + col] == digit)
                return false;
        }

        var boxRow = (row / 3) * 3;
        var boxCol = (col / 3) * 3;
        for (var r = boxRow; r < boxRow + 3; r++)
        {
            for (var c = boxCol; c < boxCol + 3; c++)
            {
                if (values[r * 9 + c] == digit)
                    return false;
            }
        }

        return true;
    }

    private static int CountClues(int[] values) => values.Count(v => v != 0);

    private static IEnumerable<(int First, int Second)> SymmetricCellPairs()
    {
        for (var i = 0; i < 41; i++)
            yield return (i, Cells - 1 - i);
    }

    private sealed record DifficultyProfile(int MinClues, int MaxClues)
    {
        public static DifficultyProfile ForStars(int stars) => stars switch
        {
            1 => new DifficultyProfile(52, 60),
            2 => new DifficultyProfile(44, 54),
            3 => new DifficultyProfile(34, 46),
            4 => new DifficultyProfile(28, 38),
            5 => new DifficultyProfile(24, 32),
            _ => throw new ArgumentOutOfRangeException(nameof(stars), stars, "Difficulty must be from 1 to 5.")
        };
    }
}

public sealed record GeneratedPuzzle(Grid Grid, DifficultyRating Difficulty, SolveResult SolveResult);
