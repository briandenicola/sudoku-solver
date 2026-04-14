# Sudoku Solver Tutor

A Windows desktop application that teaches you how to solve sudoku puzzles. Upload a photo of a puzzle (or enter one manually), and the app walks you through the solution step-by-step — explaining not just *what* technique was used, but *how* the pattern was identified.

Built with .NET 10, WPF, and Material Design.

## Features

- **Image extraction** — Upload a photo of a sudoku puzzle; a local Ollama vision model (e.g., Gemma 4) reads the grid
- **Manual entry** — Type or paste 81 digits directly
- **Step-by-step solving** — Walk through with Next/Previous or auto-play at adjustable speed
- **Detailed explanations** — Each step explains the reasoning (e.g., "In row 3, the digit 7 can only go in R3C4 because…")
- **9 solving techniques** — Naked Single, Hidden Single, Naked/Hidden Pairs/Triples/Quads, Pointing Pairs, Box/Line Reduction, X-Wing, Swordfish
- **Visual highlighting** — Pattern cells (green) and affected cells (red) are highlighted on the grid
- **Configurable Ollama** — Set the server URL, model, and extraction prompt from the Settings panel; test connection and browse installed models
- **Material Design UI** — Light theme with Indigo/Teal accent via MaterialDesignThemes

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Task](https://taskfile.dev/) (optional, for build automation)
- [Ollama](https://ollama.com/) running locally (only needed for image extraction)
- A vision-capable model pulled in Ollama (e.g., `ollama pull gemma4`)

## Getting Started

```bash
# Build and run all tests
task

# Run the application
task run

# Or without Task:
dotnet build
dotnet test
dotnet run --project src/SudokuSolver.App
```

### Available Tasks

| Task | Description |
|------|-------------|
| `task` | Build + test (default) |
| `task build` | Build the solution |
| `task test` | Run all tests |
| `task test-engine` | Engine tests only |
| `task test-vision` | Vision tests only |
| `task test-filter -- Name` | Run a single test by name |
| `task run` | Launch the app |
| `task publish` | Self-contained Windows executable |
| `task clean` | Remove build outputs |

## Configuration

All Ollama settings are configurable from the in-app Settings panel:

| Setting | Default | Description |
|---------|---------|-------------|
| Server URL | `http://localhost:11434` | Ollama API endpoint |
| Model | `gemma4` | Vision model for grid extraction |
| Extraction Prompt | (built-in) | Customizable prompt sent with the image |

Click **Test Connection** to verify the server is reachable and see which models are installed.

## Project Structure

```
├── src/
│   ├── SudokuSolver.Engine/      # Core solver library (no UI dependencies)
│   │   ├── Models/               # Grid, Cell, CandidateSet, SolveStep, Technique
│   │   ├── Techniques/           # One class per technique (ISolvingTechnique)
│   │   └── Solver.cs             # Orchestrates techniques in difficulty order
│   ├── SudokuSolver.Vision/      # Ollama API client and grid extraction
│   └── SudokuSolver.App/         # WPF application (Material Design, MVVM)
│       ├── Controls/             # Custom SudokuGridControl (owner-drawn)
│       ├── ViewModels/           # MainViewModel (CommunityToolkit.Mvvm)
│       └── Converters/           # WPF value converters
├── tests/
│   ├── SudokuSolver.Engine.Tests/
│   └── SudokuSolver.Vision.Tests/
├── Taskfile.yml
└── Directory.Build.props         # Shared build settings (nullable, warnings-as-errors)
```

## License

This project is licensed under the [MIT License](LICENSE).
