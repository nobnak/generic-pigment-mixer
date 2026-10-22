# Generic Pigment Mixer for Unity

**English** | [日本語](README.ja.md)

A real-time physical pigment color mixing library for Unity.  
Eliminates muddy color degradation (such as blue + yellow turning into gray or dull ocher) caused by digital additive blending (RGB Lerp) or standard texture downsampling (Box/Mipmap averaging), reproducing the vibrant, natural look of real paint.

This library provides **two distinct shader engines** tailored to different performance and fidelity requirements:

- **PolyMixer**: 4th-degree polynomial regression model. Ultra-lightweight (~330 FMAs), ideal for high-framerate real-time 3D rendering.
- **SpectralMixer 16**: 16-band SIMD Kubelka-Munk physics model. Fully Linear RGB (HDR native), supporting arbitrary multi-color mixing and physical pixel downsampling.

---

## Engine Selection Guide

| Feature | **PolyMixer** (Fastest) | **SpectralMixer 16** (Physics Recommended) | Additive (RGB Lerp) |
| :--- | :--- | :--- | :--- |
| **Mathematical Model** | 4th-degree polynomial regression | **16-band single-constant KM model** | Linear interpolation |
| **GPU Cost** | **~330 FMAs** (Minimal registers) | **~480 ALUs** (float4x4 SIMD) | 1 ALU |
| **Blue + Yellow ($t=0.5$)** | **Vibrant emerald green** | **Deep forest green** | **Dull ocher to gray** |
| **Error vs Ground Truth** | $\Delta E \approx 2.07$ (vs Mixbox) | **$\Delta E \le 2.62$ (Imperceptible)** | $\Delta E \approx 37 \sim 46$ (Failed) |
| **Multi-Color Mixing ($N \ge 3$)** | △ Requires cascaded pairwise lerps | **◎ 1-pass simultaneous mix + tinting** | × Naive additive average |
| **Pixel Downsampling** | △ Hierarchical 2x2 tournament | **◎ Accumulator linear aggregation** | × Severe graying (Optical average) |
| **Color Space** | sRGB / Linear (via outer conversion) | **Fully Linear RGB (HDR Native)** | N/A |
| **Textures / LUTs** | **None (0 bytes)** | **None (0 bytes)** | None |
| **Recommended Use Cases** | Real-time 3D / Gameplay post-processing | **HDR rendering / Mipmaps / Painting tools** | Standard brightness fade |

- **When to choose PolyMixer**: When frame rate is paramount and you need the lightest possible 2-color pigment transition.
- **When to choose SpectralMixer 16**: When physical fidelity, simultaneous multi-color mixing, texture downsampling, or HDR/Linear pipeline integration is required.

---

## Installation (UPM)

Install via the Unity Package Manager using "**Add package from git URL...**" or by adding it directly to `Packages/manifest.json`:

```text
https://github.com/nobnak/generic-pigment-mixer.git?path=/Packages/jp.nobnak.generic_pigment_mixer
```

`Packages/manifest.json`:
```json
{
  "dependencies": {
    "jp.nobnak.generic_pigment_mixer": "https://github.com/nobnak/generic-pigment-mixer.git?path=/Packages/jp.nobnak.generic_pigment_mixer",
    "com.unity.mathematics": "1.2.6"
  }
}
```

### Importing Samples
In Unity's Package Manager window, select **Generic Pigment Mixer**, navigate to the **Samples** tab, and click **Import** next to **Pigment Mixing Comparison** to import comparison scenes and benchmark runner tools.

---

## 1. PolyMixer (4th-Degree Polynomial Approximation Engine)

