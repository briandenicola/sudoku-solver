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

        return WarpToGrid(src, corners);
    }

    private static Point2f[]? FindSudokuGridContour(Point[][] contours)
    {
        Point2f[]? bestContour = null;
        double bestArea = 0;
        const double minArea = 10000; // Minimum area for a valid grid

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minArea)
                continue;

            // Approximate contour to polygon
            var epsilon = Cv2.ArcLength(contour, true) * 0.02;
            var approx = Cv2.ApproxPolyDP(contour, epsilon, true);

            // Look for quadrilaterals (4 corners)
            if (approx.Length == 4)
            {
                if (area > bestArea)
                {
                    bestArea = area;
                    bestContour = approx.Select(p => new Point2f(p.X, p.Y)).ToArray();
                }
            }
        }

        return bestContour;
    }

    private static Point2f[]? GetCornerPoints(Point2f[] contour)
    {
        // Reorder corners to: top-left, top-right, bottom-right, bottom-left
        // Using sum and difference trick for corner ordering
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
    /// Extracts individual cells from a warped grid image.
    /// Each cell is a 50x50 image (450/9).
    /// </summary>
    public static Mat[][] ExtractCells(Mat warpedGrid)
    {
        const int cellSize = 50; // 450 / 9
        var cells = new Mat[9][];

        for (var row = 0; row < 9; row++)
        {
            cells[row] = new Mat[9];
            for (var col = 0; col < 9; col++)
            {
                var x = col * cellSize;
                var y = row * cellSize;
                cells[row][col] = new Mat(warpedGrid, new Rect(x, y, cellSize, cellSize)).Clone();
            }
        }

        return cells;
    }
}