using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class DimensionAndDownsamplingCompareRunner {
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

    [MenuItem("FilamentMixer/Run Dimension & Downsampling Comparison (8D vs 12D vs 16D)")]
    public static void RunComparison() {
        Debug.Log("Starting Dimension (8D/12D/16D) & Pixel Downsampling Comparison Experiment...");

        int width = 1120;
        int headerHeight = 90;
        int pairCount = 5;

        // Section 1: Color mixing strips for 5 pairs (6 strips per pair)
        int stripHeight = 22;
        int stripGap = 2;
        int s1LabelHeight = 20;
        int s1BlockHeight = s1LabelHeight + 6 * (stripHeight + stripGap) + 12;
        int section1TotalHeight = s1BlockHeight * pairCount;

        // Section 2: Downsampling benchmark for 5 patterns (7 methods per pattern)
        int patternSize = 64;
        int s2RowHeight = 22;
        int s2RowGap = 2;
        int s2BlockHeight = s1LabelHeight + 7 * (s2RowHeight + s2RowGap) + 14;
        int section2TotalHeight = s2BlockHeight * pairCount;

        int totalHeight = headerHeight + section1TotalHeight + 40 + section2TotalHeight + 20;
        Texture2D finalTex = new Texture2D(width, totalHeight, TextureFormat.RGBA32, false);

        Color bgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        for (int y = 0; y < totalHeight; y++) {
            for (int x = 0; x < width; x++) {
                finalTex.SetPixel(x, y, bgColor);
            }
        }

        var pairs = new ColorPair[] {
            new ColorPair("PAIR 1: BLUE (0, 33, 133) + YELLOW (252, 211, 0)", new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f), new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f)),
            new ColorPair("PAIR 2: PURE BLUE (0, 0, 255) + PURE YELLOW (255, 255, 0)", new Color(0f, 0f, 1f, 1f), new Color(1f, 1f, 0f, 1f)),
            new ColorPair("PAIR 3: RED (255, 0, 0) + CYAN (0, 255, 255)", new Color(1f, 0f, 0f, 1f), new Color(0f, 1f, 1f, 1f)),
            new ColorPair("PAIR 4: GREEN (0, 255, 0) + MAGENTA (255, 0, 255)", new Color(0f, 1f, 0f, 1f), new Color(1f, 0f, 1f, 1f)),
            new ColorPair("PAIR 5: RED (255, 0, 0) + YELLOW (255, 255, 0)", new Color(1f, 0f, 0f, 1f), new Color(1f, 1f, 0f, 1f))
        };

        // Draw Main Header
        DrawHeader(finalTex, width, totalHeight);

        int currentY = totalHeight - headerHeight;

        // ==========================================
        // SECTION 1: DIMENSION REDUCTION COMPARISON
        // ==========================================
        DrawString(finalTex, 20, currentY - 18, "=== SECTION A: DIMENSION REDUCTION ACCURACY (38D vs 16D vs 12D vs 8D vs POLYMIXER) ===", new Color(1f, 0.85f, 0.35f), 1);
        currentY -= 28;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DIMENSION REDUCTION DELTA-E BENCHMARK (at t=0.5 vs 38D Reference) ===");
        sb.AppendLine("Pair\t\t\t\t8D\t12D\t16D\tPolyMixer\tsRGB");

        int stripWidth = width - 40;
        int startX = 20;

        for (int pIdx = 0; pIdx < pairCount; pIdx++) {
            var pair = pairs[pIdx];
            int blockTop = currentY - pIdx * s1BlockHeight;
            int labelY = blockTop - s1LabelHeight + 2;

            // Precompute t=0.5 colors and DeltaE
            Color c38 = SpectralMixerN.D38.Lerp(pair.c1, pair.c2, 0.5f);
            Color c16 = SpectralMixerN.D16.Lerp(pair.c1, pair.c2, 0.5f);
            Color c12 = SpectralMixerN.D12.Lerp(pair.c1, pair.c2, 0.5f);
            Color c8 = SpectralMixerN.D8.Lerp(pair.c1, pair.c2, 0.5f);
            Color cPoly = PolyMixer.Lerp(pair.c1, pair.c2, 0.5f);
            Color cRgb = Color.Lerp(pair.c1, pair.c2, 0.5f);

            float dE16 = DeltaE(c38, c16);
            float dE12 = DeltaE(c38, c12);
            float dE8 = DeltaE(c38, c8);
            float dEPoly = DeltaE(c38, cPoly);
            float dERgb = DeltaE(c38, cRgb);

            sb.AppendLine($"{pair.name.Substring(0, Mathf.Min(30, pair.name.Length))}\t{dE8:F2}\t{dE12:F2}\t{dE16:F2}\t{dEPoly:F2}\t{dERgb:F2}");

            DrawString(finalTex, startX, labelY, pair.name, new Color(1f, 0.88f, 0.40f), 1);
            string stats = $"DELTA-E: 8D={dE8:F1} | 12D={dE12:F1} | 16D={dE16:F1} | POLY={dEPoly:F1} | RGB={dERgb:F1}";
            DrawString(finalTex, startX + 540, labelY, stats, new Color(0.7f, 0.85f, 0.95f), 1);

            int stripTop = blockTop - s1LabelHeight;

            string[] s1Names = new string[] {
                "[1] 38D (REF)", "[2] 16D (FLOAT4x4)", "[3] 12D (FLOAT4x3)", "[4] 8D (FLOAT4x2)", "[5] POLYMIXER", "[6] RGB LERP"
            };
            Color[] s1TagCols = new Color[] {
                new Color(1f, 0.75f, 0.2f), new Color(0.3f, 0.85f, 0.95f), new Color(0.3f, 0.7f, 0.95f), new Color(0.2f, 0.55f, 0.95f), new Color(0.25f, 0.95f, 0.45f), new Color(0.95f, 0.35f, 0.35f)
            };

            for (int sIdx = 0; sIdx < 6; sIdx++) {
                int sY = stripTop - (sIdx + 1) * (stripHeight + stripGap);

                for (int sx = 0; sx < stripWidth; sx++) {
                    float t = (float)sx / (stripWidth - 1);
                    Color col;
                    if (sIdx == 0) col = SpectralMixerN.D38.Lerp(pair.c1, pair.c2, t);
                    else if (sIdx == 1) col = SpectralMixerN.D16.Lerp(pair.c1, pair.c2, t);
                    else if (sIdx == 2) col = SpectralMixerN.D12.Lerp(pair.c1, pair.c2, t);
                    else if (sIdx == 3) col = SpectralMixerN.D8.Lerp(pair.c1, pair.c2, t);
                    else if (sIdx == 4) col = PolyMixer.Lerp(pair.c1, pair.c2, t);
                    else col = Color.Lerp(pair.c1, pair.c2, t);

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

        currentY -= section1TotalHeight + 20;

        // ==========================================
        // SECTION 2: PIXEL DOWNSAMPLING BENCHMARK
        // ==========================================
        DrawString(finalTex, 20, currentY - 18, "=== SECTION B: PIXEL DOWNSAMPLING ACCURACY (64X64 -> 1X1) ===", new Color(1f, 0.85f, 0.35f), 1);
        currentY -= 28;

        string[] mNames = new string[] {
            "[1] SRGB BOX AVG",
            "[2] LINEAR RGB AVG",
            "[3] POLYMIXER HIERARCHICAL",
            "[4] 8D SPECTRAL (FLOAT4x2)",
            "[5] 12D SPECTRAL (FLOAT4x3)",
            "[6] 16D SPECTRAL (FLOAT4x4)",
            "[7] 38D SPECTRAL (GROUND TRUTH)"
        };

        Color[] mColors = new Color[] {
            new Color(0.95f, 0.35f, 0.35f),
            new Color(0.95f, 0.60f, 0.35f),
            new Color(0.25f, 0.95f, 0.45f),
            new Color(0.20f, 0.55f, 0.95f),
            new Color(0.30f, 0.70f, 0.95f),
            new Color(0.30f, 0.85f, 0.95f),
            new Color(1.00f, 0.75f, 0.20f)
        };

        for (int pIdx = 0; pIdx < pairCount; pIdx++) {
            var pair = pairs[pIdx];
            int blockTop = currentY - pIdx * s2BlockHeight;
            int labelY = blockTop - s1LabelHeight + 2;

            DrawString(finalTex, startX, labelY, pair.name + " [50:50 CHECKERBOARD]", new Color(1f, 0.88f, 0.40f), 1);

            // Generate 64x64 checkerboard
            Color[] baseGrid = new Color[patternSize * patternSize];
            for (int y = 0; y < patternSize; y++) {
                for (int x = 0; x < patternSize; x++) {
                    int check = ((x / 2) + (y / 2)) % 2;
                    baseGrid[y * patternSize + x] = (check == 0) ? pair.c1 : pair.c2;
                }
            }

            // Preview pattern (80x80)
            int prevSize = 80;
            int prevX = startX;
            int prevY = blockTop - s1LabelHeight - prevSize;
            DrawTextureBlit(finalTex, baseGrid, patternSize, patternSize, prevX, prevY, prevSize, prevSize);
            DrawRectOutline(finalTex, prevX - 1, prevY - 1, prevSize + 2, prevSize + 2, new Color(0.35f, 0.38f, 0.45f));
            DrawString(finalTex, prevX + 8, prevY - 14, "INPUT 64X64", new Color(0.7f, 0.75f, 0.8f), 1);

            // Compute ground truth 1x1 color using 38D Spectral
            Color gtColor = SpectralMixerN.D38.Mix(new Color[] { pair.c1, pair.c2 });

            int rowStartX = prevX + prevSize + 24;

            for (int m = 0; m < 7; m++) {
                int rowY = blockTop - s1LabelHeight - (m + 1) * (s2RowHeight + s2RowGap);

                Color final1x1;
                if (m == 0) {
                    // sRGB Box
                    final1x1 = Color.Lerp(pair.c1, pair.c2, 0.5f);
                } else if (m == 1) {
                    // Linear RGB Box
                    float lr = (Mathf.GammaToLinearSpace(pair.c1.r) + Mathf.GammaToLinearSpace(pair.c2.r)) * 0.5f;
                    float lg = (Mathf.GammaToLinearSpace(pair.c1.g) + Mathf.GammaToLinearSpace(pair.c2.g)) * 0.5f;
                    float lb = (Mathf.GammaToLinearSpace(pair.c1.b) + Mathf.GammaToLinearSpace(pair.c2.b)) * 0.5f;
                    final1x1 = new Color(Mathf.LinearToGammaSpace(lr), Mathf.LinearToGammaSpace(lg), Mathf.LinearToGammaSpace(lb), 1f);
                } else if (m == 2) {
                    // Hierarchical PolyMixer
                    final1x1 = PolyMixer.Lerp(pair.c1, pair.c2, 0.5f);
                } else if (m == 3) {
                    // 8D Spectral
                    final1x1 = SpectralMixerN.D8.Mix(new Color[] { pair.c1, pair.c2 });
                } else if (m == 4) {
                    // 12D Spectral
                    final1x1 = SpectralMixerN.D12.Mix(new Color[] { pair.c1, pair.c2 });
                } else if (m == 5) {
                    // 16D Spectral
                    final1x1 = SpectralMixerN.D16.Mix(new Color[] { pair.c1, pair.c2 });
                } else {
                    // 38D Spectral
                    final1x1 = gtColor;
                }

                float dE = DeltaE(gtColor, final1x1);

                DrawLegendItem(finalTex, rowStartX, rowY + 5, mNames[m], mColors[m]);

                // Render 1x1 swatch (48x18)
                int swatchX = rowStartX + 310;
                for (int dy = 0; dy < 18; dy++) {
                    for (int dx = 0; dx < 48; dx++) {
                        finalTex.SetPixel(swatchX + dx, rowY + 2 + dy, final1x1);
                    }
                }
                DrawRectOutline(finalTex, swatchX - 1, rowY + 1, 50, 20, Color.white);

                int r255 = Mathf.RoundToInt(final1x1.r * 255f);
                int g255 = Mathf.RoundToInt(final1x1.g * 255f);
                int b255 = Mathf.RoundToInt(final1x1.b * 255f);
                Color.RGBToHSV(final1x1, out float h, out float s, out float v);

                string rgbStr = $"RGB({r255,3},{g255,3},{b255,3})";
                string dEStr = (m == 6) ? "DELTA-E: 0.00 (REF)" : $"DELTA-E: {dE,5:F2}";
                string hsvStr = $"S:{Mathf.RoundToInt(s * 100f),2}% V:{Mathf.RoundToInt(v * 100f),2}%";

                DrawString(finalTex, swatchX + 60, rowY + 6, rgbStr, new Color(0.9f, 0.92f, 0.95f), 1);
                DrawString(finalTex, swatchX + 220, rowY + 6, dEStr, (dE < 3.0f) ? new Color(0.3f, 0.95f, 0.4f) : new Color(0.85f, 0.88f, 0.92f), 1);
                DrawString(finalTex, swatchX + 390, rowY + 6, hsvStr, new Color(0.70f, 0.75f, 0.82f), 1);
            }
        }

        finalTex.Apply();

        string outDir = Path.Combine(Application.dataPath, "Artifacts");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "Dimension_Downsampling_Comparison.png");
        byte[] pngBytes = finalTex.EncodeToPNG();
        File.WriteAllBytes(outPath, pngBytes);

        Debug.Log(sb.ToString());
        Debug.Log($"Successfully generated Dimension & Downsampling comparison PNG at: {outPath} ({pngBytes.Length} bytes)");
    }

    #region private methods

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

    private static void DrawHeader(Texture2D tex, int w, int h) {
        DrawString(tex, 20, h - 28, "SPECTRAL DIMENSION BENCHMARK: 8D vs 12D vs 16D vs 38D & DOWNSAMPLING", Color.white, 2);
        DrawString(tex, 20, h - 48, "EVALUATING ACCURACY GAIN OF HIGHER COARSE BANDS & EFFECT ON 64X64 -> 1X1 REDUCTION", new Color(0.65f, 0.70f, 0.78f), 1);

        int legendY = h - 75;
        DrawLegendItem(tex, 20, legendY, "38D REF (1000 ALU)", new Color(1.00f, 0.75f, 0.20f));
        DrawLegendItem(tex, 220, legendY, "16D (FLOAT4x4 / 480 ALU)", new Color(0.30f, 0.85f, 0.95f));
        DrawLegendItem(tex, 480, legendY, "12D (FLOAT4x3 / 360 ALU)", new Color(0.30f, 0.70f, 0.95f));
        DrawLegendItem(tex, 740, legendY, "8D (FLOAT4x2 / 240 ALU)", new Color(0.20f, 0.55f, 0.95f));
        DrawLegendItem(tex, 970, legendY, "POLYMIXER", new Color(0.25f, 0.95f, 0.45f));
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
            case '|': return new byte[] { 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
            default:  return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }
    }

    #endregion
}
