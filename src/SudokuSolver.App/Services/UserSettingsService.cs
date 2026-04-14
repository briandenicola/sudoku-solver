using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SudokuSolver.App.Services;

public sealed class UserSettings
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "gemma4";
    public string? ExtractionPrompt { get; set; }
    public double AutoPlaySpeedSeconds { get; set; } = 2.0;
    public bool UseAiAssist { get; set; }
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
