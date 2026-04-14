using System.Windows;
using System.Windows.Controls;
using SudokuSolver.App.Services;

namespace SudokuSolver.App;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        TechniqueList.ItemsSource = TechniqueGuide.AllTechniques;

        if (TechniqueGuide.AllTechniques.Count > 0)
            TechniqueList.SelectedIndex = 0;
    }

    private void OnTechniqueSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TechniqueList.SelectedItem is TechniqueInfo info)
        {
            DetailTitle.Text = info.Name;
            DetailDifficulty.Text = info.Difficulty;
            DetailShort.Text = info.ShortDescription;
            DetailExplanation.Text = info.DetailedExplanation;
            DetailPanel.Visibility = Visibility.Visible;
        }
    }
}
