using System.Windows;
using System.Windows.Threading;

namespace SudokuSolver.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly HashSet<string> _shownExceptions = new();
    private static readonly object _exceptionLock = new();

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatalDialog("UI thread exception", e.Exception);
        // Mark as handled so the app stays alive — the user can dismiss the dialog and try again.
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ShowFatalDialog("Background thread exception", ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowFatalDialog("Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    private static void ShowFatalDialog(string title, Exception ex)
    {
        // Dedup: if the same exception (type + first stack frame) has already been
        // shown, skip the dialog. Prevents WPF render-loop storms from spawning
        // hundreds of message boxes.
        var key = $"{ex.GetType().FullName}|{ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}";
        lock (_exceptionLock)
        {
            if (!_shownExceptions.Add(key))
                return;
        }

        var message = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        if (ex.InnerException is not null)
            message += $"\n\nInner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";

        Dialogs.MessageDialog.Show(message, title, Dialogs.MessageDialog.Severity.Error);
    }
}

