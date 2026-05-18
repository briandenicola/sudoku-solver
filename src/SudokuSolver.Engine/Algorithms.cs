using SudokuSolver.Engine.Models;

namespace SudokuSolver.Engine;

/// <summary>
/// Shared utility algorithms used across solving techniques.
/// </summary>
public static class Algorithms
{
    /// <summary>
    /// Generates all combinations of the specified size from the source list.
    /// Works with any type, including both Cell and int.
    /// </summary>
    public static IEnumerable<List<T>> Combinations<T>(List<T> source, int count)
    {
        if (count == 0)
        {
            yield return [];
            yield break;
        }

        for (var i = 0; i <= source.Count - count; i++)
        {
            foreach (var rest in Combinations(source[(i + 1)..], count - 1))
            {
                rest.Insert(0, source[i]);
                yield return rest;
            }
        }
    }

    /// <summary>
    /// Enumerates all units (rows, columns, boxes) in the grid.
    /// </summary>
    public static IEnumerable<(List<Cell> Cells, UnitType Type, int Index)> GetAllUnits(Grid grid)
    {
        for (var i = 0; i < 9; i++)
        {
            yield return (grid.Row(i).ToList(), UnitType.Row, i);
            yield return (grid.Column(i).ToList(), UnitType.Column, i);
            yield return (grid.Box(i).ToList(), UnitType.Box, i);
        }
    }

    /// <summary>
    /// Formats a unit name for display in explanations.
    /// </summary>
    public static string FormatUnitName(UnitType unitType, int index) => unitType switch
    {
        UnitType.Row => $"row {index + 1}",
        UnitType.Column => $"column {index + 1}",
        UnitType.Box => $"box {index + 1}",
        _ => throw new ArgumentOutOfRangeException(nameof(unitType))
    };
}