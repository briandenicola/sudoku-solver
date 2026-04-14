namespace SudokuSolver.App.Services;

/// <summary>
/// Provides educational descriptions of each solving technique for the help panel.
/// </summary>
public static class TechniqueGuide
{
    public static IReadOnlyList<TechniqueInfo> AllTechniques { get; } =
    [
        new("Naked Single", "Beginner",
            "A cell has only one candidate left, so it must be that value.",
            "After eliminating all digits that already appear in the same row, column, and box, " +
            "only one possibility remains for this cell.\n\n" +
            "Example: If a cell's row contains 1,2,3,4 — its column contains 5,6 — and its box contains 7,8 — " +
            "then the only remaining candidate is 9.\n\n" +
            "This is the most fundamental technique and is often the first one beginners learn."),

        new("Hidden Single", "Beginner",
            "A digit can only go in one cell within a row, column, or box.",
            "Even though a cell may have multiple candidates, if a particular digit can only appear " +
            "in that one cell within its row, column, or box, then it must go there.\n\n" +
            "Example: In row 5, the digit 7 only appears as a candidate in R5C3. Even though R5C3 " +
            "might also have candidates {2,5,7}, it must be 7 because no other cell in row 5 can hold 7.\n\n" +
            "Look at each unit (row/column/box) and ask: 'Where can digit X go?' If there's only one spot, that's it."),

        new("Naked Pair", "Intermediate",
            "Two cells in a unit share the same two candidates — those digits are eliminated from other cells in the unit.",
            "When two cells in the same row, column, or box contain exactly the same two candidates " +
            "(and no others), those two digits must go in those two cells. They can be removed from " +
            "all other cells in that unit.\n\n" +
            "Example: In box 4, cells R4C1 and R5C2 both contain only {3,8}. One must be 3 and the other 8. " +
            "Therefore, 3 and 8 can be eliminated from every other unsolved cell in box 4.\n\n" +
            "This extends to Naked Triples (3 cells, 3 candidates) and Naked Quads (4 cells, 4 candidates)."),

        new("Naked Triple / Quad", "Intermediate",
            "Three or four cells in a unit collectively contain only three or four candidates.",
            "A Naked Triple occurs when three cells in a unit have candidates drawn from the same three digits. " +
            "Each cell doesn't need all three — for example, cells with {1,3}, {1,7}, and {3,7} form a naked triple " +
            "on digits {1,3,7}.\n\n" +
            "The same logic extends to four cells with four candidates (Naked Quad).\n\n" +
            "Those digits can be eliminated from all other cells in the unit, because the three (or four) cells " +
            "will consume all instances of those digits within the unit."),

        new("Hidden Pair", "Intermediate",
            "Two digits appear as candidates in only two cells within a unit — other candidates can be removed from those cells.",
            "When two digits are restricted to the same two cells in a row, column, or box, those two cells " +
            "must contain those two digits. All other candidates can be removed from those two cells.\n\n" +
            "Example: In column 7, digits 4 and 9 only appear as candidates in R2C7 and R6C7. " +
            "Even if those cells have other candidates like {1,4,6,9}, the extra digits (1 and 6) can be removed.\n\n" +
            "Hidden Triples and Hidden Quads work the same way with three or four digits."),

        new("Hidden Triple / Quad", "Intermediate",
            "Three or four digits appear only in three or four cells within a unit.",
            "This is the hidden counterpart to naked triples and quads. When three digits are confined to " +
            "exactly three cells in a unit (or four digits to four cells), all other candidates can be " +
            "eliminated from those cells.\n\n" +
            "These are harder to spot than naked subsets because the pattern is 'hidden' among other candidates. " +
            "Tip: if you can't find naked subsets, try looking for which digits are restricted to just a few cells."),

        new("Pointing Pair", "Intermediate",
            "A candidate in a box is restricted to a single row or column — it can be eliminated from that row/column outside the box.",
            "When a digit's candidates within a box all fall in the same row (or column), that digit " +
            "must appear in that row within that box. Therefore, the digit can be eliminated from " +
            "all other cells in that row outside the box.\n\n" +
            "Example: In box 1, digit 5 only appears in R1C2 and R1C3. Since 5 must be in row 1 within box 1, " +
            "we can eliminate 5 from R1C4 through R1C9.\n\n" +
            "Think of it as the box 'pointing' at the row or column where the digit must go."),

        new("Box/Line Reduction", "Intermediate",
            "A candidate in a row/column is restricted to a single box — it can be eliminated from other cells in that box.",
            "This is the reverse of Pointing Pair. When all occurrences of a digit in a row (or column) " +
            "fall within a single box, that digit must go in that box's portion of the row. " +
            "The digit can be eliminated from all other cells in that box.\n\n" +
            "Example: In row 3, digit 2 only appears in R3C4 and R3C6 (both in box 2). " +
            "Since 2 in row 3 must be in box 2, we can eliminate 2 from all other cells in box 2.\n\n" +
            "Pointing Pairs and Box/Line Reductions are two sides of the same coin — " +
            "the interaction between box constraints and line constraints."),

        new("X-Wing", "Advanced",
            "A digit forms a rectangle pattern across two rows and two columns, enabling eliminations.",
            "When a digit appears as a candidate in exactly two cells in each of two different rows, " +
            "and those cells align in the same two columns, an X-Wing is formed.\n\n" +
            "The digit must occupy one diagonal of the rectangle. Either way, it 'covers' both columns. " +
            "Therefore, the digit can be eliminated from all other cells in those two columns.\n\n" +
            "Example: Digit 6 in row 2 only appears in columns 3 and 7. Digit 6 in row 8 also only appears " +
            "in columns 3 and 7. This forms an X-Wing. Digit 6 can be eliminated from columns 3 and 7 " +
            "in all other rows.\n\n" +
            "The same logic works with columns as the base and rows as the cover lines."),

        new("Swordfish", "Advanced",
            "A 3×3 fish pattern — a digit in three rows is confined to three columns (or vice versa).",
            "Swordfish extends X-Wing to three rows and three columns. When a digit appears in exactly " +
            "two or three cells in each of three rows, and all those cells fall within the same three columns, " +
            "the digit can be eliminated from those three columns in all other rows.\n\n" +
            "Unlike X-Wing's clean rectangle, Swordfish cells don't all need to be present — " +
            "some corners of the 3×3 grid may be empty. The key is that the row candidates are " +
            "confined to those three columns.\n\n" +
            "This is significantly harder to spot visually. Focus on digits that appear in exactly 2-3 cells per row."),

        new("Jellyfish", "Advanced",
            "A 4×4 fish pattern — a digit in four rows is confined to four columns (or vice versa).",
            "Jellyfish extends the fish pattern to four rows and four columns. When a digit's candidates " +
            "in four rows are all confined to the same four columns, eliminations can be made.\n\n" +
            "These are rare in practice and very hard to spot manually. The solver checks for them " +
            "systematically by examining all combinations of four rows (or columns) that meet the criteria.\n\n" +
            "The logic is identical to X-Wing and Swordfish, just scaled up."),

        new("XY-Wing", "Advanced",
            "Three bi-value cells form a 'hinge and wings' pattern to eliminate a shared candidate.",
            "An XY-Wing uses three cells, each with exactly two candidates:\n" +
            "• Hinge cell: contains digits X and Y\n" +
            "• Wing 1: contains digits X and Z (sees the hinge)\n" +
            "• Wing 2: contains digits Y and Z (sees the hinge)\n\n" +
            "The key insight: if the hinge is X, then Wing 1 must be Z. If the hinge is Y, then Wing 2 must be Z. " +
            "Either way, at least one wing is Z.\n\n" +
            "Therefore, any cell that can see BOTH wings cannot be Z.\n\n" +
            "Note: the two wings don't need to see each other — they just both need to see the hinge."),

        new("XYZ-Wing", "Advanced",
            "Like XY-Wing but the hinge has three candidates — eliminations must see all three cells.",
            "An XYZ-Wing is similar to XY-Wing, but the hinge cell has three candidates (X, Y, Z) " +
            "instead of two:\n" +
            "• Hinge: {X, Y, Z}\n" +
            "• Wing 1: {X, Z} (sees the hinge)\n" +
            "• Wing 2: {Y, Z} (sees the hinge)\n\n" +
            "Since the hinge itself also contains Z, eliminations are more restricted: " +
            "Z can only be eliminated from cells that see ALL THREE pattern cells (hinge and both wings).\n\n" +
            "In practice, this means the eliminated cells must be in the same box as the hinge " +
            "and in the same row or column as a wing."),

        new("Unique Rectangle", "Advanced",
            "Exploits the fact that a valid sudoku has exactly one solution to avoid 'deadly patterns.'",
            "A Unique Rectangle (Type 1) involves four cells at the corners of a rectangle " +
            "spanning exactly two rows, two columns, and two boxes.\n\n" +
            "If three corners each contain only the same two candidates {A, B}, and the fourth corner " +
            "also contains A and B (plus other candidates), we know the fourth corner CANNOT be just {A, B}. " +
            "Otherwise, A and B could be swapped among all four corners, creating two valid solutions.\n\n" +
            "Since a proper sudoku has exactly one solution, we can eliminate A and B from the fourth corner.\n\n" +
            "This is sometimes called an 'avoidable deadly pattern' technique."),

        new("Simple Coloring", "Advanced",
            "Uses chains of conjugate pairs to color candidates and find contradictions or eliminations.",
            "For a single digit, conjugate pairs are units where the digit appears in exactly two cells. " +
            "These form a chain of strong links. Coloring alternates cells between two 'colors' along the chain.\n\n" +
            "Rule 1 (Contradiction): If two cells of the same color appear in the same unit, that color " +
            "is impossible — the digit is eliminated from ALL cells of that color.\n\n" +
            "Rule 2 (Both Colors Seen): If an uncolored cell sees cells of BOTH colors, the digit can be " +
            "eliminated from that cell. One of the two colors must be true, so the uncolored cell will " +
            "always see a cell that contains the digit.\n\n" +
            "This technique introduces chain-based reasoning, which is a gateway to more advanced strategies."),

        new("AI-Assisted", "Expert",
            "When all built-in techniques are exhausted, an AI model (Ollama) is consulted for the next move.",
            "This technique uses a locally-running large language model via Ollama to analyze the current " +
            "grid state and suggest the next cell placement.\n\n" +
            "The AI receives the full grid with all remaining candidates and is asked to apply advanced " +
            "techniques such as chains (X-Chain, XY-Chain, AIC), Almost Locked Sets, Sue de Coq, " +
            "and other strategies that aren't built into the deterministic solver.\n\n" +
            "AI suggestions are validated before being applied — the suggested value must be a legal candidate " +
            "that doesn't conflict with any peer cells. However, the AI's explanation of its reasoning " +
            "may not always be perfectly accurate.\n\n" +
            "Enable this in Settings under 'Use AI Assist (Ollama).' Requires a running Ollama server."),

        new("Backtracking", "Last Resort",
            "Systematic trial-and-error to guarantee a solution when all other techniques fail.",
            "Backtracking is a brute-force approach: pick an unsolved cell, try a candidate, and see if " +
            "it leads to a valid solution. If it reaches a dead end, undo the choice and try the next candidate.\n\n" +
            "The solver uses the Minimum Remaining Values (MRV) heuristic — it picks the cell with the " +
            "fewest candidates first, which dramatically reduces the search space.\n\n" +
            "While backtracking always finds the correct answer, it doesn't teach specific techniques. " +
            "Steps solved by backtracking indicate the puzzle may require strategies beyond what the " +
            "deterministic solver implements.\n\n" +
            "Backtracking is always used as the absolute last resort, after all logical techniques " +
            "(and optionally AI assistance) have been exhausted."),
    ];
}

public record TechniqueInfo(string Name, string Difficulty, string ShortDescription, string DetailedExplanation);
