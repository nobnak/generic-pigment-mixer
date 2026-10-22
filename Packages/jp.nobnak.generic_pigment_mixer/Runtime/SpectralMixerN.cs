using System;
using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Generalized K-dimensional reduced-order Kubelka-Munk Spectral Mixer.
/// Supports arbitrary dimension K (e.g. 8, 12, 16, 38).
/// </summary>
public sealed class SpectralMixerProfile {
    public readonly int Dimension;
    public readonly float[,] Bases;
    public readonly float3[] Cmf;

    public SpectralMixerProfile(int dimension) {
        if (dimension < 4 || dimension > 38) {
            throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be between 4 and 38.");
        }

        Dimension = dimension;
        Bases = new float[dimension, 7];
        Cmf = new float3[dimension];

        if (dimension == 38) {
            for (int i = 0; i < 38; i++) {
                Cmf[i] = SpectralMixer.CMF[i];
                for (int k = 0; k < 7; k++) {
                    Bases[i, k] = SpectralMixer.BASES[i, k];
                }
            }
            return;
        }

        // Compute band boundaries spanning 38 wavelengths
        int[] bandStarts = new int[dimension + 1];
        for (int b = 0; b <= dimension; b++) {
            bandStarts[b] = Mathf.RoundToInt((float)b * 38f / dimension);
        }

        for (int b = 0; b < dimension; b++) {
            int start = bandStarts[b];
            int end = bandStarts[b + 1];

            float3 sumCmf = float3.zero;
            float sumWeight = 0f;

            for (int i = start; i < end; i++) {
                sumCmf += SpectralMixer.CMF[i];
                float w = SpectralMixer.CMF[i].x + SpectralMixer.CMF[i].y + SpectralMixer.CMF[i].z + 1e-6f;
                sumWeight += w;
            }
            Cmf[b] = sumCmf;

            for (int k = 0; k < 7; k++) {
                float weightedBase = 0f;
                for (int i = start; i < end; i++) {
                    float w = SpectralMixer.CMF[i].x + SpectralMixer.CMF[i].y + SpectralMixer.CMF[i].z + 1e-6f;
                    weightedBase += SpectralMixer.BASES[i, k] * w;
                }
                Bases[b, k] = weightedBase / sumWeight;
            }
        }
    }

    public Color Lerp(Color c1, Color c2, float t) {
        return Mix(c1, c2, 1f - t, t, 1f, 1f);
    }

    public Color Mix(Color c1, Color c2, float factor1, float factor2, float tint1 = 1f, float tint2 = 1f) {
        float3 rgb1 = new float3(c1.r, c1.g, c1.b);
        float3 rgb2 = new float3(c2.r, c2.g, c2.b);
        float3 mixed = MixRgb(rgb1, rgb2, factor1, factor2, tint1, tint2);
        return new Color(mixed.x, mixed.y, mixed.z, Mathf.Lerp(c1.a, c2.a, factor2 / Mathf.Max(0.0001f, factor1 + factor2)));
    }

    public Color Mix(Color[] colors, float[] weights = null) {
        if (colors == null || colors.Length == 0) return Color.black;
        if (colors.Length == 1) return colors[0];

        Span<float> ksMix = stackalloc float[Dimension];
        ksMix.Clear();

        float totalC = 0f;
        Span<float> R = stackalloc float[Dimension];

        for (int k = 0; k < colors.Length; k++) {
            Color c = colors[k];
            float w = (weights != null && k < weights.Length) ? weights[k] : 1f;
            if (w <= 0f) continue;

            float3 lrgb = SrgbToLinear(new float3(c.r, c.g, c.b));
            LinearToReflectance(lrgb, R);
            float lum = ReflectanceToXyz(R).y;
            float ck = (w * w) * lum;
            totalC += ck;

            for (int i = 0; i < Dimension; i++) {
                ksMix[i] += KS(R[i]) * ck;
            }
        }

        if (totalC <= 0f) return colors[0];

        Span<float> Rmix = stackalloc float[Dimension];
        for (int i = 0; i < Dimension; i++) {
            Rmix[i] = KM(ksMix[i] / totalC);
        }

        float3 xyz = ReflectanceToXyz(Rmix);
        float3 srgb = XyzToSrgb(xyz);
        return new Color(srgb.x, srgb.y, srgb.z, 1f);
    }

