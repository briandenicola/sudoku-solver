"""
Train a CNN on synthetic printed digits and export to ONNX.
Renders digits in many system fonts with augmentations simulating
real-world photos processed through OpenCV thresholding.

Usage:
    pip install torch torchvision onnx pillow
    python train_digit_model.py

Produces: mnist-cnn.onnx in the same directory.
"""

import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import DataLoader, Dataset
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import numpy as np
import random
import os

class DigitCNN(nn.Module):
    """CNN for digit classification (10 classes: 0-9)."""

    def __init__(self):
        super().__init__()
        self.features = nn.Sequential(
            nn.Conv2d(1, 32, 3, padding=1),
            nn.ReLU(),
            nn.Conv2d(32, 64, 3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(2),
            nn.Dropout(0.25),
            nn.Conv2d(64, 64, 3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(2),
            nn.Dropout(0.25),
        )
        self.classifier = nn.Sequential(
            nn.Flatten(),
            nn.Linear(64 * 7 * 7, 128),
            nn.ReLU(),
            nn.Dropout(0.5),
            nn.Linear(128, 10),
        )

    def forward(self, x):
        x = self.features(x)
        x = self.classifier(x)
        return x


class PrintedDigitDataset(Dataset):
    """
    Generates synthetic printed digit images using system fonts.
    Simulates what digits look like after OpenCV adaptive thresholding:
    white digits on black background, with noise and artifacts.
    """

    def __init__(self, samples_per_digit=5000):
        self.images = []
        self.labels = []
        self.fonts = self._find_fonts()

        print(f"Found {len(self.fonts)} fonts for synthetic data generation")

        for digit in range(10):
            for _ in range(samples_per_digit):
                img = self._render_digit(str(digit))
                self.images.append(img)
                self.labels.append(digit)

        print(f"Generated {len(self.images)} total samples ({samples_per_digit} per digit)")

    def _find_fonts(self):
        """Find available system fonts suitable for digit rendering.
        Uses only standard text fonts that produce clean, readable digits
        like those found in printed sudoku puzzles."""
        font_dirs = []
        if os.name == 'nt':
            font_dirs.append(r"C:\Windows\Fonts")
        else:
            font_dirs.extend(["/usr/share/fonts", "/usr/local/share/fonts",
                            os.path.expanduser("~/.fonts")])

        # Curated list of font families commonly used in printed material
        # Focus on sans-serif and clean serif fonts used in newspapers/apps
        wanted = [
            "arial", "arialbd", "ariali", "ariblk",
            "times", "timesbd", "timesi",
            "calibri", "calibrib", "calibril",
            "cambria", "cambriab",
            "georgia", "georgiab",
            "verdana", "verdanab",
            "tahoma", "tahomabd",
            "trebuc", "trebucbd",
            "consola", "consolab",
            "cour", "courbd",
            "segoeui", "segoeuib", "segoeuil",
            "pala", "palab",
            "garamond",
            "lucon",
            "micross",
        ]

        fonts = []
        for font_dir in font_dirs:
            if not os.path.isdir(font_dir):
                continue
            for fname in os.listdir(font_dir):
                if not fname.lower().endswith(('.ttf', '.otf')):
                    continue
                name_lower = fname.lower().replace('.ttf', '').replace('.otf', '')
                if name_lower not in wanted:
                    continue
                fpath = os.path.join(font_dir, fname)
                try:
                    f = ImageFont.truetype(fpath, 20)
                    bbox = f.getbbox("8")
                    if bbox and (bbox[2] - bbox[0]) > 3:
                        fonts.append(fpath)
                except Exception:
                    pass

        if not fonts:
            # Fallback: grab any font that renders digits
            for font_dir in font_dirs:
                if not os.path.isdir(font_dir):
                    continue
                for fname in os.listdir(font_dir)[:20]:
                    if not fname.lower().endswith(('.ttf', '.otf')):
                        continue
                    fpath = os.path.join(font_dir, fname)
                    try:
                        f = ImageFont.truetype(fpath, 20)
                        bbox = f.getbbox("8")
                        if bbox and (bbox[2] - bbox[0]) > 3:
                            fonts.append(fpath)
                    except Exception:
                        pass
                    if len(fonts) >= 5:
                        break

        if not fonts:
            fonts.append(None)

        return fonts

    def _render_digit(self, digit_str):
        """Render a digit simulating OpenCV-processed cell appearance."""
        size = 28
        img = Image.new('L', (size, size), color=0)
        draw = ImageDraw.Draw(img)

        # Random font and size (simulate varying print sizes)
        font_path = random.choice(self.fonts)
        font_size = random.randint(16, 24)

        try:
            if font_path:
                font = ImageFont.truetype(font_path, font_size)
            else:
                font = ImageFont.load_default()
        except Exception:
            font = ImageFont.load_default()

        # Get text bounding box and center it
        bbox = draw.textbbox((0, 0), digit_str, font=font)
        text_w = bbox[2] - bbox[0]
        text_h = bbox[3] - bbox[1]
        x = (size - text_w) // 2 - bbox[0]
        y = (size - text_h) // 2 - bbox[1]

        # Small random offset (simulates imperfect centering)
        x += random.randint(-3, 3)
        y += random.randint(-3, 3)

        # Draw digit (white on black, like after thresholding)
        brightness = random.randint(200, 255)
        draw.text((x, y), digit_str, fill=brightness, font=font)

        # Random rotation (simulates slight camera angle)
        if random.random() > 0.3:
            angle = random.uniform(-12, 12)
            img = img.rotate(angle, fillcolor=0, resample=Image.BILINEAR)

        # Simulate camera/threshold artifacts
        if random.random() > 0.5:
            # Slight blur (simulates out-of-focus or low-res)
            img = img.filter(ImageFilter.GaussianBlur(radius=random.uniform(0.3, 1.0)))

        # Apply binary threshold (simulates adaptive thresholding)
        arr = np.array(img, dtype=np.float32)
        threshold = random.uniform(80, 140)
        arr = np.where(arr > threshold, 255.0, 0.0)

        # Add salt noise (simulates threshold artifacts)
        if random.random() > 0.6:
            noise_mask = np.random.random(arr.shape) < 0.01
            arr[noise_mask] = 255.0

        # Random erosion/dilation (simulates thick/thin printing)
        if random.random() > 0.7:
            from scipy.ndimage import binary_dilation, binary_erosion
            binary = arr > 128
            if random.random() > 0.5:
                binary = binary_dilation(binary, iterations=1)
            else:
                binary = binary_erosion(binary, iterations=1)
            arr = binary.astype(np.float32) * 255.0

        arr = arr / 255.0
        return arr.astype(np.float32)

    def __len__(self):
        return len(self.images)

    def __getitem__(self, idx):
        img = self.images[idx]
        label = self.labels[idx]
        tensor = torch.from_numpy(img).unsqueeze(0)  # [1, 28, 28]
        return tensor, label


def train():
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print(f"Training on: {device}")

    # All printed digits — no MNIST
    print("Generating synthetic printed digit training data...")
    train_dataset = PrintedDigitDataset(samples_per_digit=5000)

    print("Generating synthetic printed digit test data...")
    test_dataset = PrintedDigitDataset(samples_per_digit=500)

    train_loader = DataLoader(train_dataset, batch_size=128, shuffle=True, num_workers=0)
    test_loader = DataLoader(test_dataset, batch_size=256, shuffle=False, num_workers=0)

    model = DigitCNN().to(device)
    optimizer = optim.Adam(model.parameters(), lr=0.001)
    scheduler = optim.lr_scheduler.StepLR(optimizer, step_size=5, gamma=0.5)
    criterion = nn.CrossEntropyLoss()

    # Train for 15 epochs
    for epoch in range(15):
        model.train()
        correct = 0
        total = 0

        for images, labels in train_loader:
            images, labels = images.to(device), labels.to(device)
            optimizer.zero_grad()
            outputs = model(images)
            loss = criterion(outputs, labels)
            loss.backward()
            optimizer.step()

            _, predicted = outputs.max(1)
            total += labels.size(0)
            correct += predicted.eq(labels).sum().item()

        train_acc = 100.0 * correct / total
        scheduler.step()

        # Evaluate
        model.eval()
        correct = 0
        total = 0
        with torch.no_grad():
            for images, labels in test_loader:
                images, labels = images.to(device), labels.to(device)
                outputs = model(images)
                _, predicted = outputs.max(1)
                total += labels.size(0)
                correct += predicted.eq(labels).sum().item()

        test_acc = 100.0 * correct / total
        print(f"Epoch {epoch+1}/15 - Train: {train_acc:.1f}% - Test: {test_acc:.1f}%")

    # Export to ONNX
    model.eval()
    model.cpu()
    dummy_input = torch.randn(1, 1, 28, 28)
    output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mnist-cnn.onnx")

    torch.onnx.export(
        model,
        dummy_input,
        output_path,
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "batch"}, "output": {0: "batch"}},
        opset_version=12,
        dynamo=False,
    )

    size_kb = os.path.getsize(output_path) // 1024
    print(f"\nModel exported to: {output_path} ({size_kb} KB)")
    print(f"Final test accuracy: {test_acc:.2f}%")


if __name__ == "__main__":
    train()
