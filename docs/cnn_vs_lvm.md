# Why OpenCV + a Small CNN Beats a VLM at Sudoku Transcription

A short field note on an experiment: feeding a photo of a printed Sudoku to
several vision-language models (VLMs) and comparing them against a classical
pipeline — OpenCV for grid detection plus a small CNN for digit classification.

The classical pipeline, a few hundred kilobytes of weights, transcribed the
grid correctly. Every VLM tested — including models in the 7B–30B range — failed,
despite reading nearly every individual digit correctly. This document explains
why, because the *reason* generalizes well beyond Sudoku.

## TL;DR

Transcribing a Sudoku is not one problem. It is two:

1. **Localization** — figuring out which of the 81 cells each digit belongs to.
2. **Classification** — figuring out which digit (or blank) is in a given cell.

VLMs entangle these two in a single learned forward pass. On a clean, flat scan
that is fine. On a real photo — warped page, glare, faint interior gridlines,
show-through from the reverse side — the localization half degrades and
contaminates the result, even when classification is essentially perfect.

The OpenCV + CNN pipeline *factors the two apart* and hands each to the tool
with the right inductive bias: geometry to OpenCV, glyph recognition to the CNN.
That structural separation, not model size, is what wins.

## The test image

The source was a phone photo of a printed Sudoku from a puzzle book. It had
every property that makes real-world document parsing hard:

- **Perspective warp** — the page was curved, so the grid bowed and the right
  edge slanted. Cell boundaries were not straight or evenly spaced.
- **Glare** — a bright band washed across the lower-middle of the grid.
- **Show-through** — faint digits from the reverse side of the page bled
  through, offering false positives.

A flat, high-contrast scan would have been far easier. The point of using a hard
image is that it separates models that *truly localize* from models that
*approximately localize and hope*.

## What the VLMs actually did

Three distinct failure modes showed up, in rough order of severity.

### 1. Structure collapse (most severe)

One model returned a 4×4 HTML table instead of a 9×9 grid. It sampled real
digits from across the image and crammed them into a container roughly a quarter
of the correct size, dropping every blank. This is a total failure of
localization: the model never established a real 9×9 lattice to register against.
(This was also likely aggravated by an image path that wasn't doing true
high-resolution vision — worth ruling out whenever a model fails this badly.)

### 2. Hallucinated logic on top of OCR

A 30B model read the digits, then *invented* validity errors that did not exist
— claiming duplicate digits in rows and columns that, on inspection, did not even
share a row or column. It then "assumed typos" to rescue its own conclusion and
produced confidence percentages ("100% certain", "75%+") for cells. This is
pattern-matching to *what careful Sudoku analysis sounds like* without doing the
underlying bookkeeping. The tell: its claimed coordinates didn't parse against
its own stated rules.

### 3. Coordinate drift / blank suppression (most instructive)

The strongest model read **nearly every digit correctly** but could not place
them. Its consistent error was to drop leading and interior blank cells, slide
the real digits leftward, and pad the right end with zeros — within each row.

For example, a true row of `0 5 0 3 7 0 0 0 0` came back as `5 0 3 7 0 0 0 0 0`:
the leading blank was swallowed and everything shifted one position left. Because
a blank cell has no visual token to latch onto, a model that transcribes "the
sequence of digits I see" (rather than "the value at each fixed position") will
skip it. The gap then reappears as padding at the end, and the misalignment
cascades.

Crucially, this failure was **prompt-resistant**. Switching from a single
81-character string to a row-by-row format (which re-anchors at each row
boundary) did not fix it, because the drift was happening *within* each row, not
across rows. When two reasonable prompt formats produce the identical
"left-pack, drop blanks" behavior, you've found a capability ceiling, not a
wording problem.

> Note the ranking: the *smaller* model (7B-class) outperformed the 30B one.
> Raw parameter count was not the deciding variable.

## Why the classical pipeline wins

The OpenCV + CNN approach decomposes the problem the way it should be
decomposed, and gives the hard part to the right tool.

### Step 1 — OpenCV handles localization, deterministically

- Find the grid contour.
- Apply a perspective transform to de-warp it into a flat square.
- Divide that square into an exact 9×9 of equal cells.

This is the precise step every VLM failed at — column/row registration on a
bowed grid — solved with classical geometry instead of asking a neural net to
*infer* it. The output is 81 cleanly isolated cell crops at known positions.

### Step 2 — the CNN handles classification, trivially

Each isolated crop poses a near-MNIST question: *is this a printed digit 1–9, or
empty?* A tiny CNN saturates that task. There is nothing exotic left to learn,
because position has already been solved upstream.

### Why this cannot drift

The pipeline **structurally cannot** left-pack or skip blanks. Position is
handled by grid geometry, not by the network. A blank cell is simply a crop the
classifier labels "empty" — there is no sequence to skip within, so there is no
cascade. The single failure mode that defeated every VLM is *architecturally
impossible* here.

## The real lesson

The capability that matters is not model capacity — it's whether the
**localization step is explicit and reliable**.

| Property | OpenCV + CNN | End-to-end VLM |
|---|---|---|
| Localization | Explicit (geometry) | Implicit (learned, fuzzy) |
| Classification | Isolated, trivial | Entangled with localization |
| Blank handling | A label, can't cascade | A skipped token, cascades |
| Failure style | Sharp (breaks on novelty) | Graceful (degrades on novelty) |
| Robustness to warp/glare | High (de-warp is upstream) | Low (corrupts registration) |
| Generalization | Narrow (printed grids only) | Broad (handwriting, angles, prose) |
| Size needed for this task | Kilobytes | Billions of params, still fails |

The VLMs didn't fail from lack of capacity. They failed because they fold
localization into a learned, fuzzy process that the image quality defeated. The
classical pipeline externalizes that step, and the capacity requirement
collapses to almost nothing.

This reframes the obvious question. "What's the smallest model that can read
this puzzle?" has no clean answer — but "a tiny CNN, *if you let classical CV do
the localization*" does. The bottleneck was never recognition. It was registration.

## When the trade-off flips

None of this means the classical pipeline is universally better. It is *narrow
and exact*; the VLM is *broad and approximate*. The pipeline breaks the moment
the input stops being "a printed 9×9 grid recoverable by a contour finder." The
VLM's robustness starts to win back the advantage on:

- A grid photographed at a steep angle (does contour-find + perspective
  transform still recover it?).
- Uneven lighting that breaks cell-level thresholding.
- A different font or weight than the CNN trained on.
- Touching or clipped digits near cell borders.
- Inputs that aren't grids at all — handwritten puzzles, Sudokus described in
  prose, grids with margin notes.

Narrow pipelines fail sharply on novelty; general models degrade gracefully.
Pick the bet that matches your input distribution.

## Practical takeaways

- **Decompose before you scale.** If a task has a clean geometric or structural
  sub-problem, solve that explicitly rather than asking one model to learn it
  end-to-end. The structural guarantee is worth more than parameters.
- **Fix the image before the prompt.** For VLM transcription of warped
  documents, a de-warp + tight crop helps more than any prompt wording, because
  the bottleneck is registration, not legibility.
- **Separate perception from reasoning when benchmarking VLMs.** Ask only for
  transcription (an 81-char string), then check validity in code. Mixing the two
  measures reasoning when you meant to measure vision.
- **Don't read confidence as correctness.** The model that produced the most
  authoritative-looking analysis (confidence percentages, "certain" cells) was
  also the one that hallucinated nonexistent rule violations.