An ultra-lightweight engine ported from [filament-mixer](https://github.com/justinh-rahb/filament-mixer). Achieves Mixbox-approximated vibrant pigment mixing purely via straight-line ALU instructions (~330 FMAs), without any textures, LUTs, or texture sampler overhead.

### Key Characteristics
- **Ultra-low cost**: Consumes zero texture bandwidth and minimal shader register pressure.
- **Clear 2-color transitions**: Generates a rich, saturated emerald green between blue and yellow.

### Usage in HLSL Shaders
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/PolyMixer.hlsl"

fixed4 frag(v2f i) : SV_Target {
    float3 colA = tex2D(_MainTex, i.uv).rgb;
    float3 colB = _PaintColor.rgb;
    float t = _BlendRatio; // 0.0 ~ 1.0

    // Blue + Yellow -> Vibrant emerald green
    float3 mixed = PolyMixer_Lerp(colA, colB, t);
    return float4(mixed, 1.0);
}
```

### Usage in Shader Graph
1. Create a **Custom Function** node.
2. **Type**: `File`, pointing to `Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/PolyMixer.hlsl`.
3. **Name**: `PolyMixer_Lerp`
4. **Inputs**: `c1` (Vector3), `c2` (Vector3), `t` (Float)
5. **Outputs**: `Out` (Vector3)

### Usage in C# Scripts
```csharp
using UnityEngine;

Color blue = new Color(0f / 255f, 33f / 255f, 133f / 255f);
Color yellow = new Color(252f / 255f, 211f / 255f, 0f / 255f);

Color green = PolyMixer.Lerp(blue, yellow, 0.5f);
// green -> RGB(47, 141, 56) (Vibrant emerald green)
```

---

## 2. SpectralMixer 16 (16-Band Spectral Physics Engine)

A physics-based shader engine reducing the Kubelka-Munk optical model from [Spectral.js](https://github.com/rvanwijnen/spectral.js) into 16 SIMD spectral bands (`float4` $\times 4$).

### Key Characteristics
- **Fully Linear RGB Native**: Designed for Unity's HDR/Linear color pipeline, avoiding gamma conversion overhead.
- **Imperceptible Error vs Ground Truth**: Achieves a mean $\Delta E = 2.62$ compared to the 38-wavelength reference (well below the human threshold $\Delta E < 3.0$).
- **Pure Straight-Line ALU (~480 ALUs)**: Eliminates dynamic loops and array index register spills by unfolding into unrolled `float4` SIMD vector instructions.
- **Physical Pixel Downsampling**: Linearly accumulates absorption-to-scattering ratios $(K/S)$, enabling multi-sample aggregation and texture filtering in a single pass.

### HLSL: 2-Color Mixing
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/SpectralMixer16.hlsl"

// Mix two Linear RGB colors via blend ratio t
float3 mixedLinear = Spectral16_Lerp(colA_Linear.rgb, colB_Linear.rgb, t);
```

### HLSL: 2x2 Pixel Downsampling (Mipmap / Box Filter)
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/SpectralMixer16.hlsl"

// Physically blends 4 pixels into 1 without muddy optical averaging
float3 downsampled = Spectral16_Downsample2x2(c00, c10, c01, c11);
```

### HLSL: Arbitrary Block Aggregation (Compute Shader / Accumulator)
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/SpectralMixer16.hlsl"

Spectral16_Accum acc = Spectral16_Accum_New();

// Accumulate arbitrary pixels (weight: filter weight / area, tint: tinting strength)
Spectral16_Accum_Add(acc, pixelA_Linear, weightA);
Spectral16_Accum_Add(acc, pixelB_Linear, weightB);
Spectral16_Accum_Add(acc, pixelC_Linear, weightC);

// Resolve once at the end (heavy KM inverse & XYZ integration evaluated only once)
float3 finalLinear = Spectral16_Accum_Resolve(acc);
```

### Usage in C# Scripts
```csharp
using UnityEngine;

// 2-Color Linear RGB mixing
Color mixed = SpectralMixer16.Lerp(colA, colB, 0.5f);

// 2x2 Downsampling
Color downsampled = SpectralMixer16.Downsample2x2(c00, c10, c01, c11);

// Arbitrary N-color palette mixing with weights
Color palette = SpectralMixer16.Mix(new Color[] { c1, c2, c3 }, new float[] { 1f, 2f, 0.5f });
```

---

## Visual Benchmarks & Verification

### 1. 2-Color Mixing Benchmark (Color Mixing Strip)

Comparison of color transition curves between 38-band ground truth, `SpectralMixer 16`, 4th-degree polynomial `PolyMixer`, and conventional additive blending (Linear RGB Lerp):

![Spectral16 Color Mixing Comparison](Assets/Artifacts/Spectral16_ColorMixing_Comparison.png)

- **Linear RGB Lerp**: Drops significantly in luminance and saturation across the center, degrading into dull gray.
- **PolyMixer**: Produces a bright, high-chroma emerald green.
- **SpectralMixer 16**: Closely mirrors the 38-band physics reference, producing deep forest green (Pure Blue + Pure Yellow: $\Delta E = 1.30$, Blue + Yellow: $\Delta E = 2.07$, overall mean $\Delta E = 2.62$), safely below the Just Noticeable Difference limit ($\Delta E < 3.0$).

### 2. Pixel Downsampling Benchmark (64x64 -> 1x1 Reduction)

Comparison of color preservation when downsampling a $64 \times 64$ checkerboard to a single $1 \times 1$ pixel:

![Spectral16 Downsampling Comparison](Assets/Artifacts/Spectral16_Downsampling_Comparison.png)

| Method | Blue + Yellow ($1 \times 1$) | Pure Blue + Pure Yellow ($1 \times 1$) | Red + Cyan (Complementary) | Characteristics |
| :--- | :--- | :--- | :--- | :--- |
| **[1] sRGB Box Average** | `RGB(126, 122, 66)`<br>$\Delta E = 37.83$ (Ocher) | `RGB(128, 128, 128)`<br>$\Delta E = 46.31$ (Mid Gray) | `RGB(128, 128, 128)`<br>$\Delta E = 29.05$ (Achromatic) | Naive average without gamma correction. Severe muddying. |
| **[2] Linear RGB Average** | `RGB(185, 156, 96)`<br>$\Delta E = 48.22$ (Light Beige) | `RGB(188, 188, 188)`<br>$\Delta E = 51.70$ (Light Gray) | `RGB(188, 188, 188)`<br>$\Delta E = 45.29$ (Achromatic) | Standard optical Mipmap average. Fails completely for pigment textures. |
| **[3] PolyMixer Hierarchical**| `RGB( 48, 142, 56)`<br>$\Delta E = 2.91$ (Bright Green) | `RGB( 63, 153, 103)`<br>$\Delta E = 6.88$ (Teal Green) | `RGB(140, 110, 106)`<br>$\Delta E = 16.08$ (Muted Earth) | 2x2 tournament reduction. Fast pure ALU, distinct polynomial curve. |
| **[4] SpectralMixer 16** | `RGB( 65, 146, 64)`<br>**$\Delta E = 2.07$ (Deep Green)** | `RGB( 57, 142, 85)`<br>**$\Delta E = 1.30$ (Green)** | `RGB(136, 74, 74)`<br>**$\Delta E = 3.95$ (Deep Brown)** | **Near-perfect match with physics reference. Linear accumulation.** |
| **[5] 38D KM (Ground Truth)** | `RGB( 61, 148, 62)`<br>$\Delta E = 0.00$ (Reference Green) | `RGB( 57, 143, 84)`<br>$\Delta E = 0.00$ (Reference Green) | `RGB(128, 75, 75)`<br>$\Delta E = 0.00$ (Reference Earth) | Theoretical ground truth. High computation cost. |

- **Why standard averaging fails**:  
  Standard optical filtering models spatial diffusion of light sources, causing checkerboard pigments to neutralize into gray/ocher ($\Delta E \approx 30 \sim 50$).
- **Solution via Physical Accumulation**:  
  `SpectralMixer 16` accumulates each texel as absorption/scattering concentration $(K/S)$, running the spectral inverse solve only once at the end.

---

## Directory Structure

```text
├── Assets/                                     # Development & verification project
│   ├── Artifacts/                              # Documentation benchmark images
│   ├── Editor/
│   │   └── PigmentMixer.Dev/                   # Dev-only: auto-syncs samples to Samples~ on compile
│   │       ├── PigmentMixer.Dev.Editor.asmdef
│   │       └── PigmentMixerSampleCompileSync.cs
│   └── Samples/
│       └── Comparison/                         # Development sample source
│           ├── GenericPigmentMixer.Samples.asmdef
│           ├── ColorMixSampleDemo.cs
│           ├── Scenes/
│           │   └── ColorMixComparison.unity
│           ├── Shaders/
│           │   └── ColorMixCompare.compute
│           └── Editor/
│               ├── GenericPigmentMixer.Samples.Editor.asmdef
│               ├── Spectral16ExperimentRunner.cs
│               ├── DimensionAndDownsamplingCompareRunner.cs
│               ├── DownsamplingCompareRunner.cs
│               ├── ColorMixCompareRunner.cs
│               ├── SpectralReductionCompareRunner.cs
│               └── Spectral16TableExporter.cs
├── Packages/
│   ├── manifest.json
│   └── jp.nobnak.generic_pigment_mixer/         # ★ UPM Package Root
│       ├── package.json                        # UPM package manifest
│       ├── GenericPigmentMixer.asmdef          # Core asmdef (Mathematics dependency)
│       ├── LICENSE                             # MIT license
│       ├── Runtime/                            # Core C# implementations
│       │   ├── PolyMixer.cs
│       │   ├── SpectralMixer16.cs
│       │   ├── SpectralMixerN.cs
│       │   ├── SpectralMixer8.cs
│       │   ├── SpectralMixer.cs
│       │   └── KubelkaMunk.cs
│       ├── ShaderLibrary/                      # Core HLSL shader includes
│       │   ├── PolyMixer.hlsl
│       │   ├── SpectralMixer16.hlsl
│       │   ├── Spectral16_Constants.inc.hlsl
│       │   └── SpectralMixer.hlsl
│       └── Samples~/                           # UPM Samples (auto-synced)
│           └── Comparison/
├── Docs/
│   ├── POLYMIXER_USER_GUIDE.md
│   └── RESEARCH_AND_COMPARISON.md
└── Tools/
    ├── generate_polymixer.ps1
    └── filament_mixer_raw.h
```

---

## Detailed Documentation

- [**PolyMixer User Guide & Performance Reference**](Docs/POLYMIXER_USER_GUIDE.md)  
  API signatures, Shader Graph integration, GPU resource breakdown (FMA instructions, bandwidth), and color space recommendations.
- [**Research, Comparison & Technical Report**](Docs/RESEARCH_AND_COMPARISON.md)  
  PolyMixer vs. GPMixer comparison, mathematical analysis of gamut constraints in the Kubelka-Munk model, and residual correction experiments.

---

## Credits & References

- [justinh-rahb/filament-mixer](https://github.com/justinh-rahb/filament-mixer) (MIT License)
- [rvanwijnen/spectral.js](https://github.com/rvanwijnen/spectral.js) (MIT License)
- [Mixbox](https://github.com/scrtwpns/mixbox) (Secret Weapons / Sochorová & Jamriška, 2021)
