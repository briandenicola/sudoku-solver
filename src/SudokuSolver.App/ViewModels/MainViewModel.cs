using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SudokuSolver.Engine;
using SudokuSolver.Engine.Models;
using SudokuSolver.Vision;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows.Threading;

namespace SudokuSolver.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Solver _solver = new();
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

    public ObservableCollection<StepSummaryItem> StepList { get; } = [];

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
    private void Solve()
    {
        if (_originalGrid == null) return;

        // Re-clone from original so we can replay
        var grid = _originalGrid.Clone();
        _solveResult = _solver.Solve(grid);

        StepList.Clear();
        for (var i = 0; i < _solveResult.Steps.Count; i++)
        {
            StepList.Add(new StepSummaryItem(i + 1, _solveResult.Steps[i].Summary,
                _solveResult.Steps[i].Technique.ToString()));
        }

        TotalSteps = _solveResult.Steps.Count;

        // Reset to initial grid for step-through
        CurrentGrid = _originalGrid.Clone();
        CurrentStepIndex = -1;
        ClearHighlights();

        StatusMessage = _solveResult.IsSolved
            ? $"Solved in {_solveResult.Steps.Count} steps! Use Next/Previous to walk through."
            : $"Solved {_solveResult.Steps.Count} steps but got stuck. The remaining cells require more advanced techniques.";
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
    }

    private void ReplayToStep(int targetStep)
    {
        if (_solveResult == null || _originalGrid == null) return;

        // Replay from scratch to the target step
        var grid = _originalGrid.Clone();
        var solver = new Solver();
        var tempResult = solver.Solve(grid, maxSteps: targetStep + 1);

        CurrentGrid = grid;

        if (targetStep >= 0 && targetStep < _solveResult.Steps.Count)
        {
            var step = _solveResult.Steps[targetStep];
            HighlightedPatternCells = step.PatternCells;
            HighlightedAffectedCells = step.AffectedCells;
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
}

public record StepSummaryItem(int Number, string Summary, string Technique);
