using System;
using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// 8-dimensional reduced-order version of Spectral.js (Kubelka-Munk pigment mixer).
/// Compresses 38 wavelength bands into 8 coarse spectral bands (GPU float4x2 register friendly).
/// </summary>
public static class SpectralMixer8 {
    #region public interface

    public static Color Lerp(Color c1, Color c2, float t) {
        return Mix(c1, c2, 1f - t, t, 1f, 1f);
    }

    public static Color Mix(Color c1, Color c2, float factor1, float factor2, float tint1 = 1f, float tint2 = 1f) {
        float3 rgb1 = new float3(c1.r, c1.g, c1.b);
        float3 rgb2 = new float3(c2.r, c2.g, c2.b);
        float3 mixed = MixRgb(rgb1, rgb2, factor1, factor2, tint1, tint2);
        return new Color(mixed.x, mixed.y, mixed.z, Mathf.Lerp(c1.a, c2.a, factor2 / Mathf.Max(0.0001f, factor1 + factor2)));
    }

    public static Color Mix(Color[] colors, float[] weights = null) {
        if (colors == null || colors.Length == 0) return Color.black;
        if (colors.Length == 1) return colors[0];

        Span<float> ksMix = stackalloc float[SPECTRAL_SIZE];
        ksMix.Clear();

        float totalC = 0f;
        Span<float> R = stackalloc float[SPECTRAL_SIZE];

        for (int k = 0; k < colors.Length; k++) {
            Color c = colors[k];
            float w = (weights != null && k < weights.Length) ? weights[k] : 1f;
            if (w <= 0f) continue;

            float3 lrgb = SrgbToLinear(new float3(c.r, c.g, c.b));
            LinearToReflectance(lrgb, R);
            float lum = ReflectanceToXyz(R).y;
            float ck = (w * w) * lum;
            totalC += ck;

            for (int i = 0; i < SPECTRAL_SIZE; i++) {
                ksMix[i] += KS(R[i]) * ck;
            }
        }

        if (totalC <= 0f) return colors[0];

        Span<float> Rmix = stackalloc float[SPECTRAL_SIZE];
        for (int i = 0; i < SPECTRAL_SIZE; i++) {
            Rmix[i] = KM(ksMix[i] / totalC);
        }

        float3 xyz = ReflectanceToXyz(Rmix);
        float3 srgb = XyzToSrgb(xyz);
        return new Color(srgb.x, srgb.y, srgb.z, 1f);
    }

    public static float3 MixRgb(float3 color1, float3 color2, float factor1, float factor2, float tint1 = 1f, float tint2 = 1f) {
        float3 lrgb1 = SrgbToLinear(color1);
        float3 lrgb2 = SrgbToLinear(color2);

        Span<float> R1 = stackalloc float[SPECTRAL_SIZE];
        Span<float> R2 = stackalloc float[SPECTRAL_SIZE];
        Span<float> Rmix = stackalloc float[SPECTRAL_SIZE];

        LinearToReflectance(lrgb1, R1);
        LinearToReflectance(lrgb2, R2);

        float lum1 = ReflectanceToXyz(R1).y;
        float lum2 = ReflectanceToXyz(R2).y;

        float c1 = (factor1 * factor1) * (tint1 * tint1) * lum1;
        float c2 = (factor2 * factor2) * (tint2 * tint2) * lum2;
        float totalC = c1 + c2;
        if (totalC <= 0f) return color1;

        for (int i = 0; i < SPECTRAL_SIZE; i++) {
            float ksMix = KS(R1[i]) * c1 + KS(R2[i]) * c2;
            Rmix[i] = KM(ksMix / totalC);
        }

        float3 xyz = ReflectanceToXyz(Rmix);
        return XyzToSrgb(xyz);
    }

    #endregion

    #region private methods

    private static float Uncompand(float x) {
        return (x < 0.04045f) ? x / 12.92f : math.pow((x + 0.055f) / 1.055f, SPECTRAL_GAMMA);
    }

    private static float Compand(float x) {
        return (x < 0.0031308f) ? x * 12.92f : 1.055f * math.pow(x, 1f / SPECTRAL_GAMMA) - 0.055f;
    }

