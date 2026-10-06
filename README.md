# Generic Pigment Mixer for Unity

Unity 向けのリアルタイム・物理顔料混色（ピグメントミキサー）ライブラリです。  
[justinh-rahb/filament-mixer](https://github.com/justinh-rahb/filament-mixer) で開発された 4次多項式モデル（**PolyMixer**）を HLSL シェーダーおよび C# に完全移植し、従来のデジタル加色・減色補間で発生していた「色の濁り（青＋黄が灰色になる現象）」を解消します。

---

## 混色手法の比較

従来の加色混合（sRGB Lerp）、減色混合（CMYK Lerp）、本プロジェクトの **PolyMixer (HLSL)**、および物理ベースの **Spectral.js (Kubelka-Munk)** の直接比較です。

![Color Mixing Comparison](Assets/Artifacts/ColorMixing_Comparison.png)

| 混色手法 | 青＋黄（$t=0.5$）の挙動 | GPU負荷 / メモリ | 特徴・用途 |
| :--- | :--- | :--- | :--- |
| **[1] 加色混合 (RGB Lerp)** | **くすんだ黄土色〜グレー** | 1 ALU | 光の混色。デジタル絵の具・テクスチャブレンドには不適。 |
| **[2] 減色混合 (CMYK Lerp)** | **暗い青灰色〜グレー** | 数 ALU | インク吸光度モデル。青＋黄は黒沈み・灰色化する。 |
| **[3] PolyMixer (HLSL)** | **鮮やかなエメラルドグリーン** | **約 330 FMA (テクスチャ0)** | **Mixbox近似のピグメント混色。リアルタイム描画に最適。** |
| **[4] Spectral.js (KM)** | **ディープエメラルドグリーン** | **1,000+ ALU (38反復ループ)** | **38波長単一定数KM物理モデル。多色混色・着色力指定に対応。** |

---

## 主な特徴

- **青＋黄＝緑になる自然なピグメント混色**:
  - デジタルペイント、プロシージャルテクスチャ、スプラトゥーン風のインク混色、マテリアルブレンドに最適。
- **テクスチャサンプラー・3D LUT 完全不要**:
  - 本家 Mixbox のような巨大な 3D LUT テクスチャ（数十MB〜192MB）や VRAM 帯域を一切消費しません。
- **純粋な ALU 演算（約 330 FMA）**:
  - 単項式基底と係数テーブルを展開したストレートライン計算により、ピクセルシェーダー上で数ナノ秒で完了します。
- **C# / HLSL 両対応**:
  - シェーダーだけでなく、CPU 側（テクスチャ事前ベイクやゲームロジック等）でも全く同一の混色結果が得られます。
- **Spectral.js 実装も同梱**:
  - 3色以上の同時調合や着色力（Tinting Strength）パラメータを扱いたい用途向けに、38波長 Kubelka-Munk 物理エンジン（`SpectralMixer.cs` / `SpectralMixer.hlsl`）も収録。

---

## PolyMixer vs Spectral.js の使い分け

| 項目 | PolyMixer (推奨) | Spectral.js |
| :--- | :--- | :--- |
| **主な用途** | リアルタイム 3D / ポストプロセス / ゲーム描画 | パレット調合ツール / デジタルペイント / 物理実験 |
| **シェーダー適性** | **◎ 最適**（低レジスタ圧・ループなし） | **△ 注意**（38ループ・配列による高レジスタ圧） |
| **混合の柔軟性** | 2色ペア補間（$t \in [0, 1]$） | **任意 N 色の同時混色 ＋ 着色力 ($T$) 指定** |
| **端点色保証** | **完全保証**（$t=0 \implies C_1, t=1 \implies C_2$） | 近似（RGB $\to$ スペクトル $\to$ RGB で微小誤差） |
| **ライセンス** | **MIT License** | **MIT License** |

---

## クイックスタート

### 1. HLSL シェーダーでの利用

任意の `.shader` や `.hlsl` でインクルードし、`PolyMixer_Lerp` を呼び出します。

```hlsl
#include "Assets/Shaders/PolyMixer.hlsl"

fixed4 frag(v2f i) : SV_Target {
    float3 colA = tex2D(_MainTex, i.uv).rgb;
    float3 colB = _PaintColor.rgb;
    float t = _BlendRatio; // 0.0 ~ 1.0

    // 従来の lerp の代わりに呼ぶだけ (青+黄 -> 鮮やかな緑)
    float3 mixed = PolyMixer_Lerp(colA, colB, t);

    return float4(mixed, 1.0);
}
```

### 2. Shader Graph での利用
1. **Custom Function** ノードを作成。
2. **Type**: `File` を選び、`Assets/Shaders/PolyMixer.hlsl` を指定。
3. **Name**: `PolyMixer_Lerp`
4. **Inputs**: `c1` (Vector3), `c2` (Vector3), `t` (Float)
5. **Outputs**: `Out` (Vector3)

### 3. C# スクリプトでの利用

```csharp
using UnityEngine;

// 青 (0, 33, 133) と 黄 (252, 211, 0) を 50% で混色
Color blue = new Color(0f / 255f, 33f / 255f, 133f / 255f);
Color yellow = new Color(252f / 255f, 211f / 255f, 0f / 255f);

Color green = PolyMixer.Lerp(blue, yellow, 0.5f);
// green -> RGB(47, 141, 56) (鮮やかなグリーン)
```

---

## ディレクトリ構成

```text
├── Assets/
│   ├── Artifacts/
│   │   └── ColorMixing_Comparison.png  # 4手法の視覚的比較画像 (RGB/CMYK/PolyMixer/Spectral)
│   ├── Scripts/
│   │   ├── PolyMixer.cs                # 4次多項式 C# 実装
│   │   ├── SpectralMixer.cs            # Spectral.js (38波長 KM) C# 実装
│   │   ├── KubelkaMunk.cs              # 38波長 KM 物理リファレンス実装 (filament-mixer)
│   │   └── Editor/
│   │       └── ColorMixCompareRunner.cs # 4方式比較画像生成エディタランナー
│   └── Shaders/
│       ├── PolyMixer.hlsl              # ピグメント混色 HLSL シェーダー関数 (本採用)
│       ├── SpectralMixer.hlsl          # Spectral.js HLSL シェーダー関数
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

より詳しい実装仕様や技術的背景は以下のドキュメントを参照してください：

- [**PolyMixer ユーザーガイド & パフォーマンスリファレンス**](Docs/POLYMIXER_USER_GUIDE.md)  
  API シグネチャ、Shader Graph 連携、GPU リソース詳細（FMA 演算量・帯域）、色空間（sRGB / Linear）の推奨事項。
- [**調査・比較・検証記録 (Technical Report)**](Docs/RESEARCH_AND_COMPARISON.md)  
  PolyMixer vs GPMixer の比較、Kubelka-Munk 物理法における色域制約の数理的分析、残差補正実験の経緯。

---

## クレジット・参考

- [justinh-rahb/filament-mixer](https://github.com/justinh-rahb/filament-mixer) (MIT License)
- [Mixbox](https://github.com/scrtwpns/mixbox) (Secret Weapons / Sochorová & Jamriška, 2021)
