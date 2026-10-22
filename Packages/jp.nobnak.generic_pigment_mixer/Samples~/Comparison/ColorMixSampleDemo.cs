using System;
using UnityEngine;

namespace Nobnak.PigmentMixer.Samples {
    /// <summary>
    /// Interactive pigment mixing comparison demo.
    /// Demonstrates 2-color gradient mixing, preset overview, and checkerboard downsampling.
    /// </summary>
    [ExecuteAlways]
    public class ColorMixSampleDemo : MonoBehaviour {
        public enum DisplayMode {
            InteractiveBlend = 0,
            AllPresetsOverview = 1,
            DownsamplingComparison = 2
        }

        public enum PresetPair {
            MixboxCanonical = 0,
            PureBlueYellow = 1,
            RedCyan = 2,
            GreenMagenta = 3,
            RedYellow = 4,
            Custom = 5
        }

        #region serialized fields

        [Header("Display Settings")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.InteractiveBlend;
        [SerializeField] private PresetPair preset = PresetPair.MixboxCanonical;
        [SerializeField] private Renderer targetRenderer;

        [Header("Color Inputs (sRGB)")]
        [SerializeField] private Color colorA = new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f);
        [SerializeField] private Color colorB = new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f);

        [Header("Blend Controls")]
        [Range(0f, 1f)] [SerializeField] private float blendRatio = 0.5f;
        [SerializeField] private bool autoAnimate = false;
        [SerializeField] private float animationSpeed = 0.35f;

        [Header("Mixed Outputs (Read-only Preview at t)")]
        [SerializeField] private Color spectral38Mixed;
        [SerializeField] private Color spectral16Mixed;
        [SerializeField] private Color polyMixed;
        [SerializeField] private Color cmykMixed;
        [SerializeField] private Color additiveMixed;

        [Header("Accuracy (Delta-E vs 38D Reference)")]
        [SerializeField] private float deltaE16;
        [SerializeField] private float deltaEPoly;

        #endregion

        #region private fields

        private Texture2D generatedTexture;
        private Material displayMaterial;
        private bool isDirty = true;
        private DisplayMode lastMode;
        private PresetPair lastPreset;
        private Color lastColorA;
        private Color lastColorB;
        private float lastBlendRatio = -1f;

        #endregion

        #region properties

        public DisplayMode Mode {
            get => displayMode;
            set { if (displayMode != value) { displayMode = value; isDirty = true; } }
        }

        public PresetPair Preset {
            get => preset;
            set {
                if (preset != value) {
                    preset = value;
                    ApplyPresetColors(preset);
                    isDirty = true;
                }
            }
        }

        public Color ColorA {
            get => colorA;
            set { if (colorA != value) { colorA = value; preset = PresetPair.Custom; isDirty = true; } }
        }

        public Color ColorB {
            get => colorB;
            set { if (colorB != value) { colorB = value; preset = PresetPair.Custom; isDirty = true; } }
        }

