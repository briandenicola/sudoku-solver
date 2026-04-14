# Sudoku Solver Tutor

A WPF application that teaches users how to solve sudoku puzzles step-by-step. Upload a photo of a puzzle, and the app extracts the grid using a local Ollama vision model, then walks you through the solution with detailed explanations of each solving technique.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com/) running locally (for image-to-grid extraction)
- A vision-capable model (e.g., `gemma4`)

## Getting Started

```bash
# Clone and build
dotnet build

# Run the application
dotnet run --project src/SudokuSolver.App

# Run tests
dotnet test
```

## Configuration

The Ollama endpoint and model are configurable in the application settings. Defaults:
- **URL**: `http://localhost:11434`
- **Model**: `gemma4`

## Project Structure

- **SudokuSolver.Engine** — Core solver library with solving techniques and step explanations
- **SudokuSolver.Vision** — Ollama integration for extracting puzzles from images
- **SudokuSolver.App** — WPF desktop application

## License

MIT
