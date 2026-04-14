namespace SudokuSolver.Vision.Tests;

public class OllamaSettingsTests
{
    [Fact]
    public void Validate_DefaultSettings_Passes()
    {
        var settings = new OllamaSettings();
        settings.Validate(); // should not throw
    }

    [Fact]
    public void Validate_EmptyUrl_Throws()
    {
        var settings = new OllamaSettings { BaseUrl = "" };
        Assert.Throws<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void Validate_InvalidUrl_Throws()
    {
        var settings = new OllamaSettings { BaseUrl = "not-a-url" };
        Assert.Throws<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void Validate_EmptyModel_Throws()
    {
        var settings = new OllamaSettings { Model = "" };
        Assert.Throws<InvalidOperationException>(() => settings.Validate());
    }

    [Fact]
    public void Validate_NegativeTimeout_Throws()
    {
        var settings = new OllamaSettings { TimeoutSeconds = -1 };
        Assert.Throws<InvalidOperationException>(() => settings.Validate());
    }
}
