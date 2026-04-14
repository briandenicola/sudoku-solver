using System.Windows;
using Microsoft.Win32;
using SudokuSolver.App.ViewModels;

namespace SudokuSolver.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnLoadImageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Sudoku Puzzle Image",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            await ViewModel.LoadImageCommand.ExecuteAsync(dialog.FileName);
        }
    }

    private void OnManualEntryClick(object sender, RoutedEventArgs e)
    {
        ManualEntryPanel.Visibility = ManualEntryPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnLoadManualClick(object sender, RoutedEventArgs e)
    {
        var text = ManualEntryBox.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            ViewModel.LoadManualPuzzleCommand.Execute(text);
            ManualEntryPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnHelpClick(object sender, RoutedEventArgs e)
    {
        var help = new HelpWindow { Owner = this };
        help.Show();
    }
}