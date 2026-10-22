using System;
using System.IO;
using UnityEngine;
using UnityEditor;

public static class ColorMixCompareRunner {
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

    [MenuItem("FilamentMixer/Validate ColorMixComparison Scene")]
    public static void ValidateSampleScene() {
        Debug.Log("Validating ColorMixComparison scene...");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Samples/Comparison/Scenes/ColorMixComparison.unity");
        if (!scene.IsValid()) throw new Exception("ColorMixComparison scene could not be opened!");

        var demo = UnityEngine.Object.FindFirstObjectByType<Nobnak.PigmentMixer.Samples.ColorMixSampleDemo>();
        if (demo == null) throw new Exception("ColorMixSampleDemo component was not found in ColorMixComparison scene!");

        string outDir = Path.Combine(Application.dataPath, "Artifacts");
        Directory.CreateDirectory(outDir);

        // 1. Interactive Blend
        demo.Mode = Nobnak.PigmentMixer.Samples.ColorMixSampleDemo.DisplayMode.InteractiveBlend;
        demo.Preset = Nobnak.PigmentMixer.Samples.ColorMixSampleDemo.PresetPair.MixboxCanonical;
        demo.BlendRatio = 0.5f;
        demo.ForceRefresh();
        var tex = demo.OutputTexture;
        if (tex == null || tex.width != 960 || tex.height != 540) throw new Exception($"Invalid texture for InteractiveBlend: {tex}");
        File.WriteAllBytes(Path.Combine(outDir, "ColorMix_Interactive_Blend_Demo.png"), tex.EncodeToPNG());
        Debug.Log("Saved ColorMix_Interactive_Blend_Demo.png");

        // 2. All Presets Overview
        demo.Mode = Nobnak.PigmentMixer.Samples.ColorMixSampleDemo.DisplayMode.AllPresetsOverview;
        demo.ForceRefresh();
        tex = demo.OutputTexture;
        if (tex == null || tex.width != 980 || tex.height != 640) throw new Exception($"Invalid texture for AllPresetsOverview: {tex}");
        File.WriteAllBytes(Path.Combine(outDir, "ColorMix_Overview_Demo.png"), tex.EncodeToPNG());
        Debug.Log("Saved ColorMix_Overview_Demo.png");

        // 3. Downsampling Comparison
        demo.Mode = Nobnak.PigmentMixer.Samples.ColorMixSampleDemo.DisplayMode.DownsamplingComparison;
        demo.ForceRefresh();
        tex = demo.OutputTexture;
        if (tex == null || tex.width != 1040 || tex.height != 640) throw new Exception($"Invalid texture for DownsamplingComparison: {tex}");
        File.WriteAllBytes(Path.Combine(outDir, "ColorMix_Downsampling_Demo.png"), tex.EncodeToPNG());
        Debug.Log("Saved ColorMix_Downsampling_Demo.png");

        // Reset to default Interactive mode
        demo.Mode = Nobnak.PigmentMixer.Samples.ColorMixSampleDemo.DisplayMode.InteractiveBlend;
        demo.ForceRefresh();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("ColorMixComparison scene validation passed successfully!");
    }

