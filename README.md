# Generic Pigment Mixer for Unity

**English** | [日本語](README.ja.md)

[![openupm](https://img.shields.io/npm/v/jp.nobnak.generic_pigment_mixer?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/jp.nobnak.generic_pigment_mixer/)
[![Unity 6000.0+](https://img.shields.io/badge/Unity-6000.0%2B-black.svg?logo=unity)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

![2-Color Mixing Benchmark: 16-Band KM vs 38D vs PolyMixer vs Additive](Assets/Artifacts/Spectral16_ColorMixing_Comparison.png)
*Above: 2-color pigment mixing benchmark (e.g. Blue [0, 33, 133] + Yellow [252, 211, 0]). Where standard digital additive mixing (Linear RGB Lerp) turns into a muddy, desaturated gray, this package's physical model (SpectralMixer 16) and polynomial approximation (PolyMixer) yield natural, vibrant emerald greens as expected from real pigments.*

---

**[jp.nobnak.generic_pigment_mixer](https://openupm.com/packages/jp.nobnak.generic_pigment_mixer/)** is a real-time physical pigment mixing (subtractive color blending) package for Unity.

It eliminates the "muddy gray blending" defect inherent in digital additive mixing (standard RGB Lerp) and standard texture downsampling (Box/Mipmap averaging), producing realistic and vibrant color mixtures directly inside shaders and C# scripts.

**Requires zero textures, 3D LUTs, or sampler overhead (0 bytes)**, running purely on GPU ALU instructions (SIMD) with minimal register pressure.

## Key Features

- ⚡ **PolyMixer (Fastest / Polynomial Approximation)**: 4th-degree polynomial regression engine (~330 FMAs). Straight-line ALU execution with zero branches. Ideal for 2-color gradient effects and high-framerate 3D rendering.
- 🎨 **SpectralMixer 16 (High Precision / 16-Band SIMD Physical Model)**: 16-band reduced-order Kubelka-Munk model (~480 ALUs via `float4x4` SIMD). Full Linear RGB (HDR native) support, simultaneous $N \ge 3$ color blending, and pixel downsampling.
- 🧪 **Physical Accumulation (Accumulator API)**: Directly accumulates arbitrary pixels/colors in the latent spectral ($K/S$) space without order dependence. Resolves the heavy inverse KM solve only once at the end.
- 📉 **Physical Pixel Downsampling**: Blends high-frequency checkerboards and texture patterns via subtractive color physics, avoiding washed-out optical graying in Mipmaps.
- 🛠️ **Seamless Integration**: Ready to use in HLSL shaders, C# scripts, and Shader Graph (Custom Function Node).

---

## Installation

Install via [OpenUPM](https://openupm.com/packages/jp.nobnak.generic_pigment_mixer/) (recommended) or Git URL.

### Option 1: Install via OpenUPM (Recommended)

#### A. Using OpenUPM CLI
```bash
openupm add jp.nobnak.generic_pigment_mixer
```

#### B. Using Scoped Registry in Unity Editor
1. In Unity, open **Edit → Project Settings → Package Manager → Scoped Registries**.
2. Add the following entry and click **Save**:

| Field | Value |
| :--- | :--- |
| **Name** | `package.openupm.com` |
| **URL** | `https://package.openupm.com` |
| **Scope(s)** | `jp.nobnak` |

3. Open **Window → Package Manager**, select **My Registries** in the package filter, and install **Generic Pigment Mixer**.

#### C. Directly Editing `Packages/manifest.json`
```json
{
  "dependencies": {
    "jp.nobnak.generic_pigment_mixer": "1.0.0",
    "com.unity.mathematics": "1.2.6"
  },
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "jp.nobnak"
      ]
    }
  ]
}
```

---

### Option 2: Install via Git URL

In Unity Package Manager, click **「+」→「Add package from git URL...」** and enter:

```text
https://github.com/nobnak/generic-pigment-mixer.git?path=/Packages/jp.nobnak.generic_pigment_mixer
```

To lock to a specific release tag:
```text
https://github.com/nobnak/generic-pigment-mixer.git?path=/Packages/jp.nobnak.generic_pigment_mixer#v1.0.0
```

---

## Quick Start

### 1. In HLSL Shaders

#### A. 2-Color Pigment Mixing (SpectralMixer 16 / Linear RGB)
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/SpectralMixer16.hlsl"

// Mix two Linear RGB colors via blend ratio t (0.0 - 1.0)
float3 mixedLinear = Spectral16_Lerp(colA_Linear.rgb, colB_Linear.rgb, t);
```

#### B. 2-Color Pigment Mixing (PolyMixer / Ultra-fast)
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/PolyMixer.hlsl"

// Ultra-fast polynomial mix via blend ratio t (0.0 - 1.0)
float3 mixedColor = PolyMixer_Lerp(colA.rgb, colB.rgb, t);
```

#### C. Physical 2x2 Pixel Downsampling (Mipmaps / Antialiasing)
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/SpectralMixer16.hlsl"

// Physically blends 4 pixels into 1 without muddy optical averaging
float3 downsampled = Spectral16_Downsample2x2(c00, c10, c01, c11);
```

#### D. Arbitrary Block Aggregation (Accumulator API)
```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/SpectralMixer16.hlsl"

// 1. Initialize accumulator
Spectral16_Accum acc = Spectral16_Accum_New();

// 2. Accumulate arbitrary colors/pixels (weight: filter weight, tint: tinting strength)
// Additive in latent K/S concentration space, completely order-independent
Spectral16_Accum_Add(acc, pixelA_Linear, weightA);
Spectral16_Accum_Add(acc, pixelB_Linear, weightB);
Spectral16_Accum_Add(acc, pixelC_Linear, weightC);

// 3. Resolve once at the end (heavy KM inverse & XYZ integration evaluated only once)
float3 finalLinear = Spectral16_Accum_Resolve(acc);
```

### 2. In C# Scripts

```csharp
using UnityEngine;

// 1. PolyMixer (4th-degree polynomial)
Color polyMixed = PolyMixer.Lerp(colorA, colorB, t);

// 2. SpectralMixer 16 (16-band KM)
Color spectralMixed = SpectralMixer16.Lerp(colorA, colorB, t);

// 3. Multi-color physical blending (N >= 3)
Color multiMixed = SpectralMixer16.Mix(
    new Color[] { colorA, colorB, colorC },
    new float[] { 0.5f, 0.3f, 0.2f } // weights
);
```

### 3. In Shader Graph
1. Create a **Custom Function** node.
2. Set **Type** to `File`, pointing to `Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/PolyMixer.hlsl` (or `SpectralMixer16.hlsl`).
3. Set **Name** to `PolyMixer_Lerp` (or `Spectral16_Lerp`).
4. Set **Inputs**: `c1` (Vector3), `c2` (Vector3), `t` (Float).
5. Set **Outputs**: `Out` (Vector3).

---

## Samples

In Package Manager, select **Generic Pigment Mixer**, navigate to the **Samples** tab, and click **Import** next to **Pigment Mixing Comparison**.

| Sample Asset | Description |
| :--- | :--- |
| `ColorMixComparison.unity` | Real-time interactive comparison scene with gradient strips, swatches, and $\Delta E$ readouts |
| `ColorMixSampleDemo.cs` | Controller supporting interactive blending, 5 preset overviews, and checkerboard downsampling |
| `ColorMixCompare.compute` | Compute shader comparison implementation |
| Benchmark Runners (`Editor/`) | Automated image generation and CSV data exporter tools |

---

## Engine Selection Guide

| Criteria | **PolyMixer** (Fastest) | **SpectralMixer 16** (Physical / Recommended) | Standard Additive (RGB Lerp) |
| :--- | :--- | :--- | :--- |
| **Mathematical Model** | 4th-degree polynomial regression | **16-band single-constant KM model** | Linear interpolation |
| **GPU Cost** | **~330 FMAs** (minimal registers) | **~480 ALUs** (`float4x4` SIMD) | 1 ALU |
| **Blue + Yellow ($t=0.5$)** | **Vibrant Emerald Green** | **Deep Forest Green** | **Muddy Grayish-Ochre** |
| **Perceptual Error** | $\Delta E \approx 2.07$ (vs Mixbox) | **$\Delta E \le 2.62$ (below JND threshold)** | $\Delta E \approx 37 - 46$ (failed) |
| **Multi-Color ($N \ge 3$)** | Cascaded 2-color pairs | **1-pass simultaneous $N$-color mixing** | Simple additive average |
| **Pixel Downsampling** | Hierarchical 2x2 reduction | **Linear spectral accumulator** | Muddy optical graying |
| **Color Space** | sRGB / Linear (external conv) | **Native Linear RGB (HDR compatible)** | N/A |
| **Textures / LUTs** | **None (0 bytes)** | **None (0 bytes)** | None |
| **Recommended Use** | Real-time 3D, mobile, post-process | **HDR pipelines, Mipmap generation, paint tools** | Brightness fades |

---

## Pixel Downsampling Benchmark (Texture Reduction)

Comparison of color preservation when downsampling a high-frequency $64 \times 64$ checkerboard to $1 \times 1$:

![Pixel Downsampling Comparison](Assets/Artifacts/Spectral16_Downsampling_Comparison.png)

- **Standard Optical Average (sRGB / Linear Box Avg)**: High-frequency color dots blend additively like light, resulting in washed-out grays.
- **SpectralMixer 16 / PolyMixer**: Physical pigment interaction is preserved, yielding a rich, saturated green identical to physical paint.

---

## Directory Structure

```text
Packages/jp.nobnak.generic_pigment_mixer/   # ★ UPM Package Root
├── package.json                            # Package manifest
├── GenericPigmentMixer.asmdef              # Mathematics dependency
├── Runtime/                                # C# implementations
│   ├── PolyMixer.cs                        # 4th-degree polynomial model
│   ├── SpectralMixer16.cs                  # 16-band SIMD KM
│   ├── SpectralMixerN.cs                   # Arbitrary K-dimensional KM
│   ├── SpectralMixer8.cs                   # 8-band SIMD KM
│   ├── SpectralMixer.cs                    # 38-wavelength Spectral.js compatible
│   └── KubelkaMunk.cs                      # 38-wavelength physics reference
├── ShaderLibrary/                          # HLSL shader includes
│   ├── PolyMixer.hlsl                      # ~330 FMA fastest shader
│   ├── SpectralMixer16.hlsl                # ~480 ALU SIMD physical shader
│   ├── Spectral16_Constants.inc.hlsl       # 16-band constants table
│   └── SpectralMixer.hlsl                  # 38-wavelength reference shader
└── Samples~/                               # UPM Sample assets
    └── Comparison/
        ├── Scenes/ColorMixComparison.unity # Interactive comparison scene
        ├── Shaders/ColorMixCompare.compute # Compute shader comparison
        └── ColorMixSampleDemo.cs           # Runtime controller
```

---

## Technical Documentation & References

- [PolyMixer User Guide & Performance Details](Docs/POLYMIXER_USER_GUIDE.md)
- [Research Notes, KM Reduction Mathematics & Spectral.js Verification](Docs/RESEARCH_AND_COMPARISON.md)

### Acknowledgments & Prior Art
- **PolyMixer**: [filament-mixer](https://github.com/justinh-rahb/filament-mixer) (Justin Hensley) — Apache 2.0 License
- **Spectral.js**: [spectral.js](https://github.com/rvanwijnen/spectral.js) (Rolf van Wijnen) — MIT License
- **Mixbox**: [Mixbox](https://scrtwpns.com/mixbox/) (Secret Weapons)

---

## License

See [LICENSE](LICENSE) (MIT License).
