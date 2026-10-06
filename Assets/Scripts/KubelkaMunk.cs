using System;
using UnityEngine;

public class KubelkaMunk {
    public struct Pigment {
        public string name;
        public float[] K; // 38 elements
        public float[] S; // 38 elements

        public Pigment(string name, float[] k, float[] s) {
            this.name = name;
            this.K = k;
            this.S = s;
        }
    }

    private readonly float k1;
    private readonly float k2;
    private readonly float yD65;

    public KubelkaMunk(float k1 = 0.04f, float k2 = 0.6f) {
        this.k1 = k1;
        this.k2 = k2;
        this.yD65 = TrapezoidIntegral(Multiply(CieYBar, D65Illuminant));
    }

    #region public interface

    public Color MixPigments(Pigment[] pigments, float[] concentrations, bool applyGamma = true) {
        int n = pigments.Length;
        float total = 0f;
        for (int i = 0; i < n; i++) total += Mathf.Max(0f, concentrations[i]);
        if (total <= 0f) total = 1f;

        float[] kMix = new float[WaveLengthCount];
        float[] sMix = new float[WaveLengthCount];

        for (int i = 0; i < n; i++) {
            float c = Mathf.Max(0f, concentrations[i]) / total;
            for (int w = 0; w < WaveLengthCount; w++) {
                kMix[w] += c * pigments[i].K[w];
                sMix[w] += c * pigments[i].S[w];
            }
        }

        // 1. Compute reflectance R = 1 + a - sqrt(a^2 + 2a)
        float[] r = new float[WaveLengthCount];
        for (int w = 0; w < WaveLengthCount; w++) {
            float s = Mathf.Max(1e-10f, sMix[w]);
            float a = kMix[w] / s;
            float b = Mathf.Sqrt(a * a + 2f * a);
            r[w] = Mathf.Clamp01(1f + a - b);
        }

        // 2. Saunderson correction
        float[] rPrime = new float[WaveLengthCount];
        float f1 = (1f - k1) * (1f - k2);
        for (int w = 0; w < WaveLengthCount; w++) {
            float denom = Mathf.Max(1e-10f, 1f - k2 * r[w]);
            rPrime[w] = Mathf.Clamp01(f1 * r[w] / denom);
        }

        // 3. Reflectance to XYZ
        float x = TrapezoidIntegral(Multiply(Multiply(rPrime, D65Illuminant), CieXBar)) / yD65;
        float y = TrapezoidIntegral(Multiply(Multiply(rPrime, D65Illuminant), CieYBar)) / yD65;
        float z = TrapezoidIntegral(Multiply(Multiply(rPrime, D65Illuminant), CieZBar)) / yD65;

        // 4. XYZ to linear sRGB
        float linR = 3.2406f * x - 1.5372f * y - 0.4986f * z;
        float linG = -0.9689f * x + 1.8758f * y + 0.0415f * z;
        float linB = 0.0557f * x - 0.2040f * y + 1.0570f * z;

        if (!applyGamma) return new Color(Mathf.Clamp01(linR), Mathf.Clamp01(linG), Mathf.Clamp01(linB), 1f);

        // 5. sRGB gamma
        return new Color(LinearToSrgb(linR), LinearToSrgb(linG), LinearToSrgb(linB), 1f);
    }

    #endregion

    #region private methods

    private static float LinearToSrgb(float val) {
        if (val <= 0.0031308f) return Mathf.Clamp01(12.92f * val);
        return Mathf.Clamp01(1.055f * Mathf.Pow(Mathf.Max(0f, val), 1f / 2.4f) - 0.055f);
    }

    private static float[] Multiply(float[] a, float[] b) {
        float[] result = new float[a.Length];
        for (int i = 0; i < a.Length; i++) result[i] = a[i] * b[i];
        return result;
    }

    private static float TrapezoidIntegral(float[] values) {
        // Uniform 10nm spacing from 380 to 750
        float sum = 0.5f * (values[0] + values[values.Length - 1]);
        for (int i = 1; i < values.Length - 1; i++) sum += values[i];
        return sum * 10f;
    }

    #endregion

    #region constants / static

    public const int WaveLengthCount = 38;

    private static readonly float[] CieXBar = {
        0.0014f, 0.0042f, 0.0143f, 0.0435f, 0.1344f, 0.2839f, 0.3483f, 0.3362f, 0.2908f, 0.1954f,
        0.0956f, 0.0320f, 0.0049f, 0.0093f, 0.0633f, 0.1655f, 0.2904f, 0.4334f, 0.5945f, 0.7621f,
        0.9163f, 1.0263f, 1.0622f, 1.0026f, 0.8544f, 0.6424f, 0.4479f, 0.2835f, 0.1649f, 0.0874f,
        0.0468f, 0.0227f, 0.0114f, 0.0058f, 0.0029f, 0.0014f, 0.0007f, 0.0003f
    };

