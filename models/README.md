# Digit Recognition Model

The sudoku solver uses a CNN model in ONNX format to classify digits extracted from puzzle images.

## Setup

Place `mnist-cnn.onnx` in this directory. The application looks for it at:
- `<app-dir>/Models/mnist-cnn.onnx`
- `<app-dir>/mnist-cnn.onnx`

## Training Your Own Model

```bash
cd models
pip install torch torchvision onnx
python train_digit_model.py
```

This trains a simple CNN on MNIST (~99.3% accuracy) and exports `mnist-cnn.onnx`.  Read [this document](./docs/cnn_vs_vlm.md) for details on why a CNN is used and how it compares to VLMs for this task.

## Model Requirements

The ONNX model must:
- Accept input tensor shape `[batch, 1, 28, 28]` (float32, values 0-1)
- Produce output tensor shape `[batch, 10]` (logits or probabilities for digits 0-9)

## Accuracy Notes

The stock MNIST model works well for clean printed digits. For better accuracy on
real-world sudoku photos (varied fonts, angles, noise), consider fine-tuning on
actual sudoku cell crops from your target domain.