        public float BlendRatio {
            get => blendRatio;
            set {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Abs(blendRatio - clamped) > 0.0005f) { blendRatio = clamped; isDirty = true; }
            }
        }

        public bool AutoAnimate {
            get => autoAnimate;
            set => autoAnimate = value;
        }

        public Texture2D OutputTexture => generatedTexture;

        #endregion

        #region Unity lifecycle

        private void OnEnable() {
            EnsureTargetComponents();
            ApplyPresetColors(preset);
            isDirty = true;
            UpdateOutputs();
            RenderDisplayTexture();
        }

        private void OnDisable() {
            if (generatedTexture != null) {
                if (Application.isPlaying) Destroy(generatedTexture);
                else DestroyImmediate(generatedTexture);
                generatedTexture = null;
            }
        }

        private void OnValidate() {
            if (preset != lastPreset && preset != PresetPair.Custom) ApplyPresetColors(preset);
            isDirty = true;
        }

        private void Update() {
            if (autoAnimate && Application.isPlaying) {
                blendRatio = Mathf.PingPong(Time.time * animationSpeed, 1f);
                isDirty = true;
            }

            if (HasParametersChanged()) {
                isDirty = true;
            }

            if (isDirty) {
                UpdateOutputs();
                RenderDisplayTexture();
                isDirty = false;
            }
        }

        private void OnGUI() {
            if (!Application.isPlaying) return;
            DrawRuntimeGUI();
        }

        #endregion

        #region public interface

        public void SetPreset(PresetPair p) {
            preset = p;
            ApplyPresetColors(p);
            isDirty = true;
        }

        public void ForceRefresh() {
            isDirty = true;
        }

        #endregion

        #region private methods - core rendering

        private bool HasParametersChanged() {
            if (displayMode != lastMode || preset != lastPreset) return true;
            if (colorA != lastColorA || colorB != lastColorB) return true;
            if (Mathf.Abs(blendRatio - lastBlendRatio) > 0.001f) return true;
            return false;
        }

        private void EnsureTargetComponents() {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == null) {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                meshFilter.sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(quad);
            }

            if (displayMaterial == null) {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
                if (shader != null) displayMaterial = new Material(shader) { name = "ColorMixDisplay (Generated)" };
            }

            if (targetRenderer != null && displayMaterial != null) {
                targetRenderer.sharedMaterial = displayMaterial;
            }
        }

        private void ApplyPresetColors(PresetPair p) {
            switch (p) {
                case PresetPair.MixboxCanonical:
                    colorA = new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f);
                    colorB = new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f);
                    break;
                case PresetPair.PureBlueYellow:
                    colorA = new Color(0f, 0f, 1f, 1f);
                    colorB = new Color(1f, 1f, 0f, 1f);
                    break;
                case PresetPair.RedCyan:
                    colorA = new Color(1f, 0f, 0f, 1f);
                    colorB = new Color(0f, 1f, 1f, 1f);
                    break;
                case PresetPair.GreenMagenta:
                    colorA = new Color(0f, 1f, 0f, 1f);
                    colorB = new Color(1f, 0f, 1f, 1f);
                    break;
                case PresetPair.RedYellow:
                    colorA = new Color(1f, 0f, 0f, 1f);
                    colorB = new Color(1f, 1f, 0f, 1f);
                    break;
                case PresetPair.Custom:
                    break;
            }
        }

        private void UpdateOutputs() {
            spectral38Mixed = SpectralMixerN.D38.Lerp(colorA, colorB, blendRatio);
            spectral16Mixed = SpectralMixer16.Lerp(colorA, colorB, blendRatio);
            polyMixed = PolyMixer.Lerp(colorA, colorB, blendRatio);
            cmykMixed = CmykLerp(colorA, colorB, blendRatio);
            additiveMixed = Color.Lerp(colorA.linear, colorB.linear, blendRatio).gamma;

            deltaE16 = DeltaE(spectral38Mixed, spectral16Mixed);
            deltaEPoly = DeltaE(spectral38Mixed, polyMixed);

            lastMode = displayMode;
            lastPreset = preset;
            lastColorA = colorA;
            lastColorB = colorB;
            lastBlendRatio = blendRatio;
        }

        private void RenderDisplayTexture() {
            int width, height;
            switch (displayMode) {
                case DisplayMode.AllPresetsOverview:
                    width = 980; height = 640;
                    EnsureTexture(width, height);
                    DrawOverviewTexture(generatedTexture, width, height);
                    break;
                case DisplayMode.DownsamplingComparison:
                    width = 1040; height = 640;
                    EnsureTexture(width, height);
                    DrawDownsamplingTexture(generatedTexture, width, height);
                    break;
                default:
                    width = 960; height = 540;
                    EnsureTexture(width, height);
                    DrawInteractiveTexture(generatedTexture, width, height);
                    break;
            }

            generatedTexture.Apply();

            if (displayMaterial != null) {
                if (displayMaterial.HasProperty(PropBaseMap)) displayMaterial.SetTexture(PropBaseMap, generatedTexture);
                if (displayMaterial.HasProperty(PropMainTex)) displayMaterial.SetTexture(PropMainTex, generatedTexture);
            }
            if (targetRenderer != null && targetRenderer.sharedMaterial != displayMaterial && displayMaterial != null) {
                targetRenderer.sharedMaterial = displayMaterial;
            }
        }

        private void EnsureTexture(int w, int h) {
            if (generatedTexture == null || generatedTexture.width != w || generatedTexture.height != h) {
                if (generatedTexture != null) {
                    if (Application.isPlaying) Destroy(generatedTexture);
                    else DestroyImmediate(generatedTexture);
                }
                generatedTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) {
                    name = "ColorMix_DisplayTexture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
        }

        #endregion

        #region private methods - texture layouts

        private void DrawInteractiveTexture(Texture2D tex, int width, int height) {
            ClearTexture(tex, BgColor);

            // 1. Header (y: 470 to 525)
            string title = $"GENERIC PIGMENT MIXER : 2-COLOR INTERACTIVE BLEND (t = {blendRatio:P0})";
            DrawString(tex, 24, height - 30, title, TextHeaderColor, 2);

            string pairDesc = (preset == PresetPair.Custom) ? "CUSTOM COLOR PAIR" : preset.ToString().ToUpperInvariant();
            DrawString(tex, 24, height - 52, $"COLOR PAIR: {pairDesc}", TextSubColor, 1);

            // Swatches for Input A & B
            DrawSwatchWithLabel(tex, 680, height - 58, 28, 20, colorA, $"COL A: #{ColorUtility.ToHtmlStringRGB(colorA)}");
            DrawSwatchWithLabel(tex, 820, height - 58, 28, 20, colorB, $"COL B: #{ColorUtility.ToHtmlStringRGB(colorB)}");

            // 2. Continuous Gradient Strips (y: 190 to 450)
            int stripStartX = 24;
            int stripWidth = width - 48;
            int stripHeight = 36;
            int stripGap = 16;
            int stripsTop = height - 76;

            var methods = new (string name, Color tagColor, Func<float, Color> sampler)[] {
                ("[1] SPECTRAL.JS 38D (GROUND TRUTH KM)", MethodGold, t => SpectralMixerN.D38.Lerp(colorA, colorB, t)),
                ("[2] SPECTRALMIXER 16 (16-BAND SIMD KM)", MethodCyan, t => SpectralMixer16.Lerp(colorA, colorB, t)),
                ("[3] POLYMIXER (4TH-DEGREE POLYNOMIAL)", MethodGreen, t => PolyMixer.Lerp(colorA, colorB, t)),
                ("[4] SUBTRACTIVE (CMYK LERP)", MethodViolet, t => CmykLerp(colorA, colorB, t)),
                ("[5] ADDITIVE (LINEAR RGB LERP)", MethodCoral, t => Color.Lerp(colorA.linear, colorB.linear, t).gamma)
            };

            for (int i = 0; i < methods.Length; i++) {
                int sY = stripsTop - (i + 1) * (stripHeight + stripGap);
                DrawLegendItem(tex, stripStartX, sY + stripHeight + 4, methods[i].name, methods[i].tagColor);

                for (int sx = 0; sx < stripWidth; sx++) {
                    float t = (float)sx / (stripWidth - 1);
                    Color col = methods[i].sampler(t);
                    for (int dy = 0; dy < stripHeight; dy++) {
                        Color pCol = (dy == 0 || dy == stripHeight - 1 || sx == 0 || sx == stripWidth - 1) ? col * 0.7f : col;
                        pCol.a = 1f;
                        tex.SetPixel(stripStartX + sx, sY + dy, pCol);
                    }
                }

                // Blend marker hairline at current t
                int markerX = stripStartX + Mathf.RoundToInt(blendRatio * (stripWidth - 1));
                for (int dy = -2; dy < stripHeight + 2; dy++) {
                    tex.SetPixel(markerX - 1, sY + dy, Color.black);
                    tex.SetPixel(markerX, sY + dy, Color.white);
                    tex.SetPixel(markerX + 1, sY + dy, Color.black);
                }
            }

            // 3. Current Mixed Swatches at ratio t (y: 20 to 125)
            int bottomY = 24;
            int cardW = (width - 48 - 16 * 4) / 5;
            var swatches = new (string label, Color col, string deltaEStr, Color tag)[] {
                ("38D REF", spectral38Mixed, "GT (0.00)", MethodGold),
                ("16D SIMD", spectral16Mixed, $"dE: {deltaE16:F2}", MethodCyan),
                ("POLYMIXER", polyMixed, $"dE: {deltaEPoly:F2}", MethodGreen),
                ("CMYK", cmykMixed, $"dE: {DeltaE(spectral38Mixed, cmykMixed):F1}", MethodViolet),
                ("ADDITIVE", additiveMixed, $"dE: {DeltaE(spectral38Mixed, additiveMixed):F1}", MethodCoral)
            };

            for (int i = 0; i < swatches.Length; i++) {
                int cardX = stripStartX + i * (cardW + 16);
                DrawMixedCard(tex, cardX, bottomY, cardW, 88, swatches[i].label, swatches[i].col, swatches[i].deltaEStr, swatches[i].tag);
            }
        }

        private void DrawOverviewTexture(Texture2D tex, int width, int height) {
            ClearTexture(tex, BgColor);

            DrawString(tex, 20, height - 28, "GENERIC PIGMENT MIXER : 5-PRESET OVERVIEW BENCHMARK", TextHeaderColor, 2);
            DrawString(tex, 20, height - 48, "COMPARING 16-BAND SIMD KM vs 38D REFERENCE vs POLYMIXER vs LINEAR RGB LERP", TextSubColor, 1);

            int legendY = height - 74;
            DrawLegendItem(tex, 20, legendY, "[1] SPECTRAL.JS 38D (REF)", MethodGold);
            DrawLegendItem(tex, 270, legendY, "[2] SPECTRALMIXER 16", MethodCyan);
            DrawLegendItem(tex, 520, legendY, "[3] POLYMIXER", MethodGreen);
            DrawLegendItem(tex, 730, legendY, "[4] LINEAR RGB LERP", MethodCoral);

            var pairs = GetCanonicalPairs();
            int pairCount = pairs.Length;
            int headerH = 86;
            int totalBlockH = (height - headerH - 16) / pairCount;
            int stripW = width - 40;
            int startX = 20;

            for (int pIdx = 0; pIdx < pairCount; pIdx++) {
                var p = pairs[pIdx];
                int blockBottom = height - headerH - (pIdx + 1) * totalBlockH;
                int blockTop = blockBottom + totalBlockH - 8;

                Color mid38 = SpectralMixerN.D38.Lerp(p.c1, p.c2, 0.5f);
                Color mid16 = SpectralMixer16.Lerp(p.c1, p.c2, 0.5f);
                float dE = DeltaE(mid38, mid16);

                DrawString(tex, startX, blockTop - 14, p.name, TextAccentColor, 1);
                string dEStr = $"DELTA-E (16D vs 38D at t=0.5): {dE:F2}";
                DrawString(tex, startX + 600, blockTop - 14, dEStr, (dE < 3.0f) ? MethodGreen : MethodCyan, 1);

                int stripH = 18;
                int stripGap = 2;
                int stripsY = blockTop - 18;

                for (int sIdx = 0; sIdx < 4; sIdx++) {
                    int sY = stripsY - (sIdx + 1) * (stripH + stripGap);
                    for (int sx = 0; sx < stripW; sx++) {
                        float t = (float)sx / (stripW - 1);
                        Color c;
                        if (sIdx == 0) c = SpectralMixerN.D38.Lerp(p.c1, p.c2, t);
                        else if (sIdx == 1) c = SpectralMixer16.Lerp(p.c1, p.c2, t);
                        else if (sIdx == 2) c = PolyMixer.Lerp(p.c1, p.c2, t);
                        else c = Color.Lerp(p.c1.linear, p.c2.linear, t).gamma;

                        for (int dy = 0; dy < stripH; dy++) {
                            Color pCol = (dy == 0 || dy == stripH - 1 || sx == 0 || sx == stripW - 1) ? c * 0.7f : c;
                            pCol.a = 1f;
                            tex.SetPixel(startX + sx, sY + dy, pCol);
                        }
                    }
                }
            }
        }

        private void DrawDownsamplingTexture(Texture2D tex, int width, int height) {
            ClearTexture(tex, BgColor);

            DrawString(tex, 20, height - 28, "GENERIC PIGMENT MIXER : 64X64 CHECKERBOARD DOWNSAMPLING BENCHMARK", TextHeaderColor, 2);
            DrawString(tex, 20, height - 48, "PHYSICAL PIGMENT REDUCTION (16D & 38D) vs POLYMIXER vs OPTICAL BOX/LINEAR AVG", TextSubColor, 1);

            int legendY = height - 74;
            DrawLegendItem(tex, 20, legendY, "[1] SRGB BOX AVG", MethodCoral);
            DrawLegendItem(tex, 200, legendY, "[2] LINEAR RGB AVG", MethodOrange);
            DrawLegendItem(tex, 410, legendY, "[3] POLYMIXER", MethodGreen);
            DrawLegendItem(tex, 620, legendY, "[4] SPECTRALMIXER 16", MethodCyan);
            DrawLegendItem(tex, 860, legendY, "[5] 38D GROUND TRUTH", MethodGold);

            var pairs = GetCanonicalPairs();
            int pairCount = pairs.Length;
            int headerH = 86;
            int sectionH = (height - headerH - 16) / pairCount;
            int startX = 20;
            int patternSize = 64;

            for (int pIdx = 0; pIdx < pairCount; pIdx++) {
                var p = pairs[pIdx];
                int blockBottom = height - headerH - (pIdx + 1) * sectionH;
                int blockTop = blockBottom + sectionH - 6;

                DrawString(tex, startX, blockTop - 12, $"{p.name} [50:50 CHECKERBOARD]", TextAccentColor, 1);

                // Checkerboard preview (72x72)
                int prevSize = 72;
                int prevX = startX;
                int prevY = blockTop - 18 - prevSize;

                Color[] baseGrid = new Color[patternSize * patternSize];
                for (int y = 0; y < patternSize; y++) {
                    for (int x = 0; x < patternSize; x++) {
                        int check = ((x / 2) + (y / 2)) % 2;
                        baseGrid[y * patternSize + x] = (check == 0) ? p.c1 : p.c2;
                    }
                }

                DrawTextureBlit(tex, baseGrid, patternSize, patternSize, prevX, prevY, prevSize, prevSize);
                DrawRectOutline(tex, prevX - 1, prevY - 1, prevSize + 2, prevSize + 2, BorderColor);

                Color gtColor = SpectralMixerN.D38.Mix(new Color[] { p.c1, p.c2 });

                string[] mNames = { "[1] SRGB BOX AVG", "[2] LINEAR RGB AVG", "[3] POLYMIXER", "[4] SPECTRALMIXER 16", "[5] 38D GT" };
                Color[] mTags = { MethodCoral, MethodOrange, MethodGreen, MethodCyan, MethodGold };

                int rowStartX = prevX + prevSize + 20;
                int rowH = 14;
                int rowGap = 1;

                for (int m = 0; m < 5; m++) {
                    int rY = blockTop - 18 - (m + 1) * (rowH + rowGap);
                    Color mixed;
                    if (m == 0) mixed = Color.Lerp(p.c1, p.c2, 0.5f);
                    else if (m == 1) mixed = Color.Lerp(p.c1.linear, p.c2.linear, 0.5f).gamma;
                    else if (m == 2) mixed = PolyMixer.Lerp(p.c1, p.c2, 0.5f);
                    else if (m == 3) mixed = SpectralMixer16.Mix(new Color[] { p.c1, p.c2 });
                    else mixed = gtColor;

                    float dE = DeltaE(gtColor, mixed);
                    DrawLegendItem(tex, rowStartX, rY + 2, mNames[m], mTags[m]);

                    // Swatch
                    int swX = rowStartX + 260;
                    DrawRectFill(tex, swX, rY + 1, 48, rowH, mixed);
                    DrawRectOutline(tex, swX - 1, rY, 50, rowH + 2, BorderColor);

                    string evalStr = (m == 4) ? "DELTA-E: 0.00 (REF)" : $"DELTA-E: {dE:F2}";
                    Color evalCol = (dE < 2.5f) ? MethodGreen : (dE < 8f) ? MethodCyan : MethodCoral;
                    DrawString(tex, swX + 64, rY + 3, evalStr, evalCol, 1);
                }
            }
        }

        #endregion

        #region private methods - drawing helpers

        private static void ClearTexture(Texture2D tex, Color col) {
            Color[] pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            tex.SetPixels(pixels);
        }

        private static void DrawRectFill(Texture2D tex, int x, int y, int w, int h, Color col) {
            for (int dy = 0; dy < h; dy++) {
                int py = y + dy;
                if (py < 0 || py >= tex.height) continue;
                for (int dx = 0; dx < w; dx++) {
                    int px = x + dx;
                    if (px >= 0 && px < tex.width) tex.SetPixel(px, py, col);
                }
            }
        }

        private static void DrawRectOutline(Texture2D tex, int x, int y, int w, int h, Color col) {
            for (int i = 0; i < w; i++) {
                int px = x + i;
                if (px < 0 || px >= tex.width) continue;
                if (y >= 0 && y < tex.height) tex.SetPixel(px, y, col);
                if (y + h - 1 >= 0 && y + h - 1 < tex.height) tex.SetPixel(px, y + h - 1, col);
            }
            for (int j = 0; j < h; j++) {
                int py = y + j;
                if (py < 0 || py >= tex.height) continue;
                if (x >= 0 && x < tex.width) tex.SetPixel(x, py, col);
                if (x + w - 1 >= 0 && x + w - 1 < tex.width) tex.SetPixel(x + w - 1, py, col);
            }
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

        private static void DrawLegendItem(Texture2D tex, int x, int y, string label, Color tagColor) {
            DrawRectFill(tex, x, y, 10, 10, tagColor);
            DrawString(tex, x + 15, y + 2, label, TextLightColor, 1);
        }

        private static void DrawSwatchWithLabel(Texture2D tex, int x, int y, int swW, int swH, Color col, string label) {
            DrawRectFill(tex, x, y, swW, swH, col);
            DrawRectOutline(tex, x - 1, y - 1, swW + 2, swH + 2, BorderColor);
            DrawString(tex, x + swW + 8, y + 6, label, TextLightColor, 1);
        }

        private static void DrawMixedCard(Texture2D tex, int x, int y, int w, int h, string label, Color col, string deltaEStr, Color tag) {
            DrawRectFill(tex, x, y, w, h, CardBgColor);
            DrawRectOutline(tex, x, y, w, h, BorderColor);

            // Method tag top bar
            DrawRectFill(tex, x, y + h - 4, w, 4, tag);

            DrawString(tex, x + 8, y + h - 18, label, TextLightColor, 1);

            // Swatch
            int swH = 34;
            int swW = w - 16;
            int swY = y + 24;
            DrawRectFill(tex, x + 8, swY, swW, swH, col);
            DrawRectOutline(tex, x + 7, swY - 1, swW + 2, swH + 2, BorderColor);

            // Hex & Delta-E
            string hexStr = $"#{ColorUtility.ToHtmlStringRGB(col)}";
            DrawString(tex, x + 8, y + 8, hexStr, TextMutedColor, 1);
            DrawString(tex, x + w - 74, y + 8, deltaEStr, tag, 1);
        }

        #endregion

        #region private methods - runtime OnGUI

        private void DrawRuntimeGUI() {
            int panelW = 420;
            int panelH = (preset == PresetPair.Custom) ? 360 : 280;
            Rect panelRect = new Rect(16, 16, panelW, panelH);

            GUI.Box(panelRect, GUIContent.none);
            GUILayout.BeginArea(new Rect(24, 24, panelW - 16, panelH - 16));

            GUILayout.Label("<b>GENERIC PIGMENT MIXER CONTROLS</b>", GetHeaderStyle());
            GUILayout.Space(6);

            // Mode selection
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:", GUILayout.Width(45));
            if (GUILayout.Toggle(displayMode == DisplayMode.InteractiveBlend, "Interactive", "Button")) Mode = DisplayMode.InteractiveBlend;
            if (GUILayout.Toggle(displayMode == DisplayMode.AllPresetsOverview, "5 Presets", "Button")) Mode = DisplayMode.AllPresetsOverview;
            if (GUILayout.Toggle(displayMode == DisplayMode.DownsamplingComparison, "Downsample", "Button")) Mode = DisplayMode.DownsamplingComparison;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // Preset selection
            GUILayout.BeginHorizontal();
            GUILayout.Label("Pair:", GUILayout.Width(45));
            if (GUILayout.Toggle(preset == PresetPair.MixboxCanonical, "Mixbox", "Button")) SetPreset(PresetPair.MixboxCanonical);
            if (GUILayout.Toggle(preset == PresetPair.PureBlueYellow, "Pure B+Y", "Button")) SetPreset(PresetPair.PureBlueYellow);
            if (GUILayout.Toggle(preset == PresetPair.RedCyan, "R+C", "Button")) SetPreset(PresetPair.RedCyan);
            if (GUILayout.Toggle(preset == PresetPair.GreenMagenta, "G+M", "Button")) SetPreset(PresetPair.GreenMagenta);
            if (GUILayout.Toggle(preset == PresetPair.RedYellow, "R+Y", "Button")) SetPreset(PresetPair.RedYellow);
            if (GUILayout.Toggle(preset == PresetPair.Custom, "Custom", "Button")) preset = PresetPair.Custom;
            GUILayout.EndHorizontal();

            if (preset == PresetPair.Custom) {
                GUILayout.Space(4);
                GUILayout.Label($"Custom Color A: (R:{colorA.r:F2}, G:{colorA.g:F2}, B:{colorA.b:F2})");
                colorA.r = GUILayout.HorizontalSlider(colorA.r, 0f, 1f);
                colorA.g = GUILayout.HorizontalSlider(colorA.g, 0f, 1f);
                colorA.b = GUILayout.HorizontalSlider(colorA.b, 0f, 1f);

                GUILayout.Label($"Custom Color B: (R:{colorB.r:F2}, G:{colorB.g:F2}, B:{colorB.b:F2})");
                colorB.r = GUILayout.HorizontalSlider(colorB.r, 0f, 1f);
                colorB.g = GUILayout.HorizontalSlider(colorB.g, 0f, 1f);
                colorB.b = GUILayout.HorizontalSlider(colorB.b, 0f, 1f);
            }

            GUILayout.Space(6);

            // Blend Slider
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Blend Ratio (t = {blendRatio:F2}):", GUILayout.Width(170));
            float newT = GUILayout.HorizontalSlider(blendRatio, 0f, 1f);
            if (Mathf.Abs(newT - blendRatio) > 0.001f) BlendRatio = newT;
            GUILayout.EndHorizontal();

            // Quick ratio buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0%")) BlendRatio = 0f;
            if (GUILayout.Button("25%")) BlendRatio = 0.25f;
            if (GUILayout.Button("50%")) BlendRatio = 0.5f;
            if (GUILayout.Button("75%")) BlendRatio = 0.75f;
            if (GUILayout.Button("100%")) BlendRatio = 1f;
            autoAnimate = GUILayout.Toggle(autoAnimate, "Animate", "Button", GUILayout.Width(70));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"<b>Spectral16 dE:</b> {deltaE16:F2}  |  <b>PolyMixer dE:</b> {deltaEPoly:F2} (vs 38D Ref)");

            GUILayout.EndArea();
        }

        private static GUIStyle GetHeaderStyle() {
            var style = new GUIStyle(GUI.skin.label) {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            style.normal.textColor = Color.white;
            return style;
        }

        #endregion

        #region private methods - math & color science

        private static (string name, Color c1, Color c2)[] GetCanonicalPairs() {
            return new (string name, Color c1, Color c2)[] {
                ("PAIR 1: BLUE (0, 33, 133) + YELLOW (252, 211, 0) [MIXBOX CANONICAL]",
                    new Color(0f / 255f, 33f / 255f, 133f / 255f, 1f),
                    new Color(252f / 255f, 211f / 255f, 0f / 255f, 1f)),
                ("PAIR 2: PURE BLUE (0, 0, 255) + PURE YELLOW (255, 255, 0) [EXTREME]",
                    new Color(0f, 0f, 1f, 1f),
                    new Color(1f, 1f, 0f, 1f)),
                ("PAIR 3: RED (255, 0, 0) + CYAN (0, 255, 255) [COMPLEMENTARY 1]",
                    new Color(1f, 0f, 0f, 1f),
                    new Color(0f, 1f, 1f, 1f)),
                ("PAIR 4: GREEN (0, 255, 0) + MAGENTA (255, 0, 255) [COMPLEMENTARY 2]",
                    new Color(0f, 1f, 0f, 1f),
                    new Color(1f, 0f, 1f, 1f)),
                ("PAIR 5: RED (255, 0, 0) + YELLOW (255, 255, 0) [WARM GRADIENT]",
                    new Color(1f, 0f, 0f, 1f),
                    new Color(1f, 1f, 0f, 1f))
            };
        }

        private static Color CmykLerp(Color c1, Color c2, float t) {
            float k1 = 1f - Mathf.Max(Mathf.Max(c1.r, c1.g), c1.b);
            float invK1 = (k1 < 0.999f) ? (1f - k1) : 0.001f;
            float c1_c = (1f - c1.r - k1) / invK1;
            float c1_m = (1f - c1.g - k1) / invK1;
            float c1_y = (1f - c1.b - k1) / invK1;

            float k2 = 1f - Mathf.Max(Mathf.Max(c2.r, c2.g), c2.b);
            float invK2 = (k2 < 0.999f) ? (1f - k2) : 0.001f;
            float c2_c = (1f - c2.r - k2) / invK2;
            float c2_m = (1f - c2.g - k2) / invK2;
            float c2_y = (1f - c2.b - k2) / invK2;

            float cMix = Mathf.Lerp(c1_c, c2_c, t);
            float mMix = Mathf.Lerp(c1_m, c2_m, t);
            float yMix = Mathf.Lerp(c1_y, c2_y, t);
            float kMix = Mathf.Lerp(k1, k2, t);

            float r = (1f - Mathf.Clamp01(cMix)) * (1f - kMix);
            float g = (1f - Mathf.Clamp01(mMix)) * (1f - kMix);
            float b = (1f - Mathf.Clamp01(yMix)) * (1f - kMix);
            return new Color(r, g, b, 1f);
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

        #endregion

        #region private methods - 5x7 bitmap font

        private static void DrawString(Texture2D tex, int x, int y, string text, Color color, int scale) {
            int curX = x;
            foreach (char c in text.ToUpperInvariant()) {
                if (c == ' ') { curX += 4 * scale; continue; }
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
                case '#': return new byte[] { 0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A };
                default:  return new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            }
        }

        #endregion

        #region constants / static

        private static readonly Color BgColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color CardBgColor = new Color(0.12f, 0.14f, 0.17f, 1f);
        private static readonly Color BorderColor = new Color(0.24f, 0.27f, 0.32f, 1f);
        private static readonly Color TextHeaderColor = Color.white;
        private static readonly Color TextSubColor = new Color(0.65f, 0.70f, 0.78f, 1f);
        private static readonly Color TextAccentColor = new Color(1f, 0.88f, 0.40f, 1f);
        private static readonly Color TextLightColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        private static readonly Color TextMutedColor = new Color(0.55f, 0.60f, 0.68f, 1f);

        private static readonly Color MethodGold = new Color(1.00f, 0.75f, 0.20f, 1f);
        private static readonly Color MethodCyan = new Color(0.20f, 0.85f, 0.95f, 1f);
        private static readonly Color MethodGreen = new Color(0.25f, 0.95f, 0.45f, 1f);
        private static readonly Color MethodViolet = new Color(0.70f, 0.50f, 0.95f, 1f);
        private static readonly Color MethodCoral = new Color(0.95f, 0.35f, 0.35f, 1f);
        private static readonly Color MethodOrange = new Color(0.95f, 0.60f, 0.35f, 1f);

        private static readonly int PropBaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int PropMainTex = Shader.PropertyToID("_MainTex");

        #endregion
    }
}
