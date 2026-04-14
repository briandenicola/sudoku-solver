namespace SudokuSolver.Engine.Models;

/// <summary>
/// Solving techniques ordered by difficulty.
/// The solver tries techniques in this order.
/// </summary>
public enum Technique
{
    NakedSingle,
    HiddenSingle,
    NakedPair,
    NakedTriple,
    NakedQuad,
    HiddenPair,
    HiddenTriple,
    HiddenQuad,
    PointingPair,
    BoxLineReduction,
    XWing,
    Swordfish,
    Jellyfish,
    XYWing,
    XYZWing,
    UniqueRectangle,
    SimpleColoring,
    AiAssisted,
    Backtracking
}
