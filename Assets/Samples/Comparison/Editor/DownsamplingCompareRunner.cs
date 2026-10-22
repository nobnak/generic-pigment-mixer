using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

public static class DownsamplingCompareRunner {
    private struct PatternDef {
        public string title;
        public string desc;
        public Color[] palette;
        public int patternType; // 0: 2-color checkerboard, 1: 3-color stipple

        public PatternDef(string title, string desc, Color[] palette, int patternType) {
            this.title = title;
            this.desc = desc;
            this.palette = palette;
            this.patternType = patternType;
        }
    }

    [MenuItem("FilamentMixer/Run Downsampling Comparison")]
    public static void RunComparison() {
        Debug.Log("Starting Downsampling & Pixel Mixing Comparison Experiment...");

        int imageWidth = 1060;
        int patternSize = 64; // Base pattern resolution
        int headerHeight = 90;
        int sectionHeight = 160;
        int sectionGap = 14;

        var patterns = new PatternDef[] {
            new PatternDef(
                "PATTERN 1: BLUE (0, 33, 133) + YELLOW (252, 211, 0)",
                "CANONICAL MIXBOX TEST (50:50 CHECKERBOARD)",
                new Color[] {
                    new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f),
                    new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f)
                },
                0
            ),
            new PatternDef(
                "PATTERN 2: PURE BLUE (0, 0, 255) + PURE YELLOW (255, 255, 0)",
                "EXTREME SATURATION PRIMARY TEST (50:50 CHECKERBOARD)",
                new Color[] {
                    new Color(0f, 0f, 1f, 1f),
                    new Color(1f, 1f, 0f, 1f)
                },
                0
            ),
            new PatternDef(
                "PATTERN 3: TRI-COLOR CMY (CYAN + MAGENTA + YELLOW)",
                "SUBTRACTIVE 3-PRIMARY STIPPLE (1:1:1 DOT PATTERN)",
                new Color[] {
                    new Color(0f, 1f, 1f, 1f),
                    new Color(1f, 0f, 1f, 1f),
                    new Color(1f, 1f, 0f, 1f)
                },
                1
            ),
            new PatternDef(
                "PATTERN 4: RED (255, 0, 0) + CYAN (0, 255, 255)",
                "COMPLEMENTARY PAIR 1 (50:50 CHECKERBOARD)",
                new Color[] {
                    new Color(1f, 0f, 0f, 1f),
                    new Color(0f, 1f, 1f, 1f)
                },
                0
            ),
            new PatternDef(
                "PATTERN 5: GREEN (0, 255, 0) + MAGENTA (255, 0, 255)",
                "COMPLEMENTARY PAIR 2 (50:50 CHECKERBOARD)",
                new Color[] {
                    new Color(0f, 1f, 0f, 1f),
                    new Color(1f, 0f, 1f, 1f)
                },
                0
            )
        };

        int totalHeight = headerHeight + patterns.Length * (sectionHeight + sectionGap) + 20;
        Texture2D finalTex = new Texture2D(imageWidth, totalHeight, TextureFormat.RGBA32, false);

        Color bgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        for (int y = 0; y < totalHeight; y++) {
            for (int x = 0; x < imageWidth; x++) {
                finalTex.SetPixel(x, y, bgColor);
            }
        }

        DrawHeader(finalTex, imageWidth, totalHeight);

        for (int pIdx = 0; pIdx < patterns.Length; pIdx++) {
            var pat = patterns[pIdx];
            int sectionBottom = totalHeight - headerHeight - (pIdx + 1) * (sectionHeight + sectionGap);
            DrawSection(finalTex, pat, sectionBottom, sectionHeight, patternSize);
        }

        finalTex.Apply();

        string outDir = Path.Combine(Application.dataPath, "Artifacts");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "Downsampling_Comparison.png");
        byte[] pngBytes = finalTex.EncodeToPNG();
        File.WriteAllBytes(outPath, pngBytes);
        Debug.Log($"Successfully generated downsampling comparison PNG at: {outPath} ({pngBytes.Length} bytes)");
    }

    #region private methods

    private static void DrawSection(Texture2D tex, PatternDef pat, int sectionBottom, int sectionHeight, int baseSize) {
        int startX = 20;
        int topY = sectionBottom + sectionHeight;

        // Title and Description
        DrawString(tex, startX, topY - 16, pat.title, new Color(1f, 0.88f, 0.40f), 1);
        DrawString(tex, startX + 540, topY - 16, pat.desc, new Color(0.60f, 0.65f, 0.72f), 1);

        // Generate base pattern texture (64x64)
        Color[] baseGrid = GeneratePattern(pat, baseSize);

        // Preview of Base Pattern (render as 80x80 box)
        int previewSize = 84;
        int previewX = startX;
        int previewY = topY - 26 - previewSize;
        DrawTextureBlit(tex, baseGrid, baseSize, baseSize, previewX, previewY, previewSize, previewSize);
        DrawRectOutline(tex, previewX - 1, previewY - 1, previewSize + 2, previewSize + 2, new Color(0.35f, 0.38f, 0.45f));
        DrawString(tex, previewX + 10, previewY - 14, "INPUT 64X64", new Color(0.7f, 0.75f, 0.8f), 1);

        // 4 Downsampling methods
        string[] methodNames = new string[] {
            "[1] SRGB BOX AVERAGE",
            "[2] LINEAR RGB AVERAGE",
            "[3] SPECTRAL (KM PIGMENT)",
            "[4] POLYMIXER HIERARCHICAL"
        };

        Color[] methodTagColors = new Color[] {
            new Color(0.95f, 0.35f, 0.35f),
            new Color(0.25f, 0.75f, 0.95f),
            new Color(1.00f, 0.75f, 0.20f),
            new Color(0.25f, 0.95f, 0.45f)
        };

        int rowStartX = previewX + previewSize + 30;
        int rowHeight = 26;
        int rowGap = 3;

        for (int m = 0; m < 4; m++) {
            int rowY = topY - 26 - (m + 1) * (rowHeight + rowGap);

            // Method tag & name
            DrawLegendItem(tex, rowStartX, rowY + 6, methodNames[m], methodTagColors[m]);

            // Compute downsampled steps: 16x16, 4x4, 1x1
            Color[] grid16 = DownsampleGrid(baseGrid, baseSize, 16, m);
            Color[] grid4 = DownsampleGrid(grid16, 16, 4, m);
            Color final1x1 = DownsampleGrid(grid4, 4, 1, m)[0];

            // Render 16x16 preview (scaled to 22x22)
            int x16 = rowStartX + 230;
            DrawTextureBlit(tex, grid16, 16, 16, x16, rowY + 2, 22, 22);
            DrawRectOutline(tex, x16 - 1, rowY + 1, 24, 24, new Color(0.3f, 0.3f, 0.35f));

            // Arrow
            DrawString(tex, x16 + 28, rowY + 8, ">", new Color(0.5f, 0.55f, 0.6f), 1);

            // Render 4x4 preview (scaled to 22x22)
            int x4 = x16 + 42;
            DrawTextureBlit(tex, grid4, 4, 4, x4, rowY + 2, 22, 22);
            DrawRectOutline(tex, x4 - 1, rowY + 1, 24, 24, new Color(0.3f, 0.3f, 0.35f));

            // Arrow
            DrawString(tex, x4 + 28, rowY + 8, ">", new Color(0.5f, 0.55f, 0.6f), 1);

            // Render final 1x1 patch (large swatch: 48x22)
            int x1 = x4 + 42;
            for (int dy = 0; dy < 22; dy++) {
                for (int dx = 0; dx < 48; dx++) {
                    tex.SetPixel(x1 + dx, rowY + 2 + dy, final1x1);
                }
            }
            DrawRectOutline(tex, x1 - 1, rowY + 1, 50, 24, Color.white);

            // Color stats: RGB and HSV
            int r255 = Mathf.RoundToInt(final1x1.r * 255f);
            int g255 = Mathf.RoundToInt(final1x1.g * 255f);
            int b255 = Mathf.RoundToInt(final1x1.b * 255f);
            Color.RGBToHSV(final1x1, out float h, out float s, out float v);
            int hDeg = Mathf.RoundToInt(h * 360f);
            int sPct = Mathf.RoundToInt(s * 100f);
            int vPct = Mathf.RoundToInt(v * 100f);

            string rgbStr = $"RGB({r255,3},{g255,3},{b255,3})";
            string hsvStr = $"HSV({hDeg,3}D,{sPct,2}%,{vPct,2}%)";

            DrawString(tex, x1 + 60, rowY + 8, rgbStr, new Color(0.9f, 0.92f, 0.95f), 1);
            DrawString(tex, x1 + 225, rowY + 8, hsvStr, new Color(0.72f, 0.78f, 0.85f), 1);
        }
    }

    private static Color[] GeneratePattern(PatternDef pat, int size) {
        Color[] grid = new Color[size * size];
        if (pat.patternType == 0) {
            // 2-color Checkerboard (2x2 pixel blocks)
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int check = ((x / 2) + (y / 2)) % 2;
                    grid[y * size + x] = pat.palette[check];
                }
            }
        } else {
            // 3-color repetitive stipple
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int cIdx = (x + y * 2) % 3;
                    grid[y * size + x] = pat.palette[cIdx];
                }
            }
        }
        return grid;
    }

    private static Color[] DownsampleGrid(Color[] src, int inSize, int outSize, int method) {
        if (inSize == outSize) return src;
        int ratio = inSize / outSize;
        Color[] dst = new Color[outSize * outSize];

        for (int oy = 0; oy < outSize; oy++) {
            for (int ox = 0; ox < outSize; ox++) {
                int blockPixels = ratio * ratio;

                if (method == 0) {
                    // [1] sRGB Box Average
                    float r = 0f, g = 0f, b = 0f;
                    for (int dy = 0; dy < ratio; dy++) {
                        for (int dx = 0; dx < ratio; dx++) {
                            Color c = src[(oy * ratio + dy) * inSize + (ox * ratio + dx)];
                            r += c.r;
                            g += c.g;
                            b += c.b;
                        }
                    }
                    dst[oy * outSize + ox] = new Color(r / blockPixels, g / blockPixels, b / blockPixels, 1f);
                } else if (method == 1) {
                    // [2] Linear RGB Box Average
                    float lr = 0f, lg = 0f, lb = 0f;
                    for (int dy = 0; dy < ratio; dy++) {
                        for (int dx = 0; dx < ratio; dx++) {
                            Color c = src[(oy * ratio + dy) * inSize + (ox * ratio + dx)];
                            lr += Mathf.GammaToLinearSpace(c.r);
                            lg += Mathf.GammaToLinearSpace(c.g);
                            lb += Mathf.GammaToLinearSpace(c.b);
                        }
                    }
                    dst[oy * outSize + ox] = new Color(
                        Mathf.LinearToGammaSpace(lr / blockPixels),
                        Mathf.LinearToGammaSpace(lg / blockPixels),
                        Mathf.LinearToGammaSpace(lb / blockPixels),
                        1f
                    );
                } else if (method == 2) {
                    // [3] Spectral.js Kubelka-Munk Pigment Mix
                    Color[] blockColors = new Color[blockPixels];
                    int bIdx = 0;
                    for (int dy = 0; dy < ratio; dy++) {
                        for (int dx = 0; dx < ratio; dx++) {
                            blockColors[bIdx++] = src[(oy * ratio + dy) * inSize + (ox * ratio + dx)];
                        }
                    }
                    dst[oy * outSize + ox] = SpectralMixer.Mix(blockColors);
                } else {
                    // [4] Hierarchical 2x2 PolyMixer Reduction
                    // If ratio == 2, do single 2x2 tournament
                    if (ratio == 2) {
                        Color p00 = src[(oy * 2 + 0) * inSize + (ox * 2 + 0)];
                        Color p10 = src[(oy * 2 + 0) * inSize + (ox * 2 + 1)];
                        Color p01 = src[(oy * 2 + 1) * inSize + (ox * 2 + 0)];
                        Color p11 = src[(oy * 2 + 1) * inSize + (ox * 2 + 1)];
                        Color top = PolyMixer.Lerp(p00, p10, 0.5f);
                        Color bot = PolyMixer.Lerp(p01, p11, 0.5f);
                        dst[oy * outSize + ox] = PolyMixer.Lerp(top, bot, 0.5f);
                    } else {
                        // Multi-step reduction (e.g. 4x4 or 16x16)
                        Color[] sub = new Color[ratio * ratio];
                        for (int dy = 0; dy < ratio; dy++) {
                            for (int dx = 0; dx < ratio; dx++) {
                                sub[dy * ratio + dx] = src[(oy * ratio + dy) * inSize + (ox * ratio + dx)];
                            }
                        }
                        int currSize = ratio;
                        while (currSize > 1) {
                            int nextSize = currSize / 2;
                            Color[] next = new Color[nextSize * nextSize];
                            for (int ny = 0; ny < nextSize; ny++) {
                                for (int nx = 0; nx < nextSize; nx++) {
                                    Color p00 = sub[(ny * 2 + 0) * currSize + (nx * 2 + 0)];
                                    Color p10 = sub[(ny * 2 + 0) * currSize + (nx * 2 + 1)];
                                    Color p01 = sub[(ny * 2 + 1) * currSize + (nx * 2 + 0)];
                                    Color p11 = sub[(ny * 2 + 1) * currSize + (nx * 2 + 1)];
                                    Color top = PolyMixer.Lerp(p00, p10, 0.5f);
                                    Color bot = PolyMixer.Lerp(p01, p11, 0.5f);
                                    next[ny * nextSize + nx] = PolyMixer.Lerp(top, bot, 0.5f);
                                }
                            }
                            sub = next;
                            currSize = nextSize;
                        }
                        dst[oy * outSize + ox] = sub[0];
                    }
                }
            }
        }
        return dst;
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
        DrawString(tex, 20, h - 28, "DOWNSAMPLING & PIXEL MIXING COMPARISON EXPERIMENT", Color.white, 2);
        DrawString(tex, 20, h - 48, "REDUCTION BENCHMARK: 64X64 -> 16X16 -> 4X4 -> 1X1 (OPTICAL ADDITIVE vs PHYSICAL PIGMENT)", new Color(0.65f, 0.70f, 0.78f), 1);

        int legendY = h - 75;
        DrawLegendItem(tex, 20, legendY, "[1] SRGB BOX AVG", new Color(0.95f, 0.35f, 0.35f));
        DrawLegendItem(tex, 220, legendY, "[2] LINEAR RGB AVG", new Color(0.25f, 0.75f, 0.95f));
        DrawLegendItem(tex, 460, legendY, "[3] SPECTRAL (KM PIGMENT)", new Color(1.00f, 0.75f, 0.20f));
        DrawLegendItem(tex, 760, legendY, "[4] POLYMIXER HIERARCHICAL", new Color(0.25f, 0.95f, 0.45f));
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
            case '>': return new byte[] { 0x10, 0x08, 0x04, 0x02, 0x04, 0x08, 0x10 };
            case '=': return new byte[] { 0x00, 0x1F, 0x00, 0x00, 0x1F, 0x00, 0x00 };
            default:  return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }
    }

    #endregion
}
