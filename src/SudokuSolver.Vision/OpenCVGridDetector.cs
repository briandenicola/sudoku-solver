using OpenCvSharp;

namespace SudokuSolver.Vision;

/// <summary>
/// Uses OpenCV to detect the Sudoku grid in an image, handling perspective correction
/// and extraction of individual cells. This provides robust grid detection even for
/// angled photos, then passes each cell to LLM for digit classification.
/// </summary>
public static class OpenCVGridDetector
{
    /// <summary>
    /// Finds the Sudoku grid contour and returns the perspective-corrected image.
    /// Returns null if no grid is found.
    /// </summary>
    public static Mat? FindAndWarpGrid(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found.", imagePath);

        using var src = Cv2.ImRead(imagePath, ImreadModes.Color);
        return FindAndWarpGrid(src);
    }

    /// <summary>
    /// Finds the Sudoku grid contour and returns the perspective-corrected image.
    /// Returns null if no grid is found.
    /// </summary>
    public static Mat? FindAndWarpGrid(Mat src)
    {
        if (src.Empty())
            return null;

        var corners = DetectGridCorners(src);
        if (corners == null)
            return null;

        // Expand corners outward slightly to ensure full grid capture
        corners = ExpandCorners(corners, margin: 4);

        return WarpToGrid(src, corners);
    }

    /// <summary>
    /// Detects the four corners of the Sudoku grid in source-image coordinates.
    /// Robust to broken borders and perspective distortion: it finds the largest
    /// contour and derives the four extreme corners from all its points rather than
    /// requiring a clean 4-point polygon approximation.
    /// </summary>
    internal static Point2f[]? DetectGridCorners(Mat src)
    {
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

        // Adaptive threshold with a block size that scales with the image so the
        // grid border resolves into a connected shape on large photos.
        var blockSize = Math.Max(11, (Math.Min(src.Rows, src.Cols) / 20) | 1); // odd, >= 11
        using var thresh = new Mat();
        Cv2.AdaptiveThreshold(blurred, thresh, 255,
            AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, blockSize, 7);

        // Close gaps in the grid border so it forms one connected contour.
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var closed = new Mat();
        Cv2.MorphologyEx(thresh, closed, MorphTypes.Close, kernel);

        Cv2.FindContours(closed, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
            return null;

        // The grid is the largest contour by area, with a sanity check that it
        // covers a meaningful fraction of the image and is roughly square.
        var imageArea = (double)src.Rows * src.Cols;
        Point[]? best = null;
        double bestArea = 0;
        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < imageArea * 0.15) // must cover at least 15% of the image
                continue;
            if (area > bestArea)
            {
                bestArea = area;
                best = contour;
            }
        }

        if (best == null)
            return null;

