# Copilot Instructions for Sudoku Solver Tutor

## Build, Test, and Lint

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run a single test by name
dotnet test --filter "FullyQualifiedName~SolverTests.Solve_EasyPuzzle_Completes"

# Run tests in a specific project
dotnet test tests/SudokuSolver.Engine.Tests

# Run the WPF application
dotnet run --project src/SudokuSolver.App
```

Warnings are treated as errors (`TreatWarningsAsErrors` in Directory.Build.props). All projects use nullable reference types.

## Architecture

This is a **.NET 10 WPF application** that teaches sudoku solving by explaining each step in detail.

### Three-Project Structure

- **SudokuSolver.Engine** — Pure solver library with zero UI dependencies. Contains the grid model, candidate tracking, and all solving techniques. This is the core of the app and must remain UI-free and independently testable.
- **SudokuSolver.Vision** — Ollama API integration for extracting sudoku grids from photos using vision models. Depends on Engine for the `Grid` model.
- **SudokuSolver.App** — WPF desktop application (MVVM via CommunityToolkit.Mvvm). Depends on both Engine and Vision.

### Solver Engine Design

The solver uses a **technique chain pattern**:

1. Each technique implements `ISolvingTechnique` with a single method: `SolveStep? TryApply(Grid grid)`.
2. Techniques are tried in difficulty order (Naked Single → Swordfish). The solver calls each until one returns a step.
3. Every `SolveStep` includes: the technique used, affected cells, candidate eliminations, pattern cells (for UI highlighting), a short summary, and a **detailed explanation** of how the pattern was detected.
4. The grid is mutated in place as techniques are applied.

**When adding a new technique**: Create a class implementing `ISolvingTechnique` in `src/SudokuSolver.Engine/Techniques/`, then register it in `Solver.DefaultTechniques()` in the correct difficulty position. Write the `Explanation` to teach the user *how* the pattern was found, not just *what* it is.

### Key Models

- `Grid` — 9×9 cell array with unit queries (rows, columns, boxes, peers). Supports `Parse()` from 81-character strings (0 or . for empty).
- `Cell` — Row, Column, Box, Value, Candidates, IsGiven.
- `CandidateSet` — Bitmask-based set of digits 1-9 with set operations (union, intersect, except).
- `SolveStep` — Immutable record of a single solving action with full explanation.

### Ollama Vision Integration

- `OllamaClient` wraps the Ollama REST API (`/api/generate` endpoint) with base64 image support.
- `GridExtractor` sends a structured prompt and parses the 9×9 digit response.
- `OllamaSettings` holds URL, model name, and timeout — all configurable by the user at runtime.
- The `ParseResponse` method is `internal` (visible to tests via `InternalsVisibleTo`).

### WPF App Conventions

- **MVVM pattern** using `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).
- `SudokuGridControl` is a custom owner-drawn WPF control (overrides `OnRender`) — not XAML-templated.
- In the App project, `SudokuGrid` is aliased as `using SudokuGrid = SudokuSolver.Engine.Models.Grid` to avoid conflict with `System.Windows.Controls.Grid`.
- Step replay works by cloning the original grid and re-solving to the target step index.

## Conventions

- **Nullable reference types** are enabled solution-wide. Don't suppress nullable warnings without justification.
- **Async/await** for all I/O operations (Ollama API calls, file reads). Use `ConfigureAwait(false)` in library code.
- **No secrets in source** — Ollama URL/model are user settings, not hardcoded credentials.
- Technique explanations must explain the *reasoning process* (e.g., "Looking at row 3, the digit 7 can only appear in R3C4 because..."), not just state the result.
- `CandidateSet` is a value type (struct) — always use value semantics (methods return new instances).
