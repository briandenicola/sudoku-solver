using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SudokuSolver.Engine.Models;

using SudokuGrid = SudokuSolver.Engine.Models.Grid;

namespace SudokuSolver.App.Controls;

/// <summary>
/// Custom WPF control that renders a sudoku grid with support for:
/// - Displaying solved and unsolved cells
/// - Showing candidate pencil marks
/// - Highlighting pattern cells and affected cells for step visualization
/// </summary>
public class SudokuGridControl : Control
{
    static SudokuGridControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SudokuGridControl),
            new FrameworkPropertyMetadata(typeof(SudokuGridControl)));
        FreezeBrushes();
    }

    public static readonly DependencyProperty GridDataProperty =
        DependencyProperty.Register(nameof(GridData), typeof(SudokuGrid), typeof(SudokuGridControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightedPatternCellsProperty =
        DependencyProperty.Register(nameof(HighlightedPatternCells), typeof(IReadOnlyList<Cell>),
            typeof(SudokuGridControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightedAffectedCellsProperty =
        DependencyProperty.Register(nameof(HighlightedAffectedCells), typeof(IReadOnlyList<Cell>),
            typeof(SudokuGridControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedCellRowProperty =
        DependencyProperty.Register(nameof(SelectedCellRow), typeof(int), typeof(SudokuGridControl),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedCellColumnProperty =
        DependencyProperty.Register(nameof(SelectedCellColumn), typeof(int), typeof(SudokuGridControl),
            new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));

    public SudokuGrid? GridData
    {
        get => (SudokuGrid?)GetValue(GridDataProperty);
        set => SetValue(GridDataProperty, value);
    }

    public IReadOnlyList<Cell>? HighlightedPatternCells
    {
        get => (IReadOnlyList<Cell>?)GetValue(HighlightedPatternCellsProperty);
        set => SetValue(HighlightedPatternCellsProperty, value);
    }

    public IReadOnlyList<Cell>? HighlightedAffectedCells
    {
        get => (IReadOnlyList<Cell>?)GetValue(HighlightedAffectedCellsProperty);
        set => SetValue(HighlightedAffectedCellsProperty, value);
    }

    public int SelectedCellRow
    {
        get => (int)GetValue(SelectedCellRowProperty);
        set => SetValue(SelectedCellRowProperty, value);
    }

    public int SelectedCellColumn
    {
        get => (int)GetValue(SelectedCellColumnProperty);
        set => SetValue(SelectedCellColumnProperty, value);
    }

    // Colors
    private static readonly Brush GivenBrush = Brushes.Black;
    private static readonly Brush SolvedBrush = new SolidColorBrush(Color.FromRgb(0, 100, 180));
    private static readonly Brush CandidateBrush = Brushes.Gray;
    private static readonly Brush PatternHighlight = new SolidColorBrush(Color.FromArgb(60, 76, 175, 80));
    private static readonly Brush AffectedHighlight = new SolidColorBrush(Color.FromArgb(60, 244, 67, 54));
    private static readonly Brush SelectedHighlight = new SolidColorBrush(Color.FromArgb(40, 33, 150, 243));
    private static readonly Pen ThinPen = new(Brushes.DarkGray, 0.5);
    private static readonly Pen ThickPen = new(Brushes.Black, 2.0);
    private static readonly Pen BorderPen = new(Brushes.Black, 3.0);

    private static void FreezeBrushes()
    {
        ThinPen.Freeze();
        ThickPen.Freeze();
        BorderPen.Freeze();
        ((SolidColorBrush)SolvedBrush).Freeze();
        ((SolidColorBrush)PatternHighlight).Freeze();
        ((SolidColorBrush)AffectedHighlight).Freeze();
        ((SolidColorBrush)SelectedHighlight).Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var grid = GridData;
        if (grid == null) return;

        var size = Math.Min(ActualWidth, ActualHeight);
        var cellSize = size / 9.0;

        var patternCells = HighlightedPatternCells;
        var affectedCells = HighlightedAffectedCells;

        // Draw cell backgrounds (highlights)
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                var rect = new Rect(c * cellSize, r * cellSize, cellSize, cellSize);
                var cell = grid[r, c];

                if (r == SelectedCellRow && c == SelectedCellColumn)
                    dc.DrawRectangle(SelectedHighlight, null, rect);

                if (patternCells?.Contains(cell) == true)
                    dc.DrawRectangle(PatternHighlight, null, rect);
                else if (affectedCells?.Contains(cell) == true)
                    dc.DrawRectangle(AffectedHighlight, null, rect);
            }
        }

        // Draw grid lines
        for (var i = 0; i <= 9; i++)
        {
            var pen = (i % 3 == 0) ? ThickPen : ThinPen;
            dc.DrawLine(pen, new Point(i * cellSize, 0), new Point(i * cellSize, size));
            dc.DrawLine(pen, new Point(0, i * cellSize), new Point(size, i * cellSize));
        }

        // Draw border
        dc.DrawRectangle(null, BorderPen, new Rect(0, 0, size, size));

        // Draw cell values and candidates
        for (var r = 0; r < 9; r++)
        {
            for (var c = 0; c < 9; c++)
            {
                var cell = grid[r, c];
                if (cell.IsSolved)
                {
                    DrawCellValue(dc, cell, c * cellSize, r * cellSize, cellSize);
                }
                else if (!cell.Candidates.IsEmpty)
                {
                    DrawCandidates(dc, cell, c * cellSize, r * cellSize, cellSize);
                }
            }
        }
    }

    private static void DrawCellValue(DrawingContext dc, Cell cell, double x, double y, double cellSize)
    {
        var brush = cell.IsGiven ? GivenBrush : SolvedBrush;
        var fontSize = cellSize * 0.6;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
            cell.IsGiven ? FontWeights.Bold : FontWeights.SemiBold, FontStretches.Normal);

        var text = new FormattedText(cell.Value.ToString(), System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, fontSize, brush, VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

        dc.DrawText(text, new Point(x + (cellSize - text.Width) / 2, y + (cellSize - text.Height) / 2));
    }

    private static void DrawCandidates(DrawingContext dc, Cell cell, double x, double y, double cellSize)
    {
        var fontSize = cellSize * 0.22;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var subSize = cellSize / 3.0;

        foreach (var digit in cell.Candidates)
        {
            var subRow = (digit - 1) / 3;
            var subCol = (digit - 1) % 3;

            var text = new FormattedText(digit.ToString(), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, fontSize, CandidateBrush,
                VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

            var px = x + subCol * subSize + (subSize - text.Width) / 2;
            var py = y + subRow * subSize + (subSize - text.Height) / 2;

            dc.DrawText(text, new Point(px, py));
        }
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pos = e.GetPosition(this);
        var cellSize = Math.Min(ActualWidth, ActualHeight) / 9.0;
        var col = (int)(pos.X / cellSize);
        var row = (int)(pos.Y / cellSize);

        if (row is >= 0 and < 9 && col is >= 0 and < 9)
        {
            SelectedCellRow = row;
            SelectedCellColumn = col;
        }
    }
}
