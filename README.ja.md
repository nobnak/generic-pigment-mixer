# Generic Pigment Mixer for Unity

[English](README.md) | **日本語**

Unity 向けのリアルタイム・物理顔料混色（ピグメントミキサー）ライブラリです。  
デジタル加色混合（RGB Lerp）や通常の画像縮小（Box/Mipmap 平均）で発生する「色の濁り（青＋黄が灰色や黄土色になる現象）」を解消し、絵の具本来の自然で鮮やかな発色を実現します。

本ライブラリは、用途や負荷要件に応じて選べる **2 つの独立したシェーダーエンジン** を提供します：

- **PolyMixer**: 4 次多項式回帰モデル。約 330 FMA の超低負荷・最速動作。リアルタイム 3D 描画向け。
- **SpectralMixer 16**: 16 バンド SIMD Kubelka-Munk 物理モデル。完全 Linear RGB（HDR）対応、任意多色調合、ピクセル平均化向け。

---

## エンジン選定ガイド

| 比較項目 | **PolyMixer** (最速) | **SpectralMixer 16** (物理・推奨) | 通常の加色 (RGB Lerp) |
| :--- | :--- | :--- | :--- |
| **数理モデル** | 4次多項式回帰 (Mixbox近似) | **16バンド単一定数 KM 物理モデル** | 線形補間 |
| **GPU コスト** | **約 330 FMA** (レジスタ極小) | **約 480 ALU** (float4x4 SIMD) | 1 ALU |
| **青＋黄 ($t=0.5$)** | **鮮やかなエメラルドグリーン** | **深みのあるフォレストグリーン** | **くすんだ黄土色〜灰** |
| **真値との色差** | $\Delta E \approx 2.07$ (vs Mixbox) | **$\Delta E \le 2.62$ (知覚限界未満)** | $\Delta E \approx 37 \sim 46$ (破綻) |
| **多色混色 ($N \ge 3$)** | △ カスケード 2 色混合 | **◎ 1パス同時混色・着色力指定** | × 単純加色平均 |
| **画素平均化 (縮小)** | △ 階層的 2x2 縮約 | **◎ アキュムレータ線形集約** | × 灰色化 (光学的平均) |
| **入出力色空間** | sRGB / Linear (外側変換) | **完全 Linear RGB (HDR ネイティブ)** | N/A |
| **テクスチャ / LUT** | **不要 (0 byte)** | **不要 (0 byte)** | 不要 |
| **推奨用途** | リアルタイム 3D / ゲームポストプロセス | **HDR 描画 / Mipmap 生成 / ペイントツール** | 明度フェード等 |

- **PolyMixer の選択基準**: 最大フレームレートを最優先し、2色の混色カーブ演出を最軽量に行いたい場合。
- **SpectralMixer 16 の選択基準**: 物理的な正確さ、3色以上の同時調合、テクスチャ縮小時の色維持、または HDR/Linear パイプラインを重視する場合。

---

## 1. PolyMixer (4次多項式近似エンジン)

