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

        // Convert to grayscale
        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        // Apply Gaussian blur to reduce noise
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

        // Apply adaptive threshold to get binary image
        using var thresh = new Mat();
        Cv2.AdaptiveThreshold(blurred, thresh, 255,
            AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 11, 2);

        // Find contours
        Cv2.FindContours(thresh, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // Find the largest contour that could be the grid (4 corners, large area)
        var gridContour = FindSudokuGridContour(contours);

        if (gridContour == null)
            return null;

        // Get corner points and apply perspective transform
        var corners = GetCornerPoints(gridContour);
        if (corners == null)
            return null;

        // Expand corners outward slightly to ensure full grid capture
        corners = ExpandCorners(corners, margin: 4);

        return WarpToGrid(src, corners);
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

    private static Point2f[]? FindSudokuGridContour(Point[][] contours)
    {
        Point2f[]? bestContour = null;
        double bestArea = 0;
        const double minArea = 10000; // Minimum area for a valid grid

        // Try multiple epsilon values — some images need more aggressive simplification
        double[] epsilonFactors = [0.02, 0.03, 0.04];

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minArea)
                continue;

            var arcLength = Cv2.ArcLength(contour, true);

            foreach (var factor in epsilonFactors)
            {
                var epsilon = arcLength * factor;
                var approx = Cv2.ApproxPolyDP(contour, epsilon, true);

                if (approx.Length == 4 && area > bestArea)
                {
                    bestArea = area;
                    bestContour = approx.Select(p => new Point2f(p.X, p.Y)).ToArray();
                    break; // Found a quad for this contour, move to next
                }
            }
        }

        return bestContour;
    }

    private static Point2f[]? GetCornerPoints(Point2f[] contour)
    {
        // Reorder corners to: top-left, top-right, bottom-right, bottom-left
        var points = contour.Select(p => new Point2f(p.X, p.Y)).ToArray();

        var sums = points.Select(p => p.X + p.Y).ToArray();
        var diffs = points.Select(p => p.X - p.Y).ToArray();

        var ordered = new Point2f[4];
        ordered[0] = points[Array.IndexOf(sums, sums.Min())];     // Top-left (min sum)
        ordered[2] = points[Array.IndexOf(sums, sums.Max())];     // Bottom-right (max sum)
        ordered[1] = points[Array.IndexOf(diffs, diffs.Max())];   // Top-right (max diff)
        ordered[3] = points[Array.IndexOf(diffs, diffs.Min())];   // Bottom-left (min diff)

        return ordered;
    }

    private static Mat WarpToGrid(Mat src, Point2f[] corners)
    {
        const int gridSize = 450; // Standard grid size
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
        const int padding = 4; // Inset to avoid grid lines in cell images

        for (var row = 0; row < 9; row++)
        {
            cells[row] = new Mat[9];
            for (var col = 0; col < 9; col++)
            {
                var x1 = verticalLines[col] + padding;
                var y1 = horizontalLines[row] + padding;
                var x2 = verticalLines[col + 1] - padding;
                var y2 = horizontalLines[row + 1] - padding;

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
        const int gridSize = 450;
        const int expectedLines = 10;
        const int expectedSpacing = gridSize / 9; // ~50px

        // Convert to grayscale and threshold
        using var gray = new Mat();
        if (warpedGrid.Channels() == 3)
            Cv2.CvtColor(warpedGrid, gray, ColorConversionCodes.BGR2GRAY);
        else
            gray.SetTo(warpedGrid);

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

        if (medianGap < 20 || medianGap > 80) return null; // Unreasonable for 450px grid

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