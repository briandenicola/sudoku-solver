using OpenCvSharp;

namespace SudokuSolver.Vision;

/// <summary>
/// Preprocesses individual cell images for digit classification.
/// Enhances image quality for better OCR/LLM accuracy.
/// </summary>
public static class CellExtractor
{
    /// <summary>
    /// Preprocesses a cell image for digit detection.
    /// Returns a grayscale, thresholded image centered on the digit.
    /// </summary>
    public static Mat PreprocessCell(Mat cell)
    {
        if (cell.Empty())
            return cell.Clone();

        // Convert to grayscale
        using var gray = new Mat();
        if (cell.Channels() == 3)
            Cv2.CvtColor(cell, gray, ColorConversionCodes.BGR2GRAY);
        else
            cell.CopyTo(gray);

        // Quick contrast check: if the cell has very low variation, it's empty.
        // This prevents Otsu from hallucinating digits in uniform cells.
        Cv2.MeanStdDev(gray, out _, out var stddev);
        if (stddev[0] < 15)
            return CreateEmptyCell();

        // Apply Gaussian blur to reduce noise
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);

        // Otsu threshold — good bimodal split for cells that actually contain a digit
        using var thresh = new Mat();
        Cv2.Threshold(blurred, thresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        // Morphological opening removes small noise speckles (grid line remnants)
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var cleaned = new Mat();
        Cv2.MorphologyEx(thresh, cleaned, MorphTypes.Open, kernel);

        // Find and center the digit
        var centered = CenterDigit(cleaned);

        return centered;
    }

    /// <summary>
    /// Centers and scales the digit within the cell.
    /// </summary>
    private static Mat CenterDigit(Mat thresh)
    {
        Cv2.FindContours(thresh, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
            return thresh.Clone(); // Return as-is if no contours found

        var imgH = thresh.Rows;
        var imgW = thresh.Cols;

        // Filter contours: ignore those touching the cell edges (grid line remnants)
        // and those that are too small or have extreme aspect ratios
        Point[]? bestContour = null;
        double bestArea = 0;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < 20) continue; // Too small

            var rect = Cv2.BoundingRect(contour);

            // Reject contours that span almost the entire cell in either dimension.
            // A real digit always leaves a margin; full-span blobs are grid lines or
            // page-edge borders that leaked into the cell.
            if (rect.Width >= imgW * 0.92 || rect.Height >= imgH * 0.92)
                continue;

            // Skip contours touching edges (likely grid lines)
            if (rect.X <= 1 || rect.Y <= 1 ||
                rect.X + rect.Width >= imgW - 2 ||
                rect.Y + rect.Height >= imgH - 2)
            {
                // Allow if it's clearly a digit (occupies significant central area)
                var centerX = rect.X + rect.Width / 2;
                var centerY = rect.Y + rect.Height / 2;
                var isCentral = centerX > imgW * 0.25 && centerX < imgW * 0.75 &&
                                centerY > imgH * 0.2 && centerY < imgH * 0.8;
                if (!isCentral) continue;
            }

            // Skip very thin contours (grid lines have extreme aspect ratios)
            var aspectRatio = (float)rect.Width / Math.Max(rect.Height, 1);
            if (aspectRatio > 5.0f || aspectRatio < 0.15f)
                continue; // Too thin horizontally or vertically — likely a grid line

            // Prefer contours closest to center with good area
            if (area > bestArea)
            {
                bestArea = area;
                bestContour = contour;
            }
        }

        if (bestContour == null || bestArea < 20)
            return CreateEmptyCell();

        var digitRect = Cv2.BoundingRect(bestContour);

        // Extract the digit region (preserving aspect ratio)
        using var digit = new Mat(thresh, digitRect).Clone();

        // Place the digit centered in a 50x50 canvas, preserving aspect ratio
        var result = new Mat(new Size(50, 50), MatType.CV_8UC1, Scalar.All(0));
        var scale = Math.Min(40.0 / digit.Rows, 40.0 / digit.Cols);
        var newW = Math.Max(1, (int)(digit.Cols * scale));
        var newH = Math.Max(1, (int)(digit.Rows * scale));

        using var resized = new Mat();
        Cv2.Resize(digit, resized, new Size(newW, newH), interpolation: InterpolationFlags.Area);

        var offsetX = (50 - newW) / 2;
        var offsetY = (50 - newH) / 2;
        resized.CopyTo(new Mat(result, new Rect(offsetX, offsetY, newW, newH)));

        return result;
    }

    /// <summary>
    /// Creates an empty cell image (all black).
    /// </summary>
    private static Mat CreateEmptyCell()
    {
        return new Mat(new Size(50, 50), MatType.CV_8UC1, Scalar.All(0));
    }

    /// <summary>
    /// Converts a Mat to base64 string for LLM transmission.
    /// </summary>
    public static string MatToBase64(Mat mat)
    {
        using var encoded = new Mat();
        Cv2.ImEncode(".png", mat, out var buffer);
        return Convert.ToBase64String(buffer);
    }

    /// <summary>
    /// Checks if a cell appears to contain a digit (has sufficient ink).
    /// After morphological cleaning, real digits have 5%+ ink coverage.
    /// </summary>
    public static bool HasDigit(Mat preprocessedCell)
    {
        var nonZero = Cv2.CountNonZero(preprocessedCell);
        var totalPixels = preprocessedCell.Rows * preprocessedCell.Cols;
        // Real digits occupy at least 3% of the cell area after preprocessing
        return nonZero > totalPixels * 0.03;
    }
}