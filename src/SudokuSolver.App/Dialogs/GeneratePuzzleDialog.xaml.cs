using System.Windows;
using System.Windows.Controls;

namespace SudokuSolver.App.Dialogs;

public partial class GeneratePuzzleDialog : Window
{
    public int SelectedDifficulty { get; private set; } = 2;

    public GeneratePuzzleDialog()
    {
        InitializeComponent();
    }

    private void OnGenerateClick(object sender, RoutedEventArgs e)
    {
        if (DifficultyComboBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var difficulty))
        {
            SelectedDifficulty = difficulty;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
