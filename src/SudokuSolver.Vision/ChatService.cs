using SudokuSolver.Engine.Models;
using System.Text;

namespace SudokuSolver.Vision;

/// <summary>
/// Provides Q&amp;A chat functionality for sudoku solving using an Ollama language model.
/// Users can ask questions about techniques, moves, and puzzle state.
/// </summary>
public class ChatService
{
    private readonly OllamaClient _client;

    public ChatService(OllamaClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Asks a question about the current puzzle state and solving process.
    /// </summary>
    /// <param name="question">The user's question.</param>
    /// <param name="grid">The current puzzle grid state.</param>
    /// <param name="solveSteps">The history of solve steps taken so far.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The AI assistant's response.</returns>
    public async Task<string> AskQuestionAsync(
        string question,
        Grid? grid = null,
        IReadOnlyList<SolveStep>? solveSteps = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var prompt = BuildChatPrompt(question, grid, solveSteps);

        try
        {
            var response = await _client.GenerateAsync(prompt, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(response)
                ? "I'm sorry, I couldn't generate a response. Please try again."
                : response;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return "I'm unable to connect to the AI service. Please check your Ollama settings and ensure the service is running.";
        }
        catch (Exception)
        {
            return "An error occurred while processing your question. Please try again.";
        }
    }

    internal static string BuildChatPrompt(
        string question,
        Grid? grid,
        IReadOnlyList<SolveStep>? solveSteps)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("You are a sudoku reference. Answer questions about the puzzle directly and factually.");
        prompt.AppendLine();
        prompt.AppendLine("Rules:");
        prompt.AppendLine("- Use a direct, neutral tone. No praise, encouragement, congratulations, or filler.");
        prompt.AppendLine("- Do not address the user as \"you\" in a coaching way. State facts about the grid.");
        prompt.AppendLine("- Do not open with phrases like \"Great job\", \"Nice work\", \"Let's\", or \"To understand...\". Start with the answer.");
        prompt.AppendLine("- Use cell notation R{row}C{col} (e.g., R3C7). Do not use bold/italic markdown.");
        prompt.AppendLine("- When explaining why a candidate is or isn't valid, cite the specific conflicting cell(s) and unit (row/column/box).");
        prompt.AppendLine("- Keep responses short: 1-3 short paragraphs maximum. Omit recap of the question.");
        prompt.AppendLine();

        // Add current grid state if available
        if (grid != null)
        {
            prompt.AppendLine("Current puzzle state (0 = empty):");
            for (var r = 0; r < 9; r++)
            {
                for (var c = 0; c < 9; c++)
                {
                    prompt.Append(grid[r, c].Value);
                    if (c < 8) prompt.Append(' ');
                }
                prompt.AppendLine();
            }
            prompt.AppendLine();

            // Add candidate information for unsolved cells
            var unsolvedCells = grid.AllCells().Where(c => !c.IsSolved).ToList();
            if (unsolvedCells.Count > 0)
            {
                prompt.AppendLine("Unsolved cells and their remaining candidates:");
                foreach (var cell in unsolvedCells)
                {
                    prompt.AppendLine($"  {cell.Label}: {cell.Candidates}");
                }
                prompt.AppendLine();
            }
        }

        // Add solve history if available
        if (solveSteps != null && solveSteps.Count > 0)
        {
            prompt.AppendLine($"Steps completed so far ({solveSteps.Count} steps):");
            var recentSteps = solveSteps.TakeLast(5);  // Only show last 5 steps for context
            foreach (var step in recentSteps)
            {
                prompt.AppendLine($"  - {step.Summary}");
            }
            prompt.AppendLine();
        }

        prompt.AppendLine($"Student's question: {question}");
        prompt.AppendLine();
        prompt.AppendLine("Your response:");

        return prompt.ToString();
    }
}
