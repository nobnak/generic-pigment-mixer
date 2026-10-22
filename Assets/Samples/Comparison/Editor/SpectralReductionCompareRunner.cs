using System;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class SpectralReductionCompareRunner {
    private struct ColorPair {
        public string name;
        public Color c1;
        public Color c2;

        public ColorPair(string name, Color c1, Color c2) {
            this.name = name;
            this.c1 = c1;
            this.c2 = c2;
        }
    }

    [MenuItem("FilamentMixer/Run Spectral 8D Comparison")]
    public static void RunComparison() {
        Debug.Log("Starting Spectral.js 38D vs 8D Dimension Reduction Comparison Experiment...");

        int width = 960;
        int pairCount = 5;
        int headerHeight = 96;
        int labelHeight = 22;
        int stripHeight = 26;
        int stripGap = 3;
        int blockGap = 16;
        int blockContentHeight = labelHeight + 4 * (stripHeight + stripGap);
        int totalBlockHeight = blockContentHeight + blockGap;
        int height = headerHeight + totalBlockHeight * pairCount + 16;

        var pairs = new ColorPair[] {
            new ColorPair(
                "PAIR 1: BLUE (0, 33, 133) + YELLOW (252, 211, 0)  [CANONICAL MIXBOX TEST]",
                new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f),
                new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f)
            ),
            new ColorPair(
                "PAIR 2: PURE BLUE (0, 0, 255) + PURE YELLOW (255, 255, 0)  [PRIMARY EXTREME]",
                new Color(0f, 0f, 1f, 1f),
                new Color(1f, 1f, 0f, 1f)
            ),
            new ColorPair(
                "PAIR 3: RED (255, 0, 0) + CYAN (0, 255, 255)  [COMPLEMENTARY 1]",
                new Color(1f, 0f, 0f, 1f),
                new Color(0f, 1f, 1f, 1f)
            ),
            new ColorPair(
                "PAIR 4: GREEN (0, 255, 0) + MAGENTA (255, 0, 255)  [COMPLEMENTARY 2]",
                new Color(0f, 1f, 0f, 1f),
                new Color(1f, 0f, 1f, 1f)
            ),
            new ColorPair(
                "PAIR 5: RED (255, 0, 0) + YELLOW (255, 255, 0)  [WARM GRADIENT]",
                new Color(1f, 0f, 0f, 1f),
                new Color(1f, 1f, 0f, 1f)
            )
        };

        int stripWidth = width - 40;
        int startX = 20;

        Texture2D finalTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color bgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                finalTex.SetPixel(x, y, bgColor);
            }
        }

        DrawHeader(finalTex, width, height);

        for (int pIdx = 0; pIdx < pairCount; pIdx++) {
            var pair = pairs[pIdx];
            int blockBottom = height - headerHeight - (pIdx + 1) * totalBlockHeight;
            int blockTop = blockBottom + blockContentHeight;

            // Draw Label bar
            int labelY = blockTop - labelHeight + 4;
            DrawString(finalTex, startX, labelY, pair.name, new Color(1f, 0.88f, 0.40f), 1);

            int stripTop = blockTop - labelHeight;

            // Compute t=0.5 colors for stats
            Color mid38 = SpectralMixer.Lerp(pair.c1, pair.c2, 0.5f);
            Color mid8 = SpectralMixer8.Lerp(pair.c1, pair.c2, 0.5f);
            float deltaE = DeltaE(mid38, mid8);

            string deltaStr = $"DELTA-E (38D vs 8D at t=0.5): {deltaE:F2}";
            DrawString(finalTex, startX + 620, labelY, deltaStr, new Color(0.7f, 0.85f, 0.95f), 1);

            for (int sIdx = 0; sIdx < 4; sIdx++) {
                int sY = stripTop - (sIdx + 1) * (stripHeight + stripGap);

                for (int sx = 0; sx < stripWidth; sx++) {
                    float t = (float)sx / (stripWidth - 1);
                    Color col;

                    if (sIdx == 0) {
                        // [1] Spectral.js 38D (Original Reference)
                        col = SpectralMixer.Lerp(pair.c1, pair.c2, t);
                    } else if (sIdx == 1) {
                        // [2] Spectral.js 8D (Reduced Coarse Bands)
                        col = SpectralMixer8.Lerp(pair.c1, pair.c2, t);
                    } else if (sIdx == 2) {
                        // [3] PolyMixer (4th-order polynomial)
                        col = PolyMixer.Lerp(pair.c1, pair.c2, t);
                    } else {
                        // [4] sRGB Additive Lerp
                        col = Color.Lerp(pair.c1, pair.c2, t);
                    }

                    for (int dy = 0; dy < stripHeight; dy++) {
                        Color pixelCol = col;
                        if (dy == 0 || dy == stripHeight - 1 || sx == 0 || sx == stripWidth - 1) {
                            pixelCol *= 0.65f;
                            pixelCol.a = 1f;
                        }
                        finalTex.SetPixel(startX + sx, sY + dy, pixelCol);
                    }
                }
            }
        }

        finalTex.Apply();

        string outDir = Path.Combine(Application.dataPath, "Artifacts");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "Spectral8_Comparison.png");
        byte[] pngBytes = finalTex.EncodeToPNG();
        File.WriteAllBytes(outPath, pngBytes);
        Debug.Log($"Successfully generated Spectral 8D comparison PNG at: {outPath} ({pngBytes.Length} bytes)");
    }

    #region private methods

    private static float DeltaE(Color c1, Color c2) {
        // CIE76 Color Difference approximation in Lab space
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

    private static void DrawHeader(Texture2D tex, int w, int h) {
        DrawString(tex, 20, h - 28, "SPECTRAL.JS 38-DIMENSION vs 8-DIMENSION REDUCTION BENCHMARK", Color.white, 2);
        DrawString(tex, 20, h - 48, "WAVELENGTH REDUCTION: 38 BANDS (1,000+ ALU) -> 8 BANDS (FLOAT4x2 SIMD / ~240 ALU)", new Color(0.65f, 0.70f, 0.78f), 1);

        int legendY = h - 75;
        DrawLegendItem(tex, 20, legendY, "[1] SPECTRAL.JS 38D (ORIGINAL)", new Color(1.00f, 0.75f, 0.20f));
        DrawLegendItem(tex, 280, legendY, "[2] SPECTRAL.JS 8D (COARSE BANDS)", new Color(0.20f, 0.85f, 0.95f));
        DrawLegendItem(tex, 560, legendY, "[3] POLYMIXER (HLSL)", new Color(0.25f, 0.95f, 0.45f));
        DrawLegendItem(tex, 760, legendY, "[4] RGB LERP (ADDITIVE)", new Color(0.95f, 0.35f, 0.35f));
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
