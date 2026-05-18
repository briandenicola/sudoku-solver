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
    /// Returns a grayscale, thresholded image centered on the digits.
    /// </summary>
    public static Mat PreprocessCell(Mat cell)
    {
        // Ensure we have a valid cell
        if (cell.Empty())
            return cell.Clone();

        // Convert to grayscale
        using var gray = new Mat();
        if (cell.Channels() == 3)
            Cv2.CvtColor(cell, gray, ColorConversionCodes.BGR2GRAY);
        else
            gray.SetTo(cell);

        // Apply Gaussian blur
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(3, 3), 0);

        // Apply adaptive threshold
        using var thresh = new Mat();
        Cv2.AdaptiveThreshold(blurred, thresh, 255,
            AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 11, 2);

        // Find and center the digit
        var centered = CenterDigit(thresh);

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

        // Find the largest contour (assumed to be the digit)
        var largestIdx = 0;
        var largestArea = 0.0;
        for (var i = 0; i < contours.Length; i++)
        {
            var area = Cv2.ContourArea(contours[i]);
            if (area > largestArea)
            {
                largestArea = area;
                largestIdx = i;
            }
        }

        // Get bounding rectangle
        var rect = Cv2.BoundingRect(contours[largestIdx]);

        // Check if there's enough ink to be a digit
        if (largestArea < 10)
            return CreateEmptyCell(); // Too small, likely empty

        // Extract and resize to fill the cell
        using var digit = new Mat(thresh, rect).Clone();
        var result = new Mat(new Size(50, 50), MatType.CV_8UC1, Scalar.All(0));
        Cv2.Resize(digit, result, new Size(50, 50), interpolation: InterpolationFlags.Area);

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
    /// </summary>
    public static bool HasDigit(Mat preprocessedCell)
    {
        // Count non-zero pixels
        var nonZero = Cv2.CountNonZero(preprocessedCell);
        // More than 1% of the cell should have ink
        return nonZero > (preprocessedCell.Rows * preprocessedCell.Cols * 0.01);
    }
}