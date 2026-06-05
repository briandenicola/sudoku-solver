using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace SudokuSolver.Vision;

/// <summary>
/// Classifies preprocessed sudoku cell images as digits 1-9 or empty (0)
/// using a CNN model in ONNX format trained on MNIST-style digit images.
/// </summary>
public sealed class DigitClassifier : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;

    /// <summary>
    /// Minimum softmax confidence to accept a classification result.
    /// Below this threshold, the cell is treated as empty/uncertain.
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.6f;

    /// <summary>
    /// Creates a classifier from an ONNX model file path.
    /// The model must accept [1,1,28,28] float32 input and produce [1,10] float32 output (softmax over digits 0-9).
    /// </summary>
    public DigitClassifier(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("ONNX digit model not found.", modelPath);

        _session = new InferenceSession(modelPath);

        var inputMeta = _session.InputMetadata.First();
        _inputName = inputMeta.Key;
        var inputShape = inputMeta.Value.Dimensions;
        if (inputShape.Length < 2 || inputShape[^1] != 28 || inputShape[^2] != 28)
            throw new InvalidOperationException(
                $"Expected model input with 28x28 spatial dimensions, got [{string.Join(",", inputShape)}].");

        var outputMeta = _session.OutputMetadata.First();
        _outputName = outputMeta.Key;
    }

    /// <summary>
    /// Creates a classifier from an ONNX model loaded as a byte array.
    /// </summary>
    public DigitClassifier(byte[] modelBytes)
    {
        _session = new InferenceSession(modelBytes);

        var inputMeta = _session.InputMetadata.First();
        _inputName = inputMeta.Key;

        var outputMeta = _session.OutputMetadata.First();
        _outputName = outputMeta.Key;
    }

    /// <summary>
    /// Classifies a preprocessed cell image as a digit.
    /// Returns (digit, confidence) where digit is 0 for empty/uncertain.
    /// </summary>
    /// <param name="preprocessedCell">A grayscale cell image (any size, will be resized to 28x28).</param>
    public (int Digit, float Confidence) Classify(Mat preprocessedCell)
    {
        var tensor = PreprocessForModel(preprocessedCell);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        // Find the class with highest probability
        var maxIdx = 0;
        var maxVal = float.MinValue;
        for (var i = 0; i < 10; i++)
        {
            var val = output[0, i];
            if (val > maxVal)
            {
                maxVal = val;
                maxIdx = i;
            }
        }

        // Apply softmax if output isn't already probabilities
        var confidence = Softmax(output, maxIdx);

        // Post-classification correction for 1/7 confusion using aspect ratio.
        // Printed "1" is a thin vertical stroke (aspect ratio < 0.35),
        // while "7" has a horizontal bar making it wider (aspect ratio > 0.4).
        if (maxIdx is 1 or 7)
        {
            var aspectRatio = GetDigitAspectRatio(preprocessedCell);
            if (aspectRatio < 0.35f)
                maxIdx = 1;
            else if (aspectRatio > 0.5f && maxIdx == 1)
                maxIdx = 7;
        }

        // For sudoku, digit 0 means empty — if the model predicts 0, treat as empty
        if (maxIdx == 0)
            return (0, confidence);

        // If confidence is too low, treat as uncertain (empty)
        if (confidence < ConfidenceThreshold)
            return (0, confidence);

        return (maxIdx, confidence);
    }

    /// <summary>
    /// Computes the width/height aspect ratio of the digit's bounding box.
    /// Used to disambiguate narrow digits (1) from wide digits (7).
    /// </summary>
    private static float GetDigitAspectRatio(Mat cell)
    {
        // Ensure grayscale binary image
        Mat binary;
        if (cell.Channels() == 3)
        {
            binary = new Mat();
            Cv2.CvtColor(cell, binary, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            binary = cell;
        }

        Cv2.FindContours(binary, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
            return 0.5f; // Default: ambiguous

        // Find largest contour
        var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        var rect = Cv2.BoundingRect(largest);

        if (binary != cell)
            binary.Dispose();

        return rect.Height > 0 ? (float)rect.Width / rect.Height : 0.5f;
    }

    /// <summary>
    /// Resizes and normalizes a cell image to the model's expected 28x28 input format.
    /// Preserves aspect ratio by fitting the digit into a 20x20 area with 4px padding,
    /// matching MNIST conventions.
    /// </summary>
    internal static DenseTensor<float> PreprocessForModel(Mat cell)
    {
        // Ensure grayscale
        Mat gray;
        if (cell.Channels() == 3)
        {
            gray = new Mat();
            Cv2.CvtColor(cell, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = cell;
        }

        // Create 28x28 canvas (black background, matching MNIST)
        using var canvas = new Mat(28, 28, MatType.CV_8UC1, Scalar.All(0));

        if (gray.Rows > 0 && gray.Cols > 0)
        {
            // Fit into 20x20 preserving aspect ratio (MNIST convention: 4px margin)
            const int targetSize = 20;
            var scale = Math.Min((float)targetSize / gray.Rows, (float)targetSize / gray.Cols);
            var newW = Math.Max(1, (int)(gray.Cols * scale));
            var newH = Math.Max(1, (int)(gray.Rows * scale));

            using var resized = new Mat();
            Cv2.Resize(gray, resized, new Size(newW, newH), interpolation: InterpolationFlags.Area);

            // Center in the 28x28 canvas
            var offsetX = (28 - newW) / 2;
            var offsetY = (28 - newH) / 2;
            var roi = new Rect(offsetX, offsetY, newW, newH);
            resized.CopyTo(new Mat(canvas, roi));
        }

        // Convert to float tensor [1, 1, 28, 28], normalized to [0, 1]
        var tensor = new DenseTensor<float>([1, 1, 28, 28]);
        for (var y = 0; y < 28; y++)
        {
            for (var x = 0; x < 28; x++)
            {
                tensor[0, 0, y, x] = canvas.At<byte>(y, x) / 255f;
            }
        }

        if (gray != cell)
            gray.Dispose();

        return tensor;
    }

    private static float Softmax(Tensor<float> logits, int index)
    {
        var max = float.MinValue;
        for (var i = 0; i < 10; i++)
        {
            if (logits[0, i] > max) max = logits[0, i];
        }

        var sumExp = 0f;
        for (var i = 0; i < 10; i++)
        {
            sumExp += MathF.Exp(logits[0, i] - max);
        }

        return MathF.Exp(logits[0, index] - max) / sumExp;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