    private static readonly float[] CieYBar = {
        0.0000f, 0.0001f, 0.0004f, 0.0012f, 0.0040f, 0.0116f, 0.0230f, 0.0380f, 0.0600f, 0.0910f,
        0.1390f, 0.2080f, 0.3230f, 0.5030f, 0.7100f, 0.8620f, 0.9540f, 0.9950f, 0.9950f, 0.9520f,
        0.8700f, 0.7570f, 0.6310f, 0.5030f, 0.3810f, 0.2650f, 0.1750f, 0.1070f, 0.0610f, 0.0320f,
        0.0170f, 0.0082f, 0.0041f, 0.0021f, 0.0010f, 0.0005f, 0.0003f, 0.0001f
    };

    private static readonly float[] CieZBar = {
        0.0065f, 0.0201f, 0.0679f, 0.2074f, 0.6456f, 1.3856f, 1.7471f, 1.7721f, 1.6692f, 1.2876f,
        0.8130f, 0.4652f, 0.2720f, 0.1582f, 0.0782f, 0.0422f, 0.0203f, 0.0087f, 0.0039f, 0.0021f,
        0.0017f, 0.0011f, 0.0008f, 0.0003f, 0.0002f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f,
        0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f, 0.0000f
    };

    private static readonly float[] D65Illuminant = {
        49.98f, 52.31f, 54.65f, 68.70f, 82.75f, 87.12f, 91.49f, 92.46f, 93.43f, 90.06f,
        86.68f, 95.77f, 104.86f, 110.94f, 117.01f, 117.41f, 117.81f, 116.34f, 114.86f, 115.39f,
        115.92f, 112.37f, 108.81f, 109.08f, 109.35f, 108.58f, 107.80f, 106.30f, 104.79f, 106.24f,
        107.69f, 106.05f, 104.41f, 104.23f, 104.05f, 102.02f, 100.00f, 98.17f
    };

    private static float[] CreateGaussianPeak(float center, float width, float height) {
        float[] res = new float[WaveLengthCount];
        for (int i = 0; i < WaveLengthCount; i++) {
            float wl = 380f + i * 10f;
            float diff = (wl - center) / width;
            res[i] = height * Mathf.Exp(-diff * diff);
        }
        return res;
    }

    public static Pigment CreateCyan() {
        float[] k = new float[WaveLengthCount];
        float[] s = new float[WaveLengthCount];
        float[] p1 = CreateGaussianPeak(640f, 45f, 2.5f);
        float[] p2 = CreateGaussianPeak(600f, 20f, 1.5f);
        for (int i = 0; i < WaveLengthCount; i++) {
            k[i] = 0.02f + p1[i] + p2[i];
            s[i] = 0.5f;
        }
        return new Pigment("Cyan", k, s);
    }

    public static Pigment CreateMagenta() {
        float[] k = new float[WaveLengthCount];
        float[] s = new float[WaveLengthCount];
        float[] p1 = CreateGaussianPeak(530f, 20f, 3.0f);
        float[] p2 = CreateGaussianPeak(555f, 20f, 2.5f);
        for (int i = 0; i < WaveLengthCount; i++) {
            k[i] = 0.03f + p1[i] + p2[i];
            s[i] = 0.5f;
        }
        return new Pigment("Magenta", k, s);
    }

    public static Pigment CreateYellow() {
        float[] k = new float[WaveLengthCount];
        float[] s = new float[WaveLengthCount];
        float[] p1 = CreateGaussianPeak(450f, 45f, 2.5f);
        float[] p2 = CreateGaussianPeak(400f, 40f, 2.0f);
        for (int i = 0; i < WaveLengthCount; i++) {
            k[i] = 0.02f + p1[i] + p2[i];
            s[i] = 0.5f;
        }
        return new Pigment("Yellow", k, s);
    }

    public static Pigment CreateWhite() {
        float[] k = new float[WaveLengthCount];
        float[] s = new float[WaveLengthCount];
        for (int i = 0; i < WaveLengthCount; i++) {
            k[i] = 0.0989f;
            s[i] = 0.5f;
        }
        return new Pigment("White", k, s);
    }

    public static Pigment CreateBlue() {
        float[] k = new float[WaveLengthCount];
        float[] s = new float[WaveLengthCount];
        float[] p1 = CreateGaussianPeak(650f, 70f, 2.0f);
        float[] p2 = CreateGaussianPeak(580f, 50f, 1.5f);
        for (int i = 0; i < WaveLengthCount; i++) {
            k[i] = 0.1f + p1[i] + p2[i];
            s[i] = 0.6f;
        }
        return new Pigment("Blue", k, s);
    }

    public static Pigment CreateRed() {
        float[] k = new float[WaveLengthCount];
        float[] s = new float[WaveLengthCount];
        float[] p1 = CreateGaussianPeak(450f, 60f, 2.0f);
        float[] p2 = CreateGaussianPeak(520f, 50f, 1.5f);
        float[] p3 = CreateGaussianPeak(490f, 40f, 1.0f);
        for (int i = 0; i < WaveLengthCount; i++) {
            k[i] = 0.1f + p1[i] + p2[i] + p3[i];
            s[i] = 0.6f;
        }
        return new Pigment("Red", k, s);
    }

    #endregion
}