    public float3 MixRgb(float3 color1, float3 color2, float factor1, float factor2, float tint1 = 1f, float tint2 = 1f) {
        float3 lrgb1 = SrgbToLinear(color1);
        float3 lrgb2 = SrgbToLinear(color2);

        Span<float> R1 = stackalloc float[Dimension];
        Span<float> R2 = stackalloc float[Dimension];
        Span<float> Rmix = stackalloc float[Dimension];

        LinearToReflectance(lrgb1, R1);
        LinearToReflectance(lrgb2, R2);

        float lum1 = ReflectanceToXyz(R1).y;
        float lum2 = ReflectanceToXyz(R2).y;

        float c1 = (factor1 * factor1) * (tint1 * tint1) * lum1;
        float c2 = (factor2 * factor2) * (tint2 * tint2) * lum2;
        float totalC = c1 + c2;
        if (totalC <= 0f) return color1;

        for (int i = 0; i < Dimension; i++) {
            float ksMix = KS(R1[i]) * c1 + KS(R2[i]) * c2;
            Rmix[i] = KM(ksMix / totalC);
        }

        float3 xyz = ReflectanceToXyz(Rmix);
        return XyzToSrgb(xyz);
    }

    #region private methods

    private static float Uncompand(float x) {
        return (x < 0.04045f) ? x / 12.92f : math.pow((x + 0.055f) / 1.055f, 2.4f);
    }

    private static float Compand(float x) {
        return (x < 0.0031308f) ? x * 12.92f : 1.055f * math.pow(x, 1f / 2.4f) - 0.055f;
    }

    private static float3 SrgbToLinear(float3 srgb) {
        return new float3(Uncompand(srgb.x), Uncompand(srgb.y), Uncompand(srgb.z));
    }

    private static float3 LinearToSrgb(float3 lrgb) {
        return math.clamp(new float3(Compand(lrgb.x), Compand(lrgb.y), Compand(lrgb.z)), 0f, 1f);
    }

    private void LinearToReflectance(float3 lrgb, Span<float> R) {
        float w = math.min(lrgb.x, math.min(lrgb.y, lrgb.z));
        lrgb -= w;

        float c = math.min(lrgb.y, lrgb.z);
        float m = math.min(lrgb.x, lrgb.z);
        float y = math.min(lrgb.x, lrgb.y);

        float r = math.min(math.max(0f, lrgb.x - lrgb.z), math.max(0f, lrgb.x - lrgb.y));
        float g = math.min(math.max(0f, lrgb.y - lrgb.z), math.max(0f, lrgb.y - lrgb.x));
        float b = math.min(math.max(0f, lrgb.z - lrgb.y), math.max(0f, lrgb.z - lrgb.x));

        for (int i = 0; i < Dimension; i++) {
            float val = w * Bases[i, 0] + c * Bases[i, 1] + m * Bases[i, 2] + y * Bases[i, 3] +
                        r * Bases[i, 4] + g * Bases[i, 5] + b * Bases[i, 6];
            R[i] = math.max(1e-16f, val);
        }
    }

    private float3 ReflectanceToXyz(ReadOnlySpan<float> R) {
        float3 xyz = float3.zero;
        for (int i = 0; i < Dimension; i++) {
            xyz += R[i] * Cmf[i];
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
}

/// <summary>
/// Global static profiles for common dimensions: 8D, 12D, 16D, and 38D.
/// </summary>
public static class SpectralMixerN {
    public static readonly SpectralMixerProfile D8 = new SpectralMixerProfile(8);
    public static readonly SpectralMixerProfile D12 = new SpectralMixerProfile(12);
    public static readonly SpectralMixerProfile D16 = new SpectralMixerProfile(16);
    public static readonly SpectralMixerProfile D38 = new SpectralMixerProfile(38);
}