        return ExtremeCorners(best);
    }

    /// <summary>
    /// Derives the four grid corners from the extreme points of a contour.
    /// Top-left has the minimum (x+y), bottom-right the maximum (x+y),
    /// top-right the maximum (x-y), and bottom-left the minimum (x-y).
    /// Using all contour points (not a 4-point approximation) is robust to
    /// jagged or partially-broken borders.
    /// </summary>
    private static Point2f[] ExtremeCorners(Point[] contour)
    {
        Point tl = contour[0], tr = contour[0], br = contour[0], bl = contour[0];
        foreach (var p in contour)
        {
            if (p.X + p.Y < tl.X + tl.Y) tl = p;
            if (p.X + p.Y > br.X + br.Y) br = p;
            if (p.X - p.Y > tr.X - tr.Y) tr = p;
            if (p.X - p.Y < bl.X - bl.Y) bl = p;
        }

        return
        [
            new Point2f(tl.X, tl.Y),
            new Point2f(tr.X, tr.Y),
            new Point2f(br.X, br.Y),
            new Point2f(bl.X, bl.Y),
        ];
    }

    /// <summary>
    /// Expands corner points outward from centroid by a pixel margin.
    /// This ensures the warp captures the full outer grid boundary.
    /// </summary>
    private static Point2f[] ExpandCorners(Point2f[] corners, int margin)
    {
        var cx = corners.Average(p => p.X);
        var cy = corners.Average(p => p.Y);

        var expanded = new Point2f[4];
        for (var i = 0; i < 4; i++)
        {
            var dx = corners[i].X - cx;
            var dy = corners[i].Y - cy;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < 1) dist = 1;
            expanded[i] = new Point2f(
                (float)(corners[i].X + dx / dist * margin),
                (float)(corners[i].Y + dy / dist * margin));
        }
        return expanded;
    }

    private static Mat WarpToGrid(Mat src, Point2f[] corners)
    {
        const int gridSize = 900; // Large size for better digit resolution
        var dst = new Mat(gridSize, gridSize, MatType.CV_8UC3, Scalar.All(255));

        var dstPts = new Point2f[]
        {
            new(0, 0),
            new(gridSize - 1, 0),
            new(gridSize - 1, gridSize - 1),
            new(0, gridSize - 1)
        };

        var m = Cv2.GetPerspectiveTransform(corners, dstPts);
        Cv2.WarpPerspective(src, dst, m, new Size(gridSize, gridSize));

        return dst.Clone();
    }

    /// <summary>
    /// Extracts individual cells from a warped grid image using projection profiles
    /// to find actual grid line positions rather than assuming equal spacing.
    /// </summary>
    public static Mat[][] ExtractCells(Mat warpedGrid)
    {
        // Detect grid lines using projection profiles
        var verticalLines = DetectGridLines(warpedGrid, horizontal: false);
        var horizontalLines = DetectGridLines(warpedGrid, horizontal: true);

        var cells = new Mat[9][];

        for (var row = 0; row < 9; row++)
        {
            cells[row] = new Mat[9];
            for (var col = 0; col < 9; col++)
            {
                // Inset by a proportion of the cell size to crop grid-line borders.
                // Proportional padding adapts to image resolution and is large enough
                // to remove thick or slightly-misaligned grid lines from photos.
                var cellW = verticalLines[col + 1] - verticalLines[col];
                var cellH = horizontalLines[row + 1] - horizontalLines[row];
                var padX = Math.Max(3, (int)(cellW * 0.10));
                var padY = Math.Max(3, (int)(cellH * 0.10));

                var x1 = verticalLines[col] + padX;
                var y1 = horizontalLines[row] + padY;
                var x2 = verticalLines[col + 1] - padX;
                var y2 = horizontalLines[row + 1] - padY;

                // Clamp to image bounds
                x1 = Math.Max(0, x1);
                y1 = Math.Max(0, y1);
                x2 = Math.Min(warpedGrid.Cols - 1, x2);
                y2 = Math.Min(warpedGrid.Rows - 1, y2);

                var w = x2 - x1;
                var h = y2 - y1;

                if (w > 5 && h > 5)
                {
                    cells[row][col] = new Mat(warpedGrid, new Rect(x1, y1, w, h)).Clone();
                }
                else
                {
                    // Fallback: use equal division for this cell
                    var cellSize = warpedGrid.Cols / 9;
                    cells[row][col] = new Mat(warpedGrid,
                        new Rect(col * cellSize, row * cellSize, cellSize, cellSize)).Clone();
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Detects grid line positions using projection profiles.
    /// Uses morphological operations to isolate lines from digits,
    /// then finds peaks in the projection (sum of dark pixels per row/column).
    /// Returns 10 positions representing the 10 grid boundaries.
    /// </summary>
    internal static int[] DetectGridLines(Mat warpedGrid, bool horizontal)
    {
        var gridSize = horizontal ? warpedGrid.Rows : warpedGrid.Cols;
        const int expectedLines = 10;
        var expectedSpacing = gridSize / 9;

        // Convert to grayscale and threshold
        using var gray = new Mat();
        if (warpedGrid.Channels() == 3)
            Cv2.CvtColor(warpedGrid, gray, ColorConversionCodes.BGR2GRAY);
        else
            warpedGrid.CopyTo(gray);

        using var binary = new Mat();
        Cv2.AdaptiveThreshold(gray, binary, 255,
            AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 15, 2);

        // Morphological operation to isolate lines
        // For horizontal lines: use a wide horizontal kernel
        // For vertical lines: use a tall vertical kernel
        using var lineMask = new Mat();
        var kernelLength = gridSize / 4; // Lines should span at least 1/4 of the grid
        using var kernel = horizontal
            ? Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelLength, 1))
            : Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, kernelLength));

        Cv2.MorphologyEx(binary, lineMask, MorphTypes.Open, kernel);

        // Compute projection profile
        var size = horizontal ? lineMask.Rows : lineMask.Cols;
        var maskRows = lineMask.Rows;
        var maskCols = lineMask.Cols;
        var profile = new int[size];

        if (horizontal)
        {
            for (var i = 0; i < size; i++)
            {
                var count = 0;
                for (var j = 0; j < maskCols; j++)
                {
                    if (lineMask.At<byte>(i, j) > 0)
                        count++;
                }
                profile[i] = count;
            }
        }
        else
        {
            for (var i = 0; i < size; i++)
            {
                var count = 0;
                for (var j = 0; j < maskRows; j++)
                {
                    if (lineMask.At<byte>(j, i) > 0)
                        count++;
                }
                profile[i] = count;
            }
        }

        // Find peaks in the profile
        var peaks = FindPeaks(profile, minDistance: expectedSpacing / 3);

        // Fit best 10-line model
        var lines = FitGridModel(peaks, gridSize, expectedLines, expectedSpacing);

        return lines;
    }

    /// <summary>
    /// Finds peak positions in a projection profile.
    /// </summary>
    private static List<int> FindPeaks(int[] profile, int minDistance)
    {
        var peaks = new List<int>();
        var threshold = profile.Max() * 0.2; // At least 20% of max

        for (var i = 1; i < profile.Length - 1; i++)
        {
            if (profile[i] < threshold)
                continue;

            // Check if this is a local maximum
            if (profile[i] >= profile[i - 1] && profile[i] >= profile[i + 1])
            {
                // Check minimum distance from last peak
                if (peaks.Count == 0 || i - peaks[^1] >= minDistance)
                {
                    peaks.Add(i);
                }
                else if (profile[i] > profile[peaks[^1]])
                {
                    // Replace last peak if this one is stronger
                    peaks[^1] = i;
                }
            }
        }

        return peaks;
    }

    /// <summary>
    /// Fits detected peaks to the best 10-line grid model.
    /// If detection fails, falls back to equal spacing.
    /// </summary>
    private static int[] FitGridModel(List<int> peaks, int gridSize, int expectedLines, int expectedSpacing)
    {
        // If we have exactly 10 peaks at reasonable positions, use them directly
        if (peaks.Count == expectedLines && IsReasonableSpacing(peaks, expectedSpacing))
        {
            return [.. peaks];
        }

        // If we have close to 10 peaks, try to select the best subset
        if (peaks.Count > expectedLines)
        {
            var best = SelectBestLines(peaks, expectedLines, expectedSpacing);
            if (best != null)
                return best;
        }

        // If we have fewer peaks, try to interpolate missing lines
        if (peaks.Count >= 7)
        {
            var interpolated = InterpolateMissingLines(peaks, gridSize, expectedLines);
            if (interpolated != null)
                return interpolated;
        }

        // Fallback: equal spacing
        var fallback = new int[expectedLines];
        var step = (double)(gridSize - 1) / 9;
        for (var i = 0; i < expectedLines; i++)
            fallback[i] = (int)Math.Round(i * step);
        return fallback;
    }

    private static bool IsReasonableSpacing(List<int> lines, int expectedSpacing)
    {
        var minCell = expectedSpacing * 0.6;
        var maxCell = expectedSpacing * 1.5;

        for (var i = 1; i < lines.Count; i++)
        {
            var gap = lines[i] - lines[i - 1];
            if (gap < minCell || gap > maxCell)
                return false;
        }
        return true;
    }

    private static int[]? SelectBestLines(List<int> peaks, int expectedLines, int expectedSpacing)
    {
        // Score subsets by how evenly spaced they are
        // Simple greedy: start from first/last and pick peaks closest to expected positions
        if (peaks.Count < expectedLines)
            return null;

        var first = peaks[0];
        var last = peaks[^1];
        var span = last - first;

        if (span < expectedSpacing * 7) // Too narrow
            return null;

        var step = (double)span / (expectedLines - 1);
        var selected = new int[expectedLines];

        for (var i = 0; i < expectedLines; i++)
        {
            var target = first + (int)(i * step);
            // Find closest peak
            var closest = peaks.OrderBy(p => Math.Abs(p - target)).First();
            selected[i] = closest;
        }

        // Ensure monotonic
        Array.Sort(selected);
        for (var i = 1; i < selected.Length; i++)
        {
            if (selected[i] <= selected[i - 1])
                return null;
        }

        return selected;
    }

    private static int[]? InterpolateMissingLines(List<int> peaks, int gridSize, int expectedLines)
    {
        // Estimate spacing from detected peaks
        var sortedPeaks = peaks.OrderBy(p => p).ToList();
        var gaps = new List<int>();
        for (var i = 1; i < sortedPeaks.Count; i++)
            gaps.Add(sortedPeaks[i] - sortedPeaks[i - 1]);

        if (gaps.Count == 0) return null;

        // The median gap is likely one cell width
        gaps.Sort();
        var medianGap = gaps[gaps.Count / 2];

        if (medianGap < 20 || medianGap > gridSize / 5) return null; // Unreasonable gap

        // Try to build a 10-line grid starting from the first peak
        var result = new int[expectedLines];
        result[0] = Math.Max(0, sortedPeaks[0]);

        for (var i = 1; i < expectedLines; i++)
        {
            var expected = result[0] + i * medianGap;
            // Find closest detected peak
            var closest = sortedPeaks
                .Where(p => Math.Abs(p - expected) < medianGap * 0.4)
                .OrderBy(p => Math.Abs(p - expected))
                .FirstOrDefault(-1);

            result[i] = closest >= 0 ? closest : (int)expected;
        }

        // Validate: must fit within grid and be monotonic
        if (result[^1] > gridSize + 5) return null;
        for (var i = 1; i < result.Length; i++)
        {
            if (result[i] <= result[i - 1])
                return null;
        }

        return result;
    }
}