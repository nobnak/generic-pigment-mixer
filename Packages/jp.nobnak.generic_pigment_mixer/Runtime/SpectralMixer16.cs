using System;
using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// 16-dimensional Kubelka-Munk Spectral Pigment Mixer (C# interface).
/// Matches Assets/Shaders/SpectralMixer16.hlsl.
/// Fully operates in Linear RGB for HDR & modern rendering pipelines.
/// </summary>
public static class SpectralMixer16 {
    #region public interface

    /// <summary>
    /// Linearly mixes two Linear RGB colors via 16-band KM pigment simulation.
    /// </summary>
    public static Color Lerp(Color lrgb1, Color lrgb2, float t) {
        return SpectralMixerN.D16.Lerp(lrgb1, lrgb2, t);
    }

    /// <summary>
    /// Mixes two Linear RGB colors with individual blend factor and tinting strength.
    /// </summary>
    public static Color Mix(Color lrgb1, Color lrgb2, float factor1, float factor2, float tint1 = 1f, float tint2 = 1f) {
        return SpectralMixerN.D16.Mix(lrgb1, lrgb2, factor1, factor2, tint1, tint2);
    }

    /// <summary>
    /// Downsamples a 2x2 block of Linear RGB pixels into 1 pixel via 16-band KM pigment mixing.
    /// </summary>
    public static Color Downsample2x2(Color c00, Color c10, Color c01, Color c11) {
        return SpectralMixerN.D16.Mix(new Color[] { c00, c10, c01, c11 });
    }

    /// <summary>
    /// Mixes an arbitrary array of Linear RGB colors (for downsampling N pixels or custom palettes).
    /// </summary>
    public static Color Mix(Color[] colors, float[] weights = null) {
        return SpectralMixerN.D16.Mix(colors, weights);
    }

    #endregion
}