    private static float3 SrgbToLinear(float3 srgb) {
        return new float3(Uncompand(srgb.x), Uncompand(srgb.y), Uncompand(srgb.z));
    }

    private static float3 LinearToSrgb(float3 lrgb) {
        return math.clamp(new float3(Compand(lrgb.x), Compand(lrgb.y), Compand(lrgb.z)), 0f, 1f);
    }

    private static void LinearToReflectance(float3 lrgb, Span<float> R) {
        float w = math.min(lrgb.x, math.min(lrgb.y, lrgb.z));
        lrgb -= w;

        float c = math.min(lrgb.y, lrgb.z);
        float m = math.min(lrgb.x, lrgb.z);
        float y = math.min(lrgb.x, lrgb.y);

        float r = math.min(math.max(0f, lrgb.x - lrgb.z), math.max(0f, lrgb.x - lrgb.y));
        float g = math.min(math.max(0f, lrgb.y - lrgb.z), math.max(0f, lrgb.y - lrgb.x));
        float b = math.min(math.max(0f, lrgb.z - lrgb.y), math.max(0f, lrgb.z - lrgb.x));

        for (int i = 0; i < SPECTRAL_SIZE; i++) {
            float val = w * BASES_8[i, 0] + c * BASES_8[i, 1] + m * BASES_8[i, 2] + y * BASES_8[i, 3] +
                        r * BASES_8[i, 4] + g * BASES_8[i, 5] + b * BASES_8[i, 6];
            R[i] = math.max(SPECTRAL_EPSILON, val);
        }
    }

    private static float3 ReflectanceToXyz(ReadOnlySpan<float> R) {
        float3 xyz = float3.zero;
        for (int i = 0; i < SPECTRAL_SIZE; i++) {
            xyz += R[i] * CMF_8[i];
        }
        return xyz;
    }

    private static float3 XyzToSrgb(float3 xyz) {
        float r = math.dot(new float3( 3.2409699419045200f, -1.5373831775700900f, -0.4986107602930030f), xyz);
        float g = math.dot(new float3(-0.9692436362808790f,  1.8759675015077200f,  0.0415550574071756f), xyz);
        float b = math.dot(new float3( 0.0556300796969936f, -0.2039769588889760f,  1.0569715142428700f), xyz);
        return LinearToSrgb(new float3(r, g, b));
    }

    private static float KS(float R) {
        return (1f - R) * (1f - R) / (2f * R);
    }

    private static float KM(float ks) {
        return 1f + ks - math.sqrt(ks * ks + 2f * ks);
    }

    #endregion

    #region constants / static

    public const int SPECTRAL_SIZE = 8;
    private const float SPECTRAL_GAMMA = 2.4f;
    private const float SPECTRAL_EPSILON = 1e-16f;

    // 8 band boundaries spanning 38 wavelengths
    private static readonly int[] BAND_STARTS = new int[] { 0, 5, 10, 15, 20, 25, 30, 34, 38 };

    internal static readonly float[,] BASES_8 = new float[8, 7];
    internal static readonly float3[] CMF_8 = new float3[8];

    static SpectralMixer8() {
        for (int b = 0; b < 8; b++) {
            int start = BAND_STARTS[b];
            int end = BAND_STARTS[b + 1];

            float3 sumCmf = float3.zero;
            float sumWeight = 0f;

            for (int i = start; i < end; i++) {
                sumCmf += SpectralMixer.CMF[i];
                float w = SpectralMixer.CMF[i].x + SpectralMixer.CMF[i].y + SpectralMixer.CMF[i].z + 1e-6f;
                sumWeight += w;
            }
            CMF_8[b] = sumCmf;

            for (int k = 0; k < 7; k++) {
                float weightedBase = 0f;
                for (int i = start; i < end; i++) {
                    float w = SpectralMixer.CMF[i].x + SpectralMixer.CMF[i].y + SpectralMixer.CMF[i].z + 1e-6f;
                    weightedBase += SpectralMixer.BASES[i, k] * w;
                }
                BASES_8[b, k] = weightedBase / sumWeight;
            }
        }
    }

    #endregion
}
