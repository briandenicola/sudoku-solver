# Q&A Chat Feature

## Overview

The Sudoku Solver Tutor now includes an interactive Q&A chat feature that allows you to ask questions about sudoku solving techniques, specific moves, and puzzle state. The chat is powered by Ollama and provides context-aware responses based on your current puzzle and solving progress.

## Getting Started

### Prerequisites

1. **Ollama Installation**: Install [Ollama](https://ollama.ai/) on your local machine
2. **Model Download**: Download a language model (default: `gemma4`)
   ```bash
   ollama pull gemma4
   ```
3. **Ollama Service**: Ensure Ollama is running (default: `http://localhost:11434`)

### Accessing the Chat

1. Load or enter a sudoku puzzle
2. Navigate to the right panel
3. Click the **Q&A** tab (next to "Explanation")
4. Type your question and press Enter or click Send

## Features

### Context-Aware Responses

The AI assistant has access to:
- **Current grid state**: All cell values and remaining candidates
- **Solve history**: The last 5 steps taken (technique, summary, cells affected)
- **Puzzle difficulty**: The computed difficulty rating

This context allows the assistant to provide specific, relevant answers about your puzzle.

### Example Questions

**Understanding Techniques:**
- "What is a naked single?"
- "How does the X-Wing technique work?"
- "Explain the difference between naked pairs and hidden pairs"

**Current Puzzle Analysis:**
- "What technique should I use next?"
- "Why can't I place a 5 in R3C7?"
- "Which cells have the fewest candidates?"
- "Is there a naked pair in row 4?"

**Specific Move Validation:**
- "Can I place a 9 in R2C5?"
- "Why doesn't this move work: R6C3 = 4"
- "What would happen if I place a 7 here?"

**Learning & Strategy:**
- "How do I identify when to use box-line reduction?"
- "What should I look for when stuck?"
- "Explain the logic behind the last step"

## UI Elements

### Chat Panel Components

1. **Message History**
   - User messages appear on the right in blue bubbles
   - AI responses appear on the left in gray bubbles
   - Auto-scrolls to show newest messages

2. **Input Field**
   - Type your question (max 500 characters)
   - Press Enter to send
   - Click the Send button (paper plane icon)

3. **Clear Button**
   - Trash icon in the top-right
   - Clears all chat messages
   - Does not clear saved history

4. **Loading Indicator**
   - Progress bar appears while AI is thinking
   - Input is disabled during processing

### Settings

Access chat settings via the Settings panel:

- **Ollama URL**: Base URL for Ollama service (default: `http://localhost:11434`)
- **Ollama Model**: Model to use for chat (default: `gemma4`)
- **AI Assist**: Enable/disable AI assistance when solver gets stuck

## Chat History Persistence

### Automatic Saving

- The last 20 chat messages are automatically saved
- History persists between app sessions
- Saved to: `%LOCALAPPDATA%/SudokuSolverTutor/settings.json`

### Manual Control

- **Clear Chat**: Removes messages from current session (persisted on next save)
- **Disable Persistence**: Set `SaveChatHistory: false` in settings.json

## Technical Details

### Architecture

The Q&A feature consists of three main components:

1. **ChatService** (Vision project)
   - Builds context-aware prompts with puzzle state
   - Handles Ollama API communication
   - Error handling and timeout management

2. **ChatViewModel** (App project)
   - MVVM pattern for UI binding
   - Manages message collection
   - Executes send commands

3. **ChatPanel** (App project)
   - WPF user control with Material Design
   - Message bubbles with role-based styling
   - Responsive input and display

### Prompt Engineering

The chat prompt includes:

```
You are an expert sudoku tutor helping a student...

Guidelines:
- Be encouraging and educational
- Explain techniques clearly with step-by-step reasoning
- Use cell notation like R3C7 (Row 3, Column 7)
- When explaining why a move doesn't work, be specific
- Keep responses concise but thorough (2-4 paragraphs)

Current puzzle state: [9x9 grid with candidates]
Steps completed: [last 5 steps with summaries]
Student's question: [user input]
```

### Error Handling

- **Ollama Offline**: "I'm unable to connect to the AI service..."
- **Invalid Response**: "I'm sorry, I couldn't generate a response..."
- **Network Timeout**: Configurable timeout (default: 120 seconds)

## Tips for Best Results

1. **Be Specific**: Reference cells by notation (e.g., "R3C7" instead of "that cell")
2. **Provide Context**: Mention the technique or step you're asking about
3. **Ask Follow-ups**: Build on previous responses for deeper understanding
4. **Load Puzzle First**: The AI provides better answers with puzzle context

## Troubleshooting

### Chat Not Working

1. **Check Ollama Status**
   ```bash
   ollama list
   ```
   Should show installed models

2. **Verify Connection**
   - Click "Test Connection" in Settings
   - Should show "✓ Connected to Ollama"

3. **Check Model**
   - Ensure the model in settings is downloaded
   - Default model: `gemma4`

### Slow Responses

- **Model Size**: Larger models (e.g., `llama2:70b`) take longer
- **Hardware**: CPU-only inference is slower than GPU
- **Context Length**: More solve steps = longer prompts

### Empty Responses

- Try asking a more specific question
- Ensure Ollama service is running
- Check timeout settings (increase if needed)

## Limitations

- **Context Window**: Only last 5 solve steps included in context
- **No Vision**: Chat cannot "see" the grid visually (uses text representation)
- **Model Dependent**: Response quality depends on the Ollama model used
- **No Verification**: AI suggestions are not automatically validated

## Privacy & Data

- **Local Processing**: All chat processing happens on your local machine via Ollama
- **No Cloud Sending**: Your puzzles and questions never leave your computer
- **Settings Storage**: Chat history stored locally in user settings file

## Future Enhancements

Potential improvements for future versions:

- [ ] Voice input for questions
- [ ] Export chat history to file
- [ ] Suggested questions based on puzzle state
- [ ] Interactive hints (click cell to ask about it)
- [ ] Multi-turn conversation memory
- [ ] Custom prompt templates

## Feedback

Found a bug or have a feature request? Please report it on the [GitHub Issues](https://github.com/briandenicola/sudoku-solver/issues) page.
