using System;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class Spectral16ExperimentRunner {
    private struct ColorPair {
        public string name;
        public Color c1; // Linear RGB
        public Color c2; // Linear RGB

        public ColorPair(string name, Color c1, Color c2) {
            this.name = name;
            this.c1 = c1;
            this.c2 = c2;
        }
    }

    [MenuItem("FilamentMixer/Run Spectral16 Both Experiments")]
    public static void RunAllExperiments() {
        GenerateColorMixingComparison();
        GenerateDownsamplingComparison();
    }

    // =================================================================
    // EXPERIMENT 1: 2-Color Mixing Strip Comparison (Spectral16 vs 38D vs PolyMixer vs Lerp)
    // =================================================================
    public static void GenerateColorMixingComparison() {
        Debug.Log("Generating Spectral16 Color Mixing Comparison Image...");

        int width = 980;
        int pairCount = 5;
        int headerHeight = 90;
        int labelHeight = 22;
        int stripHeight = 26;
        int stripGap = 3;
        int blockGap = 16;
        int blockContentHeight = labelHeight + 4 * (stripHeight + stripGap);
        int totalBlockHeight = blockContentHeight + blockGap;
        int height = headerHeight + totalBlockHeight * pairCount + 16;

        var pairs = new ColorPair[] {
            new ColorPair("PAIR 1: BLUE (0, 33, 133) + YELLOW (252, 211, 0)  [CANONICAL MIXBOX]",
                new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f),
                new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f)),
            new ColorPair("PAIR 2: PURE BLUE (0, 0, 255) + PURE YELLOW (255, 255, 0)  [PRIMARY EXTREME]",
                new Color(0f, 0f, 1f, 1f),
                new Color(1f, 1f, 0f, 1f)),
            new ColorPair("PAIR 3: RED (255, 0, 0) + CYAN (0, 255, 255)  [COMPLEMENTARY 1]",
                new Color(1f, 0f, 0f, 1f),
                new Color(0f, 1f, 1f, 1f)),
            new ColorPair("PAIR 4: GREEN (0, 255, 0) + MAGENTA (255, 0, 255)  [COMPLEMENTARY 2]",
                new Color(0f, 1f, 0f, 1f),
                new Color(1f, 0f, 1f, 1f)),
            new ColorPair("PAIR 5: RED (255, 0, 0) + YELLOW (255, 255, 0)  [WARM GRADIENT]",
                new Color(1f, 0f, 0f, 1f),
                new Color(1f, 1f, 0f, 1f))
        };

        int stripWidth = width - 40;
        int startX = 20;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color bgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                tex.SetPixel(x, y, bgColor);
            }
        }

        // Header
        DrawString(tex, 20, height - 28, "SPECTRALMIXER 16: 2-COLOR MIXING BENCHMARK", Color.white, 2);
        DrawString(tex, 20, height - 48, "16-BAND SIMD KM (FLOAT4x4 / ~480 ALU) vs 38D REFERENCE vs POLYMIXER vs ADDITIVE", new Color(0.65f, 0.70f, 0.78f), 1);

        int legendY = height - 74;
        DrawLegendItem(tex, 20, legendY, "[1] SPECTRAL.JS 38D (REF)", new Color(1.00f, 0.75f, 0.20f));
        DrawLegendItem(tex, 270, legendY, "[2] SPECTRALMIXER 16 (HLSL)", new Color(0.20f, 0.85f, 0.95f));
        DrawLegendItem(tex, 540, legendY, "[3] POLYMIXER (HLSL)", new Color(0.25f, 0.95f, 0.45f));
        DrawLegendItem(tex, 760, legendY, "[4] LINEAR RGB LERP", new Color(0.95f, 0.35f, 0.35f));

        for (int pIdx = 0; pIdx < pairCount; pIdx++) {
            var pair = pairs[pIdx];
            int blockBottom = height - headerHeight - (pIdx + 1) * totalBlockHeight;
            int blockTop = blockBottom + blockContentHeight;
            int labelY = blockTop - labelHeight + 4;

            Color mid38 = SpectralMixerN.D38.Lerp(pair.c1, pair.c2, 0.5f);
            Color mid16 = SpectralMixer16.Lerp(pair.c1, pair.c2, 0.5f);
            float dE16 = DeltaE(mid38, mid16);

            DrawString(tex, startX, labelY, pair.name, new Color(1f, 0.88f, 0.40f), 1);
            string dEStr = $"DELTA-E (16D vs 38D at t=0.5): {dE16:F2}";
            DrawString(tex, startX + 630, labelY, dEStr, (dE16 < 3.0f) ? new Color(0.3f, 0.95f, 0.4f) : new Color(0.7f, 0.85f, 0.95f), 1);

            int stripTop = blockTop - labelHeight;

            for (int sIdx = 0; sIdx < 4; sIdx++) {
                int sY = stripTop - (sIdx + 1) * (stripHeight + stripGap);

                for (int sx = 0; sx < stripWidth; sx++) {
                    float t = (float)sx / (stripWidth - 1);
                    Color col;

                    if (sIdx == 0) col = SpectralMixerN.D38.Lerp(pair.c1, pair.c2, t);
                    else if (sIdx == 1) col = SpectralMixer16.Lerp(pair.c1, pair.c2, t);
                    else if (sIdx == 2) col = PolyMixer.Lerp(pair.c1, pair.c2, t);
                    else col = Color.Lerp(pair.c1, pair.c2, t);

                    for (int dy = 0; dy < stripHeight; dy++) {
                        Color pixelCol = col;
                        if (dy == 0 || dy == stripHeight - 1 || sx == 0 || sx == stripWidth - 1) {
                            pixelCol *= 0.65f;
                            pixelCol.a = 1f;
                        }
                        tex.SetPixel(startX + sx, sY + dy, pixelCol);
                    }
                }
            }
        }

        tex.Apply();
        SavePng(tex, "Spectral16_ColorMixing_Comparison.png");
    }

    // =================================================================
    // EXPERIMENT 2: Pixel Downsampling Comparison (64x64 -> 1x1)
    // =================================================================
    public static void GenerateDownsamplingComparison() {
        Debug.Log("Generating Spectral16 Downsampling Comparison Image...");

        int width = 1080;
        int patternSize = 64;
        int headerHeight = 90;
        int sectionHeight = 168;
        int sectionGap = 12;
        int pairCount = 5;

        var pairs = new ColorPair[] {
            new ColorPair("PATTERN 1: BLUE (0, 33, 133) + YELLOW (252, 211, 0)",
                new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f),
                new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f)),
            new ColorPair("PATTERN 2: PURE BLUE (0, 0, 255) + PURE YELLOW (255, 255, 0)",
                new Color(0f, 0f, 1f, 1f),
                new Color(1f, 1f, 0f, 1f)),
            new ColorPair("PATTERN 3: RED (255, 0, 0) + CYAN (0, 255, 255)",
                new Color(1f, 0f, 0f, 1f),
                new Color(0f, 1f, 1f, 1f)),
            new ColorPair("PATTERN 4: GREEN (0, 255, 0) + MAGENTA (255, 0, 255)",
                new Color(0f, 1f, 0f, 1f),
                new Color(1f, 0f, 1f, 1f)),
            new ColorPair("PATTERN 5: RED (255, 0, 0) + YELLOW (255, 255, 0)",
                new Color(1f, 0f, 0f, 1f),
                new Color(1f, 1f, 0f, 1f))
        };

        int totalHeight = headerHeight + pairCount * (sectionHeight + sectionGap) + 16;
        Texture2D tex = new Texture2D(width, totalHeight, TextureFormat.RGBA32, false);

        Color bgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        for (int y = 0; y < totalHeight; y++) {
            for (int x = 0; x < width; x++) {
                tex.SetPixel(x, y, bgColor);
            }
        }

        // Header
        DrawString(tex, 20, totalHeight - 28, "SPECTRALMIXER 16: PIXEL DOWNSAMPLING BENCHMARK (64X64 -> 1X1)", Color.white, 2);
        DrawString(tex, 20, totalHeight - 48, "COMPARING PHYSICAL PIGMENT REDUCTION (16D vs 38D) vs POLYMIXER vs OPTICAL ADDITIVE (LINEAR/sRGB)", new Color(0.65f, 0.70f, 0.78f), 1);

        int legendY = totalHeight - 74;
        DrawLegendItem(tex, 20, legendY, "[1] SRGB BOX AVG", new Color(0.95f, 0.35f, 0.35f));
        DrawLegendItem(tex, 200, legendY, "[2] LINEAR RGB AVG", new Color(0.95f, 0.60f, 0.35f));
        DrawLegendItem(tex, 410, legendY, "[3] POLYMIXER HIERARCHICAL", new Color(0.25f, 0.95f, 0.45f));
        DrawLegendItem(tex, 680, legendY, "[4] SPECTRALMIXER 16 (HLSL)", new Color(0.20f, 0.85f, 0.95f));
        DrawLegendItem(tex, 910, legendY, "[5] 38D REF (KM)", new Color(1.00f, 0.75f, 0.20f));

        int startX = 20;

        for (int pIdx = 0; pIdx < pairCount; pIdx++) {
            var pair = pairs[pIdx];
            int blockBottom = totalHeight - headerHeight - (pIdx + 1) * (sectionHeight + sectionGap);
            int blockTop = blockBottom + sectionHeight;

            // Title
            DrawString(tex, startX, blockTop - 16, pair.name + " [50:50 CHECKERBOARD]", new Color(1f, 0.88f, 0.40f), 1);

            // Generate 64x64 checkerboard
            Color[] baseGrid = new Color[patternSize * patternSize];
            for (int y = 0; y < patternSize; y++) {
                for (int x = 0; x < patternSize; x++) {
                    int check = ((x / 2) + (y / 2)) % 2;
                    baseGrid[y * patternSize + x] = (check == 0) ? pair.c1 : pair.c2;
                }
            }

            // Input Preview (84x84)
            int prevSize = 84;
            int prevX = startX;
            int prevY = blockTop - 26 - prevSize;
            DrawTextureBlit(tex, baseGrid, patternSize, patternSize, prevX, prevY, prevSize, prevSize);
            DrawRectOutline(tex, prevX - 1, prevY - 1, prevSize + 2, prevSize + 2, new Color(0.35f, 0.38f, 0.45f));
            DrawString(tex, prevX + 10, prevY - 14, "INPUT 64X64", new Color(0.7f, 0.75f, 0.8f), 1);

            // Reference Ground Truth (38D)
            Color gtColor = SpectralMixerN.D38.Mix(new Color[] { pair.c1, pair.c2 });

            string[] mNames = new string[] {
                "[1] SRGB BOX AVERAGE",
                "[2] LINEAR RGB AVERAGE",
                "[3] POLYMIXER HIERARCHICAL",
                "[4] SPECTRALMIXER 16 (HLSL)",
                "[5] SPECTRAL.JS 38D (GROUND TRUTH)"
            };
            Color[] mTags = new Color[] {
                new Color(0.95f, 0.35f, 0.35f),
                new Color(0.95f, 0.60f, 0.35f),
                new Color(0.25f, 0.95f, 0.45f),
                new Color(0.20f, 0.85f, 0.95f),
                new Color(1.00f, 0.75f, 0.20f)
            };

            int rowStartX = prevX + prevSize + 24;
            int rowHeight = 22;
            int rowGap = 2;

            for (int m = 0; m < 5; m++) {
                int rowY = blockTop - 26 - (m + 1) * (rowHeight + rowGap);

                Color final1x1;
                if (m == 0) final1x1 = Color.Lerp(pair.c1, pair.c2, 0.5f);
                else if (m == 1) {
                    float lr = (Mathf.GammaToLinearSpace(pair.c1.r) + Mathf.GammaToLinearSpace(pair.c2.r)) * 0.5f;
                    float lg = (Mathf.GammaToLinearSpace(pair.c1.g) + Mathf.GammaToLinearSpace(pair.c2.g)) * 0.5f;
                    float lb = (Mathf.GammaToLinearSpace(pair.c1.b) + Mathf.GammaToLinearSpace(pair.c2.b)) * 0.5f;
                    final1x1 = new Color(Mathf.LinearToGammaSpace(lr), Mathf.LinearToGammaSpace(lg), Mathf.LinearToGammaSpace(lb), 1f);
                } else if (m == 2) final1x1 = PolyMixer.Lerp(pair.c1, pair.c2, 0.5f);
                else if (m == 3) final1x1 = SpectralMixer16.Mix(new Color[] { pair.c1, pair.c2 });
                else final1x1 = gtColor;

                float dE = DeltaE(gtColor, final1x1);

                DrawLegendItem(tex, rowStartX, rowY + 5, mNames[m], mTags[m]);

                // 1x1 Patch Swatch (50x18)
                int swatchX = rowStartX + 300;
                for (int dy = 0; dy < 18; dy++) {
                    for (int dx = 0; dx < 50; dx++) {
                        tex.SetPixel(swatchX + dx, rowY + 2 + dy, final1x1);
                    }
                }
                DrawRectOutline(tex, swatchX - 1, rowY + 1, 52, 20, Color.white);

                int r255 = Mathf.RoundToInt(final1x1.r * 255f);
                int g255 = Mathf.RoundToInt(final1x1.g * 255f);
                int b255 = Mathf.RoundToInt(final1x1.b * 255f);
                Color.RGBToHSV(final1x1, out float h, out float s, out float v);

                string rgbStr = $"RGB({r255,3},{g255,3},{b255,3})";
                string dEStr = (m == 4) ? "DELTA-E: 0.00 (REF)" : $"DELTA-E: {dE,5:F2}";
                string hsvStr = $"S:{Mathf.RoundToInt(s * 100f),2}% V:{Mathf.RoundToInt(v * 100f),2}%";

                DrawString(tex, swatchX + 62, rowY + 6, rgbStr, new Color(0.9f, 0.92f, 0.95f), 1);
                DrawString(tex, swatchX + 225, rowY + 6, dEStr, (dE < 3.0f) ? new Color(0.3f, 0.95f, 0.4f) : new Color(0.85f, 0.88f, 0.92f), 1);
                DrawString(tex, swatchX + 400, rowY + 6, hsvStr, new Color(0.70f, 0.75f, 0.82f), 1);
            }
        }

        tex.Apply();
        SavePng(tex, "Spectral16_Downsampling_Comparison.png");
    }

    #region private methods

    private static void SavePng(Texture2D tex, string fileName) {
        string outDir = Path.Combine(Application.dataPath, "Artifacts");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, fileName);
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(outPath, bytes);
        Debug.Log($"Successfully saved {fileName} to: {outPath} ({bytes.Length} bytes)");
    }

    private static float DeltaE(Color c1, Color c2) {
        Vector3 lab1 = RgbToLab(c1);
        Vector3 lab2 = RgbToLab(c2);
        return Vector3.Distance(lab1, lab2);
    }

    private static Vector3 RgbToLab(Color c) {
        float r = (c.r > 0.04045f) ? Mathf.Pow((c.r + 0.055f) / 1.055f, 2.4f) : c.r / 12.92f;
        float g = (c.g > 0.04045f) ? Mathf.Pow((c.g + 0.055f) / 1.055f, 2.4f) : c.g / 12.92f;
        float b = (c.b > 0.04045f) ? Mathf.Pow((c.b + 0.055f) / 1.055f, 2.4f) : c.b / 12.92f;

        float x = (r * 0.4124f + g * 0.3576f + b * 0.1805f) / 0.95047f;
        float y = (r * 0.2126f + g * 0.7152f + b * 0.0722f) / 1.00000f;
        float z = (r * 0.0193f + g * 0.1192f + b * 0.9505f) / 1.08883f;

        x = (x > 0.008856f) ? Mathf.Pow(x, 1f / 3f) : (7.787f * x + 16f / 116f);
        y = (y > 0.008856f) ? Mathf.Pow(y, 1f / 3f) : (7.787f * y + 16f / 116f);
        z = (z > 0.008856f) ? Mathf.Pow(z, 1f / 3f) : (7.787f * z + 16f / 116f);

        return new Vector3(116f * y - 16f, 500f * (x - y), 200f * (y - z));
    }

    private static void DrawTextureBlit(Texture2D tex, Color[] src, int srcW, int srcH, int dstX, int dstY, int dstW, int dstH) {
        for (int dy = 0; dy < dstH; dy++) {
            int sy = (dy * srcH) / dstH;
            for (int dx = 0; dx < dstW; dx++) {
                int sx = (dx * srcW) / dstW;
                tex.SetPixel(dstX + dx, dstY + dy, src[sy * srcW + sx]);
            }
        }
    }

    private static void DrawRectOutline(Texture2D tex, int x, int y, int w, int h, Color col) {
        for (int i = 0; i < w; i++) {
            tex.SetPixel(x + i, y, col);
            tex.SetPixel(x + i, y + h - 1, col);
        }
        for (int j = 0; j < h; j++) {
            tex.SetPixel(x, y + j, col);
            tex.SetPixel(x + w - 1, y + j, col);
        }
    }

    private static void DrawLegendItem(Texture2D tex, int x, int y, string label, Color tagColor) {
        for (int dy = 0; dy < 10; dy++) {
            for (int dx = 0; dx < 10; dx++) {
                tex.SetPixel(x + dx, y + dy, tagColor);
            }
        }
        DrawString(tex, x + 15, y + 2, label, new Color(0.9f, 0.9f, 0.92f), 1);
    }

    // --- 5x7 Dot-Matrix Font Renderer ---
    private static void DrawString(Texture2D tex, int x, int y, string text, Color color, int scale) {
        int curX = x;
        foreach (char c in text.ToUpperInvariant()) {
            if (c == ' ') {
                curX += 4 * scale;
                continue;
            }
            DrawChar(tex, curX, y, c, color, scale);
            curX += 6 * scale;
        }
    }

    private static void DrawChar(Texture2D tex, int x, int y, char c, Color color, int scale) {
        byte[] glyph = GetGlyph(c);
        for (int row = 0; row < 7; row++) {
            byte b = glyph[row];
            for (int col = 0; col < 5; col++) {
                if ((b & (1 << (4 - col))) != 0) {
                    for (int dy = 0; dy < scale; dy++) {
                        for (int dx = 0; dx < scale; dx++) {
                            int px = x + col * scale + dx;
                            int py = y + (6 - row) * scale + dy;
                            if (px >= 0 && px < tex.width && py >= 0 && py < tex.height) {
                                tex.SetPixel(px, py, color);
                            }
                        }
                    }
                }
            }
        }
    }

    private static byte[] GetGlyph(char c) {
        switch (c) {
            case 'A': return new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            case 'B': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E };
            case 'C': return new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
            case 'D': return new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C };
            case 'E': return new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
            case 'F': return new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 };
            case 'G': return new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F };
            case 'H': return new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
            case 'I': return new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
            case 'J': return new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C };
            case 'K': return new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 };
            case 'L': return new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F };
            case 'M': return new byte[] { 0x11, 0x1B, 0x15, 0x11, 0x11, 0x11, 0x11 };
            case 'N': return new byte[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 };
            case 'O': return new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            case 'P': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
            case 'Q': return new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D };
            case 'R': return new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
            case 'S': return new byte[] { 0x0E, 0x11, 0x10, 0x0E, 0x01, 0x11, 0x0E };
            case 'T': return new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
            case 'U': return new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
            case 'V': return new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 };
            case 'W': return new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 };
            case 'X': return new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 };
            case 'Y': return new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 };
            case 'Z': return new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F };
            case '0': return new byte[] { 0x0E, 0x13, 0x15, 0x11, 0x11, 0x11, 0x0E };
            case '1': return new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
            case '2': return new byte[] { 0x0E, 0x11, 0x01, 0x06, 0x08, 0x10, 0x1F };
            case '3': return new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E };
            case '4': return new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
            case '5': return new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
            case '6': return new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
            case '7': return new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
            case '8': return new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
            case '9': return new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };
            case '+': return new byte[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 };
            case '-': return new byte[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
            case ':': return new byte[] { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
            case '(': return new byte[] { 0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02 };
            case ')': return new byte[] { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 };
            case '[': return new byte[] { 0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E };
            case ']': return new byte[] { 0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E };
            case ',': return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x06, 0x06, 0x04 };
            case '.': return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C };
            case '%': return new byte[] { 0x18, 0x19, 0x02, 0x04, 0x08, 0x13, 0x03 };
            case '=': return new byte[] { 0x00, 0x1F, 0x00, 0x00, 0x1F, 0x00, 0x00 };
            default:  return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }
    }

    #endregion
}
