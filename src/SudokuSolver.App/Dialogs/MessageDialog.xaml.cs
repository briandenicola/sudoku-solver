using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace SudokuSolver.App.Dialogs;

/// <summary>
/// Material Design styled modal dialog used in place of <see cref="MessageBox"/>
/// for application errors, warnings, and info messages. Visual style matches
/// <see cref="HelpWindow"/> (ColorZone header, Material Card body).
/// </summary>
public partial class MessageDialog : Window
{
    public enum Severity
    {
        Info,
        Warning,
        Error
    }

    public MessageDialog()
    {
        InitializeComponent();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// Shows a modal Material Design styled message dialog.
    /// </summary>
    /// <param name="message">Body text.</param>
    /// <param name="title">Window title and header text.</param>
    /// <param name="severity">Visual severity (icon + header color).</param>
    /// <param name="detailTitle">Optional secondary headline above the body text.</param>
    /// <param name="owner">Owner window for centering; falls back to the active window.</param>
    public static void Show(
        string message,
        string title = "Message",
        Severity severity = Severity.Info,
        string? detailTitle = null,
        Window? owner = null)
    {
        var dialog = new MessageDialog
        {
            Title = title,
            Owner = owner ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                          ?? Application.Current?.MainWindow
        };

        dialog.HeaderText.Text = title;
        dialog.DetailMessage.Text = message;

        if (!string.IsNullOrWhiteSpace(detailTitle))
        {
            dialog.DetailTitle.Text = detailTitle;
            dialog.DetailTitle.Visibility = Visibility.Visible;
        }

        ApplySeverity(dialog, severity);

        // Owner may be null if no window is open yet (e.g., early startup crash).
        // In that case Show modally without an owner.
        if (dialog.Owner == null)
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        dialog.ShowDialog();
    }

    private static void ApplySeverity(MessageDialog dialog, Severity severity)
    {
        switch (severity)
        {
            case Severity.Error:
                dialog.HeaderZone.Mode = ColorZoneMode.Custom;
                dialog.HeaderZone.Background = new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)); // Material Red 800
                dialog.HeaderZone.Foreground = Brushes.White;
                dialog.HeaderIcon.Kind = PackIconKind.AlertCircle;
                break;

            case Severity.Warning:
                dialog.HeaderZone.Mode = ColorZoneMode.Custom;
                dialog.HeaderZone.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x6C, 0x00)); // Material Orange 800
                dialog.HeaderZone.Foreground = Brushes.White;
                dialog.HeaderIcon.Kind = PackIconKind.Alert;
                break;

            case Severity.Info:
            default:
                dialog.HeaderZone.Mode = ColorZoneMode.PrimaryMid;
                dialog.HeaderIcon.Kind = PackIconKind.InformationOutline;
                break;
        }
    }
}
