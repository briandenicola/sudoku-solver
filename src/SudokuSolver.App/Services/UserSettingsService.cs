using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SudokuSolver.Vision;

namespace SudokuSolver.App.Services;

public sealed class UserSettings
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";

    /// <summary>Legacy single-model field. Kept for backward-compat when loading older settings.</summary>
    public string? OllamaModel { get; set; }

    /// <summary>Model used for image extraction. Must support vision input.</summary>
    public string OllamaVisionModel { get; set; } = "gemma4:26b";

    /// <summary>Model used for AI Assist hints and Q&A chat. Reasoning-focused models work best.</summary>
    public string OllamaReasoningModel { get; set; } = "gemma4:26b";

    public int OllamaTimeoutSeconds { get; set; } = 300;
    public string? ExtractionPrompt { get; set; }
    public double AutoPlaySpeedSeconds { get; set; } = 2.0;
    public bool UseAiAssist { get; set; }
    public bool SaveChatHistory { get; set; } = true;
    public List<ChatMessageDto>? RecentChatMessages { get; set; }
}

/// <summary>
/// DTO for serializing chat messages to settings.
/// </summary>
public sealed class ChatMessageDto
{
    public required string Role { get; set; }
    public required string Content { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class UserSettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SudokuSolverTutor");

    private static readonly string SettingsFilePath = Path.Combine(
        SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return new UserSettings();

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions)
                   ?? new UserSettings();
        }
        catch
        {
            // If settings file is corrupted, return defaults
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Best-effort save — don't crash the app if write fails
        }
    }
}
