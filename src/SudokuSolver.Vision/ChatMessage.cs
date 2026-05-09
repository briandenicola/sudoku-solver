namespace SudokuSolver.Vision;

/// <summary>
/// Represents a single message in a Q&amp;A chat conversation.
/// </summary>
public class ChatMessage
{
    /// <summary>The role of the message sender (user or assistant).</summary>
    public required MessageRole Role { get; init; }

    /// <summary>The message content.</summary>
    public required string Content { get; init; }

    /// <summary>When the message was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// The role of a message sender in a chat conversation.
/// </summary>
public enum MessageRole
{
    /// <summary>Message from the user asking a question.</summary>
    User,

    /// <summary>Message from the AI assistant.</summary>
    Assistant
}