    [MenuItem("FilamentMixer/Run Comparison Image Generation (Policy 2)")]
    public static void RunComparison() {
        Debug.Log("Starting Color Mixing Comparison Generation (Policy 2: 3-Way Clean Comparison)...");

        int width = 920;
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
            new ColorPair(
                "PAIR 1: BLUE (0, 33, 133) + YELLOW (252, 211, 0)  [CANONICAL MIXBOX TEST]",
                new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f),
                new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f)
            ),
            new ColorPair(
                "PAIR 2: PURE BLUE (0, 0, 255) + PURE YELLOW (255, 255, 0)",
                new Color(0f, 0f, 1f, 1f),
                new Color(1f, 1f, 0f, 1f)
            ),
            new ColorPair(
                "PAIR 3: RED (255, 0, 0) + CYAN (0, 255, 255)",
                new Color(1f, 0f, 0f, 1f),
                new Color(0f, 1f, 1f, 1f)
            ),
            new ColorPair(
                "PAIR 4: GREEN (0, 255, 0) + MAGENTA (255, 0, 255)",
                new Color(0f, 1f, 0f, 1f),
                new Color(1f, 0f, 1f, 1f)
            ),
            new ColorPair(
                "PAIR 5: RED (255, 0, 0) + YELLOW (255, 255, 0)",
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

        // Draw Header and Legend
        DrawHeader(finalTex, width, height, headerHeight);

        // Process each pair
        for (int pIdx = 0; pIdx < pairCount; pIdx++) {
            var pair = pairs[pIdx];
            int blockBottom = height - headerHeight - (pIdx + 1) * totalBlockHeight;
            int blockTop = blockBottom + blockContentHeight;

            // Draw Label bar
            int labelY = blockTop - labelHeight + 4;
            DrawString(finalTex, startX, labelY, pair.name, new Color(1f, 0.88f, 0.40f), 1);

            // Compute strips
            int stripTop = blockTop - labelHeight;

            for (int sIdx = 0; sIdx < 4; sIdx++) {
                int sY = stripTop - (sIdx + 1) * (stripHeight + stripGap);

                for (int sx = 0; sx < stripWidth; sx++) {
                    float t = (float)sx / (stripWidth - 1);
                    Color col;

                    if (sIdx == 0) {
                        // 1. Additive (RGB Lerp)
                        col = Color.Lerp(pair.c1, pair.c2, t);
                    } else if (sIdx == 1) {
                        // 2. Subtractive (CMYK Lerp)
                        col = CmykLerp(pair.c1, pair.c2, t);
                    } else if (sIdx == 2) {
                        // 3. PolyMixer (HLSL Shader Implementation)
                        col = PolyMixer.Lerp(pair.c1, pair.c2, t);
                    } else {
                        // 4. Spectral.js (38-wavelength Kubelka-Munk)
                        col = SpectralMixer.Lerp(pair.c1, pair.c2, t);
                    }

                    // Write strip pixel band
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

        // Save PNG
        byte[] pngBytes = finalTex.EncodeToPNG();
        string outDir = Path.Combine(Application.dataPath, "Artifacts");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, "ColorMixing_Comparison.png");
        File.WriteAllBytes(outPath, pngBytes);
        Debug.Log($"Generated refined comparison PNG (4-Way) at: {outPath} ({pngBytes.Length} bytes)");
    }

    #region private methods

    private static Color CmykLerp(Color c1, Color c2, float t) {
        float k1 = 1f - Mathf.Max(Mathf.Max(c1.r, c1.g), c1.b);
        float invK1 = (k1 < 0.999f) ? (1f - k1) : 0.001f;
        float cmy1_c = (1f - c1.r - k1) / invK1;
        float cmy1_m = (1f - c1.g - k1) / invK1;
        float cmy1_y = (1f - c1.b - k1) / invK1;

        float k2 = 1f - Mathf.Max(Mathf.Max(c2.r, c2.g), c2.b);
        float invK2 = (k2 < 0.999f) ? (1f - k2) : 0.001f;
        float cmy2_c = (1f - c2.r - k2) / invK2;
        float cmy2_m = (1f - c2.g - k2) / invK2;
        float cmy2_y = (1f - c2.b - k2) / invK2;

        float c = Mathf.Lerp(cmy1_c, cmy2_c, t);
        float m = Mathf.Lerp(cmy1_m, cmy2_m, t);
        float y = Mathf.Lerp(cmy1_y, cmy2_y, t);
        float k = Mathf.Lerp(k1, k2, t);

        float r = (1f - Mathf.Clamp01(c)) * (1f - k);
        float g = (1f - Mathf.Clamp01(m)) * (1f - k);
        float b = (1f - Mathf.Clamp01(y)) * (1f - k);
        return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
    }

    private static void DrawHeader(Texture2D tex, int w, int h, int headerH) {
        DrawString(tex, 20, h - 28, "COLOR MIXING COMPARISON: RGB vs CMYK vs POLYMIXER vs SPECTRAL.JS", Color.white, 2);
        DrawString(tex, 20, h - 48, "PIGMENT BENCHMARK: 4TH-ORDER POLYNOMIAL vs 38-WAVELENGTH KUBELKA-MUNK", new Color(0.65f, 0.70f, 0.78f), 1);

        int legendY = h - 75;
        DrawLegendItem(tex, 20, legendY, "[1] RGB LERP", new Color(0.9f, 0.35f, 0.35f));
        DrawLegendItem(tex, 190, legendY, "[2] CMYK LERP", new Color(0.25f, 0.75f, 0.95f));
        DrawLegendItem(tex, 420, legendY, "[3] POLYMIXER (HLSL)", new Color(0.25f, 0.95f, 0.45f));
        DrawLegendItem(tex, 680, legendY, "[4] SPECTRAL.JS (KM)", new Color(1.0f, 0.75f, 0.20f));
    }

    private static void DrawLegendItem(Texture2D tex, int x, int y, string label, Color tagColor) {
        for (int dy = 0; dy < 10; dy++) {
            for (int dx = 0; dx < 10; dx++) {
                tex.SetPixel(x + dx, y + dy, tagColor);
            }
        }
        DrawString(tex, x + 15, y + 2, label, new Color(0.9f, 0.9f, 0.92f), 1);
    }

    // --- Simple 5x7 Dot-Matrix Font Renderer ---
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
            default:  return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        }
    }

    #endregion
}
