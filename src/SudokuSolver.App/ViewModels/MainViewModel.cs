using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SudokuSolver.App.Services;
using SudokuSolver.Engine;
using SudokuSolver.Engine.Models;
using SudokuSolver.Vision;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
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
    private string ollamaModel = "gemma4";

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

    public ObservableCollection<string> AvailableModels { get; } = [];

    public ObservableCollection<StepSummaryItem> StepList { get; } = [];

    public MainViewModel()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        OllamaUrl = settings.OllamaUrl;
        OllamaModel = settings.OllamaModel;
        AutoPlaySpeedSeconds = settings.AutoPlaySpeedSeconds;
        UseAiAssist = settings.UseAiAssist;

        if (!string.IsNullOrWhiteSpace(settings.ExtractionPrompt))
            ExtractionPrompt = settings.ExtractionPrompt;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var settings = new UserSettings
        {
            OllamaUrl = OllamaUrl,
            OllamaModel = OllamaModel,
            AutoPlaySpeedSeconds = AutoPlaySpeedSeconds,
            UseAiAssist = UseAiAssist,
            ExtractionPrompt = ExtractionPrompt == GridExtractor.DefaultPrompt
                ? null
                : ExtractionPrompt
        };
        _settingsService.Save(settings);
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
                StatusMessage = "Puzzle extracted successfully. Click Solve to begin.";
            }
            else
            {
                StatusMessage = $"Failed to extract puzzle: {result.ErrorMessage}";
            }
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Could not connect to Ollama: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
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

            StatusMessage = _solveResult.IsSolved
                ? $"Solved in {_solveResult.Steps.Count} steps! Difficulty: {difficulty.Label} {difficulty.StarsDisplay}"
                : $"Solved {_solveResult.Steps.Count} steps but got stuck. The remaining cells require more advanced techniques.";
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

    private async Task TryAiAssistAsync(Grid grid)
    {
        try
        {
            var settings = new OllamaSettings
            {
                BaseUrl = OllamaUrl,
                Model = OllamaModel,
                TimeoutSeconds = 120
            };
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
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
        IsTestingConnection = true;
        ConnectionStatus = "Testing connection...";
        AvailableModels.Clear();

        try
        {
            var settings = new OllamaSettings
            {
                BaseUrl = OllamaUrl,
                Model = OllamaModel,
                TimeoutSeconds = 10
            };
            settings.Validate();

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var client = new OllamaClient(httpClient, settings);
            var models = await client.ListModelsAsync().ConfigureAwait(true);

            foreach (var model in models.OrderBy(m => m, StringComparer.OrdinalIgnoreCase))
                AvailableModels.Add(model);

            var modelMatch = models.Any(m =>
                m.StartsWith(OllamaModel, StringComparison.OrdinalIgnoreCase));

            if (modelMatch)
            {
                ConnectionStatus = $"✅ Connected — model '{OllamaModel}' is available. {models.Count} model(s) found.";
            }
            else
            {
                ConnectionStatus = $"⚠️ Connected ({models.Count} model(s) found), but '{OllamaModel}' is not installed. Select one from the list or pull it with: ollama pull {OllamaModel}";
            }
        }
        catch (InvalidOperationException ex)
        {
            ConnectionStatus = $"❌ Invalid settings: {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            ConnectionStatus = $"❌ Cannot reach Ollama at {OllamaUrl}: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ConnectionStatus = $"❌ Connection timed out. Is Ollama running at {OllamaUrl}?";
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
        var settings = new OllamaSettings
        {
            BaseUrl = OllamaUrl,
            Model = OllamaModel
        };
        var httpClient = new HttpClient();
        var ollamaClient = new OllamaClient(httpClient, settings);
        var prompt = string.IsNullOrWhiteSpace(ExtractionPrompt) ? null : ExtractionPrompt;
        _extractor = new GridExtractor(ollamaClient, prompt);
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