[filament-mixer](https://github.com/justinh-rahb/filament-mixer) の多項式モデルを完全移植した超軽量エンジンです。LUT やテクスチャフェッチを一切使わず、ALU 演算（約 330 FMA）のみで Mixbox 近似の鮮やかな発色を実現します。

### 特徴
- **超低負荷**: テクスチャ帯域を消費せず、極小のレジスタ使用量で動作。
- **明快な 2 色補間**: 鮮やかなエメラルドグリーンなどの混色カーブを提供。

### HLSL での利用
```hlsl
#include "Assets/Shaders/PolyMixer.hlsl"

fixed4 frag(v2f i) : SV_Target {
    float3 colA = tex2D(_MainTex, i.uv).rgb;
    float3 colB = _PaintColor.rgb;
    float t = _BlendRatio; // 0.0 ~ 1.0

    // 青 + 黄 -> 鮮やかなエメラルドグリーン
    float3 mixed = PolyMixer_Lerp(colA, colB, t);
    return float4(mixed, 1.0);
}
```

### Shader Graph での利用
1. **Custom Function** ノードを作成。
2. **Type**: `File` を選び、`Assets/Shaders/PolyMixer.hlsl` を指定。
3. **Name**: `PolyMixer_Lerp`
4. **Inputs**: `c1` (Vector3), `c2` (Vector3), `t` (Float)
5. **Outputs**: `Out` (Vector3)

### C# スクリプトでの利用
```csharp
using UnityEngine;

Color blue = new Color(0f / 255f, 33f / 255f, 133f / 255f);
Color yellow = new Color(252f / 255f, 211f / 255f, 0f / 255f);

Color green = PolyMixer.Lerp(blue, yellow, 0.5f);
// green -> RGB(47, 141, 56) (鮮やかなエメラルドグリーン)
```

---

## 2. SpectralMixer 16 (16バンド分光物理エンジン)

[Spectral.js](https://github.com/rvanwijnen/spectral.js) の Kubelka-Munk 物理エンジンを 16 バンド SIMD（`float4` $\times 4$）へ低次元化した物理ベースエンジンです。

### 特徴
- **完全 Linear RGB 入出力**: Unity の HDR / Linear パイプラインに最適化。ガンマ変換オーバーヘッドなし。
- **知覚差ゼロの精度**: 38 次元物理真値に対する色差は平均 $\Delta E = 2.62$（識別限界 $\Delta E < 3.0$ 未満）。
- **純粋な ALU 演算（約 480 ALU）**: 反復ループや配列スピルを全廃し、`float4` ベクトル命令のみで展開。
- **物理ピクセル集約**: 吸収散乱比 $(K/S)$ の線形蓄積により、1 パスで $N$ 色調合や縮小平均を実行可能。

### HLSL: 2 色混色
```hlsl
#include "Assets/Shaders/SpectralMixer16.hlsl"

// Linear RGB の 2 色を比率 t で混色
float3 mixedLinear = Spectral16_Lerp(colA_Linear.rgb, colB_Linear.rgb, t);
```

### HLSL: 2x2 ピクセル縮小 (Mipmap / Box フィルタ)
```hlsl
#include "Assets/Shaders/SpectralMixer16.hlsl"

// 4テクセルを絵の具として調合し、彩度を落とさずに 1 テクセルへ縮小
float3 downsampled = Spectral16_Downsample2x2(c00, c10, c01, c11);
```

### HLSL: 任意ブロック集約 (Compute Shader / アキュムレータ)
```hlsl
#include "Assets/Shaders/SpectralMixer16.hlsl"

Spectral16_Accum acc = Spectral16_Accum_New();

// 任意の個数のピクセルを蓄積 (weight: フィルタ重み/面積, tint: 着色力)
Spectral16_Accum_Add(acc, pixelA_Linear, weightA);
Spectral16_Accum_Add(acc, pixelB_Linear, weightB);
Spectral16_Accum_Add(acc, pixelC_Linear, weightC);

// 最後に 1 回だけ解決 (重い KM 逆算と XYZ 積分は 1 回のみ実行)
float3 finalLinear = Spectral16_Accum_Resolve(acc);
```

### C# スクリプトでの利用
```csharp
using UnityEngine;

// 2色の Linear RGB 混色
Color mixed = SpectralMixer16.Lerp(colA, colB, 0.5f);

// 2x2 画素のダウンサンプリング
Color downsampled = SpectralMixer16.Downsample2x2(c00, c10, c01, c11);

// 任意 N 色の調合 (重み指定可能)
Color palette = SpectralMixer16.Mix(new Color[] { c1, c2, c3 }, new float[] { 1f, 2f, 0.5f });
```

---

## 実験結果・ベンチマーク

### 1. 2色混色ベンチマーク (Color Mixing Strip)

38次元物理真値、本ライブラリの `SpectralMixer 16`、4次多項式 `PolyMixer`、および通常の加色混合（Linear RGB Lerp）の混色カーブ比較です。

![Spectral16 Color Mixing Comparison](Assets/Artifacts/Spectral16_ColorMixing_Comparison.png)

- **加色混合 (Linear RGB Lerp)**: 青と黄の中間で彩度と明度が著しく低下し、灰色に落ち込みます。
- **PolyMixer**: 明度が高く鮮やかなエメラルドグリーンを形成します。
- **SpectralMixer 16**: 38次元真値と極めて近接した深みのあるフォレストグリーンを再現。色差は純青＋純黄で $\Delta E = 1.30$、青＋黄で $\Delta E = 2.07$、全体平均 $\Delta E = 2.62$ となり、知覚限界（$\Delta E < 3.0$）未満の精度を達成しています。

### 2. ピクセル平均化ベンチマーク (Downsampling 64x64 -> 1x1)

青＋黄などの市松模様テクスチャを $64 \times 64$ から $1 \times 1$ に縮小した際、絵の具の色が保持されるかの比較です。

![Spectral16 Downsampling Comparison](Assets/Artifacts/Spectral16_Downsampling_Comparison.png)

| 手法 | 青＋黄 縮小色 | 純青＋純黄 縮小色 | 赤＋シアン (補色) | 特徴・評価 |
| :--- | :--- | :--- | :--- | :--- |
| **[1] sRGB Box平均** | `RGB(126, 122, 66)`<br>$\Delta E = 37.83$ (黄土色) | `RGB(128, 128, 128)`<br>$\Delta E = 46.31$ (中間灰) | `RGB(128, 128, 128)`<br>$\Delta E = 29.05$ (無彩色) | ガンマ未補正の単純平均。最悪の濁り。 |
| **[2] Linear RGB平均** | `RGB(185, 156, 96)`<br>$\Delta E = 48.22$ (薄ベージュ) | `RGB(188, 188, 188)`<br>$\Delta E = 51.70$ (ライト灰) | `RGB(188, 188, 188)`<br>$\Delta E = 45.29$ (明灰) | 光の点描平均（標準Mipmap）。絵の具としては完全脱色。 |
| **[3] PolyMixer 階層縮約**| `RGB( 48, 142, 56)`<br>$\Delta E = 2.91$ (鮮緑) | `RGB( 63, 153, 103)`<br>$\Delta E = 6.88$ (青緑) | `RGB(140, 110, 106)`<br>$\Delta E = 16.08$ (薄土色) | 2x2トーナメント。ALUのみで高速だが独自カーブ。 |
| **[4] SpectralMixer 16** | `RGB( 65, 146, 64)`<br>**$\Delta E = 2.07$ (深緑)** | `RGB( 57, 142, 85)`<br>**$\Delta E = 1.30$ (緑)** | `RGB(136, 74, 74)`<br>**$\Delta E = 3.95$ (暗赤褐)** | **物理真値と完全一致。アキュムレータで線形集約。** |
| **[5] 38D KM (真値基準)** | `RGB( 61, 148, 62)`<br>$\Delta E = 0.00$ (基準深緑) | `RGB( 57, 143, 84)`<br>$\Delta E = 0.00$ (基準緑) | `RGB(128, 75, 75)`<br>$\Delta E = 0.00$ (基準赤褐) | 理論的真値。計算コスト高。 |

- **通常の平均化の破綻**:
  光の加法平均（加色）は「空間的に離れた光の合成」であるため、市松模様が均一な灰色・黄土色に脱色されます（$\Delta E \approx 30 \sim 50$）。
- **物理アキュムレータによる解決**:
  `SpectralMixer 16` は各テクセルを顔料の濃度比率 $(K/S)$ として蓄積し、最終ピクセルで一度だけスペクトル積分を行うため、物理的に正しい混色結果が得られます。

---

## ディレクトリ構成

```text
├── Assets/
│   ├── Artifacts/
│   │   ├── Spectral16_ColorMixing_Comparison.png # 16次元KM vs 38D vs PolyMixer 2色混色比較
│   │   ├── Spectral16_Downsampling_Comparison.png# 16次元KM vs 加色平均 画素平均化比較
│   │   ├── ColorMixing_Comparison.png            # 4手法の初期視覚的比較画像
│   │   └── Dimension_Downsampling_Comparison.png # 8D/12D/16D総合ベンチマーク比較画像
│   ├── Scripts/
│   │   ├── PolyMixer.cs                # 4次多項式 C# 実装
│   │   ├── SpectralMixer16.cs          # 16次元 KM C# 実装 (Linear RGB)
│   │   ├── SpectralMixerN.cs           # 汎用 K次元 (8D/12D/16D/38D) KM 実装
│   │   ├── SpectralMixer.cs            # Spectral.js (38波長 KM) C# 実装
│   │   ├── KubelkaMunk.cs              # 38波長 KM 物理リファレンス実装 (filament-mixer)
│   │   └── Editor/
│   │       ├── Spectral16ExperimentRunner.cs     # Spectral16 2実験画像生成エディタランナー
│   │       ├── DimensionAndDownsamplingCompareRunner.cs # 8D/12D/16D総合ベンチマークランナー
│   │       ├── DownsamplingCompareRunner.cs      # ダウンサンプリング比較エディタランナー
│   │       └── ColorMixCompareRunner.cs          # 4方式比較画像生成エディタランナー
│   └── Shaders/
│       ├── PolyMixer.hlsl              # ピグメント混色 HLSL シェーダー関数 (約330 FMA)
│       ├── SpectralMixer16.hlsl        # 16次元 KM HLSL シェーダー (float4x4 SIMD, Linear RGB)
│       ├── Spectral16_Constants.inc.hlsl # 16バンド定数テーブル
│       ├── SpectralMixer.hlsl          # 38波長 Spectral.js HLSL シェーダー関数
│       └── ColorMixCompare.compute     # 比較画像生成コンピュートシェーダー
├── Docs/
│   ├── POLYMIXER_USER_GUIDE.md         # 詳細ユーザーガイド & パフォーマンス
│   └── RESEARCH_AND_COMPARISON.md      # 調査・数理比較・KM法・Spectral.js検証記録
└── Tools/
    ├── generate_polymixer.ps1          # C++係数からHLSL/C#コードを生成するスクリプト
    └── filament_mixer_raw.h            # 原典のC++ヘッダー
```

---

## 詳細ドキュメント

- [**PolyMixer ユーザーガイド & パフォーマンスリファレンス**](Docs/POLYMIXER_USER_GUIDE.md)  
  API シグネチャ、Shader Graph 連携、GPU リソース詳細（FMA 演算量・帯域）、色空間の推奨事項。
- [**調査・比較・検証記録 (Technical Report)**](Docs/RESEARCH_AND_COMPARISON.md)  
  PolyMixer vs GPMixer の比較、Kubelka-Munk 物理法における色域制約の数理的分析、残差補正実験の経緯。

---

## クレジット・参考

- [justinh-rahb/filament-mixer](https://github.com/justinh-rahb/filament-mixer) (MIT License)
- [rvanwijnen/spectral.js](https://github.com/rvanwijnen/spectral.js) (MIT License)
- [Mixbox](https://github.com/scrtwpns/mixbox) (Secret Weapons / Sochorová & Jamriška, 2021)
