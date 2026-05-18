using SudokuSolver.Engine.Models;
using SudokuSolver.Vision;

namespace SudokuSolver.Vision.Tests;

public class ChatServiceTests
{
    [Fact]
    public void BuildChatPrompt_WithoutContext_CreatesBasicPrompt()
    {
        var question = "What is a naked single?";

        var prompt = ChatService.BuildChatPrompt(question, null, null);

        Assert.Contains("sudoku reference", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(question, prompt);
        Assert.DoesNotContain("Current puzzle state", prompt);
        Assert.DoesNotContain("Steps completed", prompt);
    }

    [Fact]
    public void BuildChatPrompt_WithGrid_IncludesGridState()
    {
        var grid = Grid.Parse("530070000600195000098000060800060003400803001700020006060000280000419005000080079");
        var question = "Why can't I place a 5 in R1C3?";

        var prompt = ChatService.BuildChatPrompt(question, grid, null);

        Assert.Contains("Current puzzle state", prompt);
        Assert.Contains("5 3 0", prompt);  // First row of the grid
        Assert.Contains("Unsolved cells and their remaining candidates", prompt);
        Assert.Contains(question, prompt);
    }

    [Fact]
    public void BuildChatPrompt_WithSolveSteps_IncludesStepHistory()
    {
        var grid = Grid.Parse("530070000600195000098000060800060003400803001700020006060000280000419005000080079");
        var steps = new List<SolveStep>
        {
            new SolveStep
            {
                Technique = Technique.NakedSingle,
                SolvedCell = grid[0, 2],
                PlacedValue = 4,
                Summary = "Naked Single: R1C3 = 4",
                Explanation = "Test explanation"
            }
        };
        var question = "What technique should I use next?";

        var prompt = ChatService.BuildChatPrompt(question, grid, steps);

        Assert.Contains("Steps completed so far (1 steps)", prompt);
        Assert.Contains("Naked Single: R1C3 = 4", prompt);
        Assert.Contains(question, prompt);
    }

    [Fact]
    public void BuildChatPrompt_WithManySteps_OnlyShowsRecentSteps()
    {
        var grid = Grid.Parse("530070000600195000098000060800060003400803001700020006060000280000419005000080079");
        var steps = new List<SolveStep>();

        // Create 10 steps
        for (int i = 0; i < 10; i++)
        {
            steps.Add(new SolveStep
            {
                Technique = Technique.NakedSingle,
                Summary = $"Step {i + 1}",
                Explanation = "Test"
            });
        }

        var question = "What's next?";
        var prompt = ChatService.BuildChatPrompt(question, grid, steps);

        Assert.Contains("Steps completed so far (10 steps)", prompt);
        // Should contain last 5 steps (6-10)
        Assert.Contains("Step 6", prompt);
        Assert.Contains("Step 7", prompt);
        Assert.Contains("Step 8", prompt);
        Assert.Contains("Step 9", prompt);
        Assert.Contains("Step 10", prompt);
        // Should not contain early steps (1-5) - check for line breaks to avoid false positives
        Assert.DoesNotContain("  - Step 1\n", prompt);
        Assert.DoesNotContain("  - Step 2\n", prompt);
        Assert.DoesNotContain("  - Step 3\n", prompt);
    }

    [Fact]
    public void BuildChatPrompt_IncludesGuidelines()
    {
        var question = "Help me understand X-Wing";

        var prompt = ChatService.BuildChatPrompt(question, null, null);

        Assert.Contains("Rules:", prompt);
        Assert.Contains("direct, neutral tone", prompt);
        Assert.Contains("R3C7", prompt);  // Cell notation example
        Assert.DoesNotContain("encouraging", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskQuestionAsync_ThrowsOnEmptyQuestion()
    {
        var client = new OllamaClient(new HttpClient(), new OllamaSettings
        {
            BaseUrl = "http://localhost:11434",
            Model = "test"
        });
        var service = new ChatService(client);

        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.AskQuestionAsync("", null, null));
    }
}
