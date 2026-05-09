using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SudokuSolver.Engine.Models;
using SudokuSolver.Vision;
using System.Collections.ObjectModel;
using System.Net.Http;

namespace SudokuSolver.App.ViewModels;

/// <summary>
/// View model for the Q&amp;A chat panel.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private ChatService? _chatService;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> messages = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string currentMessage = "";

    [ObservableProperty]
    private bool isSending;

    [ObservableProperty]
    private bool isChatEnabled = true;

    /// <summary>
    /// Current puzzle grid for context.
    /// </summary>
    public Grid? CurrentGrid { get; set; }

    /// <summary>
    /// Solve steps history for context.
    /// </summary>
    public IReadOnlyList<SolveStep>? SolveSteps { get; set; }

    /// <summary>
    /// Initializes the chat service with Ollama settings.
    /// </summary>
    public void InitializeChatService(string ollamaUrl, string ollamaModel)
    {
        try
        {
            var settings = new OllamaSettings
            {
                BaseUrl = ollamaUrl,
                Model = ollamaModel
            };

            var httpClient = new HttpClient();
            var ollamaClient = new OllamaClient(httpClient, settings);
            _chatService = new ChatService(ollamaClient);
            IsChatEnabled = true;
        }
        catch
        {
            IsChatEnabled = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (_chatService == null || string.IsNullOrWhiteSpace(CurrentMessage))
            return;

        var userMessage = CurrentMessage.Trim();
        CurrentMessage = "";

        // Add user message to chat
        Messages.Add(new ChatMessage
        {
            Role = MessageRole.User,
            Content = userMessage
        });

        IsSending = true;

        try
        {
            // Get AI response with puzzle context
            var response = await _chatService.AskQuestionAsync(
                userMessage,
                CurrentGrid,
                SolveSteps);

            // Add assistant response to chat
            Messages.Add(new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = response
            });
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = $"Error: {ex.Message}"
            });
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSendMessage()
    {
        return !IsSending && !string.IsNullOrWhiteSpace(CurrentMessage) && IsChatEnabled;
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
    }
}
