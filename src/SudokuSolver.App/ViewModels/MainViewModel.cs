using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SudokuSolver.App.Dialogs;
using SudokuSolver.App.Services;
using SudokuSolver.Engine;
using SudokuSolver.Engine.Models;
using SudokuSolver.Vision;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SudokuSolver.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Solver _solver = new();
    private readonly UserSettingsService _settingsService = new();
    private GridExtractor? _extractor;
    private DispatcherTimer? _autoPlayTimer;
    private SolveResult? _solveResult;
    private Grid? _originalGrid;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SolveCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousStepCommand))]
    private Grid? currentGrid;

    [ObservableProperty]
    private IReadOnlyList<Cell>? highlightedPatternCells;

    [ObservableProperty]
    private IReadOnlyList<Cell>? highlightedAffectedCells;

    [ObservableProperty]
    private IReadOnlyList<CandidateHighlight>? highlightedCandidates;

    [ObservableProperty]
    private IReadOnlyList<Elimination>? eliminatedCandidates;

    [ObservableProperty]
    private string statusMessage = "Load a puzzle image or enter a puzzle manually to begin.";

    [ObservableProperty]
    private string currentExplanation = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousStepCommand))]
    private int currentStepIndex = -1;

    [ObservableProperty]
    private int totalSteps;

    [ObservableProperty]
    private string difficultyLabel = "";

    [ObservableProperty]
    private string difficultyStars = "";

    [ObservableProperty]
    private string difficultyBreakdown = "";

    [ObservableProperty]
    private bool isAutoPlaying;

    [ObservableProperty]
    private double autoPlaySpeedSeconds = 2.0;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string ollamaUrl = "http://localhost:11434";

    [ObservableProperty]
    private string ollamaVisionModel = "qwen3-vl:30b";

    [ObservableProperty]
    private string ollamaReasoningModel = "gemma4:26b";

    [ObservableProperty]
    private string ollamaCellModel = "gemma4";

    [ObservableProperty]
    private int ollamaTimeoutSeconds = 300;

    [ObservableProperty]
    private string extractionPrompt = GridExtractor.DefaultPrompt;

    [ObservableProperty]
    private string connectionStatus = "";

    [ObservableProperty]
    private bool isTestingConnection;

    [ObservableProperty]
    private bool useAiAssist;

    [ObservableProperty]
    private BitmapImage? puzzleImage;

    [ObservableProperty]
    private bool showCandidates;

    public ObservableCollection<string> AvailableModels { get; } = [];

    public ObservableCollection<StepSummaryItem> StepList { get; } = [];

    public ChatViewModel ChatViewModel { get; } = new();

    public MainViewModel()
    {
        LoadSettings();
        InitializeChatViewModel();
    }

    private void InitializeChatViewModel()
    {
        ChatViewModel.InitializeChatService(OllamaUrl, OllamaReasoningModel, OllamaTimeoutSeconds);
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        OllamaUrl = settings.OllamaUrl;

        // Migrate from legacy single-model setting: if vision/reasoning weren't
        // explicitly stored, fall back to the old OllamaModel value.
        var legacyModel = settings.OllamaModel;
        OllamaVisionModel = !string.IsNullOrWhiteSpace(settings.OllamaVisionModel)
            ? settings.OllamaVisionModel
            : (!string.IsNullOrWhiteSpace(legacyModel) ? legacyModel! : "gemma4:26b");
        OllamaReasoningModel = !string.IsNullOrWhiteSpace(settings.OllamaReasoningModel)
            ? settings.OllamaReasoningModel
            : (!string.IsNullOrWhiteSpace(legacyModel) ? legacyModel! : "gemma4:26b");
        OllamaCellModel = !string.IsNullOrWhiteSpace(settings.OllamaCellModel)
            ? settings.OllamaCellModel
            : "gemma4";

        OllamaTimeoutSeconds = settings.OllamaTimeoutSeconds;
        AutoPlaySpeedSeconds = settings.AutoPlaySpeedSeconds;
        UseAiAssist = settings.UseAiAssist;

        if (!string.IsNullOrWhiteSpace(settings.ExtractionPrompt))
            ExtractionPrompt = settings.ExtractionPrompt;

        // Load chat history if enabled
        if (settings.SaveChatHistory && settings.RecentChatMessages != null)
        {
            foreach (var dto in settings.RecentChatMessages)
            {
                ChatViewModel.Messages.Add(new ChatMessage
                {
                    Role = Enum.Parse<MessageRole>(dto.Role),
                    Content = dto.Content,
                    Timestamp = dto.Timestamp
                });
            }
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var settings = new UserSettings
        {
            OllamaUrl = OllamaUrl,
            OllamaVisionModel = OllamaVisionModel,
            OllamaReasoningModel = OllamaReasoningModel,
            OllamaCellModel = OllamaCellModel,
            OllamaTimeoutSeconds = OllamaTimeoutSeconds,
            AutoPlaySpeedSeconds = AutoPlaySpeedSeconds,
            UseAiAssist = UseAiAssist,
            ExtractionPrompt = ExtractionPrompt == GridExtractor.DefaultPrompt
                ? null
                : ExtractionPrompt,
            SaveChatHistory = true,
            RecentChatMessages = ChatViewModel.Messages
                .TakeLast(20)  // Save only last 20 messages
                .Select(m => new ChatMessageDto
                {
                    Role = m.Role.ToString(),
                    Content = m.Content,
                    Timestamp = m.Timestamp
                })
                .ToList()
        };
        _settingsService.Save(settings);

        // Reinitialize chat service with new settings
        InitializeChatViewModel();
        StatusMessage = "Settings saved.";
    }

    [RelayCommand]
    private async Task LoadImageAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusMessage = "Invalid file path.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Extracting puzzle from image...";

        try
        {
            PuzzleImage = LoadBitmapImage(filePath);
            EnsureExtractor();
            var result = await _extractor!.ExtractFromFileAsync(filePath);

            if (result.Success && result.Grid != null)
            {
                SetPuzzle(result.Grid);

                if (!string.IsNullOrWhiteSpace(result.Warning))
                {
                    StatusMessage = "Puzzle loaded with warnings — please review.";
                    MessageDialog.Show(
                        result.Warning,
                        "Extracted puzzle has conflicts",
                        MessageDialog.Severity.Warning);
                }
                else
                {
                    StatusMessage = "Puzzle extracted successfully. Click Solve to begin.";
                }
            }
            else
            {
                var errorText = result.ErrorMessage ?? "Unknown error.";
                StatusMessage = $"Failed to extract puzzle: {errorText}";
                MessageDialog.Show(
                    errorText,
                    "Could not extract puzzle",
                    MessageDialog.Severity.Warning,
                    detailTitle: "The AI was unable to read this puzzle.");
            }
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Could not connect to Ollama: {ex.Message}";
            MessageDialog.Show(
                ex.Message,
                "Connection error",
                MessageDialog.Severity.Error,
                detailTitle: "Could not connect to Ollama.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageDialog.Show(
                ex.Message,
                "Error loading image",
                MessageDialog.Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void LoadManualPuzzle(string puzzleString)
    {
        try
        {
            var grid = Grid.Parse(puzzleString);
            SetPuzzle(grid);
            StatusMessage = "Puzzle loaded. Click Solve to begin.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Invalid puzzle: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSolve))]
    private async Task SolveAsync()
    {
        if (_originalGrid == null) return;

        IsBusy = true;
        StatusMessage = "Solving puzzle...";

        try
        {
            // Re-clone from original so we can replay
            var grid = _originalGrid.Clone();
            _solveResult = _solver.Solve(grid);

            // If stuck and AI assist is enabled, try AI hints
            if (!_solveResult.IsSolved && UseAiAssist)
            {
                StatusMessage = "Logical techniques exhausted. Consulting AI...";
                await TryAiAssistAsync(grid).ConfigureAwait(true);
            }

            StepList.Clear();
            for (var i = 0; i < _solveResult.Steps.Count; i++)
            {
                StepList.Add(new StepSummaryItem(i + 1, _solveResult.Steps[i].Summary,
                    _solveResult.Steps[i].Technique.ToString()));
            }

            TotalSteps = _solveResult.Steps.Count;

            var difficulty = _solveResult.GetDifficulty();
            DifficultyLabel = difficulty.Label;
            DifficultyStars = difficulty.StarsDisplay;
            DifficultyBreakdown = difficulty.Breakdown;

            // Reset to initial grid for step-through
            CurrentGrid = _originalGrid.Clone();
            CurrentStepIndex = -1;
            ClearHighlights();

            // Once a solve has been computed, reveal candidates so the user can
            // see eliminations and the technique patterns as they step through.
            ShowCandidates = true;

            StatusMessage = _solveResult.IsSolved
                ? $"Solved in {_solveResult.Steps.Count} steps! Difficulty: {difficulty.Label} {difficulty.StarsDisplay}"
                : $"Solved {_solveResult.Steps.Count} steps but got stuck. The remaining cells require more advanced techniques.";

            // Update chat context with solve results
            UpdateChatContext();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during solve: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateChatContext()
    {
        ChatViewModel.CurrentGrid = CurrentGrid;
        ChatViewModel.SolveSteps = _solveResult?.Steps;
    }

    private async Task TryAiAssistAsync(Grid grid)
    {
        try
        {
            var timeoutSeconds = Math.Max(OllamaTimeoutSeconds, 1);
            var settings = new OllamaSettings
            {
                BaseUrl = OllamaUrl,
                Model = OllamaReasoningModel,
                TimeoutSeconds = timeoutSeconds
            };
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            var client = new OllamaClient(httpClient, settings);
            var aiService = new AiHintService(client);

            var allSteps = new List<SolveStep>(_solveResult!.Steps);
            var maxAiAttempts = 50;

            for (var attempt = 0; attempt < maxAiAttempts && !grid.IsSolved; attempt++)
            {
                StatusMessage = $"Consulting AI for hint ({attempt + 1})...";
                var aiStep = await aiService.GetHintAsync(grid).ConfigureAwait(true);
                if (aiStep == null) break;

                allSteps.Add(aiStep);

                // After AI placement, try deterministic techniques again
                var followUp = _solver.Solve(grid);
                allSteps.AddRange(followUp.Steps);

                if (grid.IsSolved) break;
            }

            _solveResult = new SolveResult(allSteps,
                grid.IsSolved ? SolveOutcome.Solved : SolveOutcome.Stuck);
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI assist failed: {ex.Message}. Showing deterministic results.";
        }
    }

    private bool CanSolve() => CurrentGrid != null;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextStep()
    {
        if (_solveResult == null || _originalGrid == null) return;

        CurrentStepIndex++;
        ReplayToStep(CurrentStepIndex);
    }

    private bool CanGoNext() => _solveResult != null && CurrentStepIndex < _solveResult.Steps.Count - 1;

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousStep()
    {
        if (_solveResult == null || _originalGrid == null) return;

        CurrentStepIndex--;
        if (CurrentStepIndex < 0)
        {
            CurrentGrid = _originalGrid.Clone();
            ClearHighlights();
            CurrentExplanation = "";
        }
        else
        {
            ReplayToStep(CurrentStepIndex);
        }
    }

    private bool CanGoPrevious() => CurrentStepIndex >= 0;

    [RelayCommand]
    private void ToggleAutoPlay()
    {
        if (IsAutoPlaying)
        {
            StopAutoPlay();
        }
        else
        {
            StartAutoPlay();
        }
    }

    [RelayCommand]
    private void Reset()
    {
        if (_originalGrid == null) return;
        StopAutoPlay();
        CurrentGrid = _originalGrid.Clone();
        _solveResult = null;
        CurrentStepIndex = -1;
        TotalSteps = 0;
        StepList.Clear();
        ClearHighlights();
        CurrentExplanation = "";
        StatusMessage = "Puzzle reset. Click Solve to begin.";
    }

    private void SetPuzzle(Grid grid)
    {
        StopAutoPlay();
        _originalGrid = grid.Clone();
        CurrentGrid = grid;
        _solveResult = null;
        CurrentStepIndex = -1;
        TotalSteps = 0;
        StepList.Clear();
        ClearHighlights();
        CurrentExplanation = "";
        DifficultyLabel = "";
        DifficultyStars = "";
        DifficultyBreakdown = "";
        // Start without pencil marks — let the user choose to reveal candidates
        // (or auto-reveal once they click Solve so step eliminations are visible).
        ShowCandidates = false;
    }

    private void ReplayToStep(int targetStep)
    {
        if (_solveResult == null || _originalGrid == null) return;

        // Replay from scratch to just before the target step, so the grid shows
        // the state before this step's eliminations — letting us highlight them
        var grid = _originalGrid.Clone();
        var solver = new Solver();
        solver.Solve(grid, maxSteps: targetStep);

        CurrentGrid = grid;

        if (targetStep >= 0 && targetStep < _solveResult.Steps.Count)
        {
            var step = _solveResult.Steps[targetStep];
            HighlightedPatternCells = step.PatternCells;
            HighlightedAffectedCells = step.AffectedCells;
            HighlightedCandidates = step.HighlightedCandidates;
            EliminatedCandidates = step.Eliminations;
            CurrentExplanation = step.Explanation;
            StatusMessage = $"Step {targetStep + 1}/{TotalSteps}: {step.Summary}";
        }

        NextStepCommand.NotifyCanExecuteChanged();
        PreviousStepCommand.NotifyCanExecuteChanged();
    }

    private void ClearHighlights()
    {
        HighlightedPatternCells = null;
        HighlightedAffectedCells = null;
        HighlightedCandidates = null;
        EliminatedCandidates = null;
    }

    partial void OnCurrentStepIndexChanged(int oldValue, int newValue)
    {
        // When the step list selection changes (e.g., user clicks a step), replay to that step
        if (_solveResult == null || _originalGrid == null) return;
        if (newValue == oldValue) return;

        if (newValue < 0)
        {
            CurrentGrid = _originalGrid.Clone();
            ClearHighlights();
            CurrentExplanation = "";
        }
        else if (newValue < _solveResult.Steps.Count)
        {
            ReplayToStep(newValue);
        }
    }

    private void StartAutoPlay()
    {
        if (_solveResult == null) return;
        IsAutoPlaying = true;
        _autoPlayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AutoPlaySpeedSeconds)
        };
        _autoPlayTimer.Tick += (_, _) =>
        {
            if (CanGoNext())
                NextStep();
            else
                StopAutoPlay();
        };
        _autoPlayTimer.Start();
    }

    private void StopAutoPlay()
    {
        IsAutoPlaying = false;
        _autoPlayTimer?.Stop();
        _autoPlayTimer = null;
    }

    [RelayCommand]
    private void ResetPrompt()
    {
        ExtractionPrompt = GridExtractor.DefaultPrompt;
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        // Capture desired models up-front. Mutating AvailableModels below can
        // cause the editable ComboBoxes to reset their Text (and therefore the
        // bound model properties) before validation runs.
        var desiredVision = OllamaVisionModel;
        var desiredReasoning = OllamaReasoningModel;
        var desiredUrl = OllamaUrl;

        IsTestingConnection = true;
        ConnectionStatus = "Testing connection...";

        try
        {
            var settings = new OllamaSettings
            {
                BaseUrl = desiredUrl,
                Model = string.IsNullOrWhiteSpace(desiredVision) ? desiredReasoning : desiredVision,
                TimeoutSeconds = 10
            };
            settings.Validate();

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var client = new OllamaClient(httpClient, settings);
            var models = await client.ListModelsAsync().ConfigureAwait(true);

            AvailableModels.Clear();
            foreach (var model in models.OrderBy(m => m, StringComparer.OrdinalIgnoreCase))
                AvailableModels.Add(model);

            // Restore selections — clearing/repopulating ItemsSource can wipe
            // the editable ComboBox text when the previously selected item is
            // momentarily absent from the collection.
            OllamaVisionModel = desiredVision;
            OllamaReasoningModel = desiredReasoning;

            bool ModelExists(string name) =>
                !string.IsNullOrWhiteSpace(name) &&
                models.Any(m => m.StartsWith(name, StringComparison.OrdinalIgnoreCase));

            var visionOk = ModelExists(desiredVision);
            var reasoningOk = ModelExists(desiredReasoning);

            var missing = new List<string>();
            if (!visionOk) missing.Add($"vision: '{desiredVision}'");
            if (!reasoningOk) missing.Add($"reasoning: '{desiredReasoning}'");

            if (missing.Count == 0)
            {
                ConnectionStatus = $"✅ Connected — both models available. {models.Count} model(s) found.";
            }
            else
            {
                ConnectionStatus = $"⚠️ Connected ({models.Count} model(s) found), but missing — {string.Join("; ", missing)}. Select from the dropdown or pull with: ollama pull <name>";
            }
        }
        catch (InvalidOperationException ex)
        {
            ConnectionStatus = $"❌ Invalid settings: {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            ConnectionStatus = $"❌ Cannot reach Ollama at {desiredUrl}: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ConnectionStatus = $"❌ Connection timed out. Is Ollama running at {desiredUrl}?";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    private void EnsureExtractor()
    {
        var timeoutSeconds = Math.Max(OllamaTimeoutSeconds, 1);
        var settings = new OllamaSettings
        {
            BaseUrl = OllamaUrl,
            Model = OllamaVisionModel,
            TimeoutSeconds = timeoutSeconds
        };
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        var ollamaClient = new OllamaClient(httpClient, settings);

        // Create a separate fast client for per-cell digit classification
        var cellSettings = new OllamaSettings
        {
            BaseUrl = OllamaUrl,
            Model = OllamaCellModel,
            TimeoutSeconds = 30
        };
        var cellHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var cellClient = new OllamaClient(cellHttpClient, cellSettings);

        var prompt = string.IsNullOrWhiteSpace(ExtractionPrompt) ? null : ExtractionPrompt;
        _extractor = new GridExtractor(ollamaClient, cellClient, prompt);
    }

    private static BitmapImage LoadBitmapImage(string filePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}

public record StepSummaryItem(int Number, string Summary, string Technique);
