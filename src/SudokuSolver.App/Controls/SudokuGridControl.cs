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

    public static readonly DependencyProperty HighlightedCandidatesProperty =
        DependencyProperty.Register(nameof(HighlightedCandidates), typeof(IReadOnlyList<CandidateHighlight>),
            typeof(SudokuGridControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EliminatedCandidatesProperty =
        DependencyProperty.Register(nameof(EliminatedCandidates), typeof(IReadOnlyList<Elimination>),
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

    public IReadOnlyList<CandidateHighlight>? HighlightedCandidates
    {
        get => (IReadOnlyList<CandidateHighlight>?)GetValue(HighlightedCandidatesProperty);
        set => SetValue(HighlightedCandidatesProperty, value);
    }

    public IReadOnlyList<Elimination>? EliminatedCandidates
    {
        get => (IReadOnlyList<Elimination>?)GetValue(EliminatedCandidatesProperty);
        set => SetValue(EliminatedCandidatesProperty, value);
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
    private static readonly Brush GreenCandidateBrush = new SolidColorBrush(Color.FromRgb(46, 125, 50));
    private static readonly Brush RedCandidateBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47));
    private static readonly Brush GreenCircleFill = new SolidColorBrush(Color.FromArgb(35, 76, 175, 80));
    private static readonly Brush RedCircleFill = new SolidColorBrush(Color.FromArgb(70, 244, 67, 54));
    private static readonly Pen GreenCirclePen = new(new SolidColorBrush(Color.FromRgb(46, 125, 50)), 1.5);
    private static readonly Pen RedCirclePen = new(new SolidColorBrush(Color.FromRgb(211, 47, 47)), 2.0);
    private static readonly Pen RedStrikethroughPen = new(new SolidColorBrush(Color.FromRgb(211, 47, 47)), 2.0);
    private static readonly Pen ThinPen = new(Brushes.DarkGray, 0.5);
    private static readonly Pen ThickPen = new(Brushes.Black, 2.0);
    private static readonly Pen BorderPen = new(Brushes.Black, 3.0);

    private static void FreezeBrushes()
    {
        ThinPen.Freeze();
        ThickPen.Freeze();
        BorderPen.Freeze();
        GreenCirclePen.Freeze();
        RedCirclePen.Freeze();
        RedStrikethroughPen.Freeze();
        ((SolidColorBrush)SolvedBrush).Freeze();
        ((SolidColorBrush)PatternHighlight).Freeze();
        ((SolidColorBrush)AffectedHighlight).Freeze();
        ((SolidColorBrush)SelectedHighlight).Freeze();
        ((SolidColorBrush)GreenCandidateBrush).Freeze();
        ((SolidColorBrush)RedCandidateBrush).Freeze();
        ((SolidColorBrush)GreenCircleFill).Freeze();
        ((SolidColorBrush)RedCircleFill).Freeze();
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

        // Build candidate-level highlight lookups: (row, col, digit) → highlight type
        var greenCandidates = new HashSet<(int Row, int Col, int Digit)>();
        var redCandidates = new HashSet<(int Row, int Col, int Digit)>();

        if (HighlightedCandidates is { Count: > 0 } hc)
        {
            foreach (var h in hc)
                greenCandidates.Add((h.Cell.Row, h.Cell.Column, h.Digit));
        }

        if (EliminatedCandidates is { Count: > 0 } ec)
        {
            foreach (var e in ec)
                redCandidates.Add((e.Cell.Row, e.Cell.Column, e.Digit));
        }

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
                    DrawCandidates(dc, cell, c * cellSize, r * cellSize, cellSize,
                        greenCandidates, redCandidates);
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

    private static void DrawCandidates(DrawingContext dc, Cell cell, double x, double y, double cellSize,
        HashSet<(int Row, int Col, int Digit)> greenCandidates,
        HashSet<(int Row, int Col, int Digit)> redCandidates)
    {
        var fontSize = cellSize * 0.22;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var boldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var subSize = cellSize / 3.0;
        var dpi = VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;

        foreach (var digit in cell.Candidates)
        {
            var subRow = (digit - 1) / 3;
            var subCol = (digit - 1) % 3;
            var key = (cell.Row, cell.Column, digit);

            var isGreen = greenCandidates.Contains(key);
            var isRed = redCandidates.Contains(key);

            var brush = isGreen ? GreenCandidateBrush : isRed ? RedCandidateBrush : CandidateBrush;
            var face = (isGreen || isRed) ? boldTypeface : typeface;

            var text = new FormattedText(digit.ToString(), System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, face, fontSize, brush, dpi);

            var cx = x + subCol * subSize + subSize / 2.0;
            var cy = y + subRow * subSize + subSize / 2.0;
            var px = cx - text.Width / 2.0;
            var py = cy - text.Height / 2.0;

            // Draw circle behind the digit
            if (isGreen || isRed)
            {
                var radius = Math.Min(subSize, subSize) * 0.42;
                var circlePen = isGreen ? GreenCirclePen : RedCirclePen;
                var circleFill = isGreen ? GreenCircleFill : RedCircleFill;
                dc.DrawEllipse(circleFill, circlePen, new Point(cx, cy), radius, radius);
            }

            dc.DrawText(text, new Point(px, py));

            // Draw diagonal strikethrough over eliminated candidates
            if (isRed)
            {
                var halfSize = subSize * 0.32;
                dc.DrawLine(RedStrikethroughPen,
                    new Point(cx - halfSize, cy - halfSize),
                    new Point(cx + halfSize, cy + halfSize));
            }
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
