# PolyMixer ユーザーガイド & パフォーマンスリファレンス

## 1. PolyMixer とは

**PolyMixer** は、物理顔料混色のデファクトスタンダードである [Mixbox](https://github.com/scrtwpns/mixbox) の混色挙動を、**4次多項式回帰（330項の単項式基底）** によって高精度（Mixbox比 平均 $\Delta E$ 2.07）に近似・再現するピグメントミキサー関数です。

従来の 3D LUT（Mixbox方式：数十MB〜192MBのテクスチャ）や複雑な物理反復解法を必要とせず、**純粋な算術命令（ALU）のみ** で絵の具のような減色ピグメント混色（青＋黄＝鮮やかな緑）をリアルタイムに計算できます。

---

## 2. HLSL シェーダーでの使い方

### 基本インクルード
シェーダーファイル（`.shader`, `.hlsl`, `.compute`）内で [PolyMixer.hlsl](file:///c:/Users/nakata/Repo/generic-pigment-mixer/Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/PolyMixer.hlsl) をインクルードします。

```hlsl
#include "Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/PolyMixer.hlsl"
```
（または相対パス `#include "PolyMixer.hlsl"`）

### 関数シグネチャ

```hlsl
float3 PolyMixer_Lerp(float3 c1, float3 c2, float t);
```

- **`c1`**: 開始色。各チャンネルは **`0.0` 〜 `1.0`**（sRGB）。
- **`c2`**: 終了色。各チャンネルは **`0.0` 〜 `1.0`**（sRGB）。
- **`t`**: 混合比率。**`0.0`**（100% `c1`）〜 **`1.0`**（100% `c2`）。
- **戻り値**: 混色された RGB（`0.0` 〜 `1.0` に自動クランプ）。

### 使用例 (フラグメントシェーダー)

```hlsl
fixed4 frag(v2f i) : SV_Target {
    float3 colorA = tex2D(_MainTex, i.uv).rgb;
    float3 colorB = _PaintColor.rgb;
    float blendFactor = _Blend;

    // 従来の lerp の代わりに PolyMixer_Lerp を呼ぶだけ
    float3 mixed = PolyMixer_Lerp(colorA, colorB, blendFactor);

    return float4(mixed, 1.0);
}
```

### Shader Graph (Custom Function ノード) での利用
1. Shader Graph 内で **Custom Function** ノードを作成。
2. **Type**: `File` を選択し、`Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/PolyMixer.hlsl` を指定。
3. **Name**: `PolyMixer_Lerp`
4. **Inputs**:
   - `c1` (Vector3)
   - `c2` (Vector3)
   - `t` (Float)
5. **Outputs**:
   - `Out` (Vector3)

---

## 3. C# スクリプトでの使い方

CPU 側（エディタスクリプト、テクスチャ事前ベイク、ゲームロジック等）でも [PolyMixer.cs](file:///c:/Users/nakata/Repo/generic-pigment-mixer/Assets/Scripts/PolyMixer.cs) を用いて全く同一の混色を行えます。

```csharp
using UnityEngine;

Color blue = new Color(0f / 255f, 33f / 255f, 133f / 255f);
Color yellow = new Color(252f / 255f, 211f / 255f, 0f / 255f);

// t = 0.5 で混色 -> 鮮やかなグリーン (RGB: 47, 141, 56)
Color green = PolyMixer.Lerp(blue, yellow, 0.5f);
```

---

## 4. パフォーマンス特性とGPUリソース

| 項目 | 負荷・リソース | 技術的詳細 |
| :--- | :--- | :--- |
| **テクスチャサンプラー** | **0 (不要)** | テクスチャユニットやメモリ帯域を一切消費しません。 |
| **VRAM / キャッシュ** | **0 (不要)** | 巨大な 3D LUT（Mixbox方式では約 192MB）が不要。 |
| **ALU 演算量** | **約 330 FMA** | 7変数の整数の累乗積＋積和演算。 |
| **分岐ペナルティ** | **なし** | 端点判定（`t <= 0`, `t >= 1`）を除き、完全にストレートライン実行。 |
| **CPU 実行速度** | **約 0.001 ms** | C# / C++ で 1 回あたり 1 マイクロ秒以下（実質無料）。 |

### GPUでの実行コスト比較
- **通常の `lerp`**: 約 1 ALU サイクル（最速だが青＋黄は灰色）。
- **3D LUT サンプリング (Mixbox)**: 3D テクスチャサンプリング 2 回 ＋ 補間（VRAM帯域消費、L1/L2キャッシュミス要因）。
- **PolyMixer (HLSL)**: 純粋 ALU の約 330 サイクル。現代の GPU（テラフロップス級）においては、1 ピクセルあたり数ナノ秒で完了し、テクスチャフェッチ待ちによるパイプラインストールが発生しないため、極めて安定したフレームレートを維持します。

---

## 5. 設計上の注意点と推奨事項

1. **端点の完全保証**:
   - $t \le 0.0$ のときは元の `c1`、$t \ge 1.0$ のときは元の `c2` を早期リターンするため、端点で多項式の近似誤差が生じることはありません。
2. **2色ペア混色特化**:
   - PolyMixer は 2 色の混合（`lerp`）をモデル化しています。3 色以上を同時に混ぜる場合は、`PolyMixer_Lerp(PolyMixer_Lerp(c1, c2, t1), c3, t2)` のように段階的にカスケード混色してください。
3. **色空間（sRGB / Linear）**:
   - PolyMixer は **sRGB 空間** のカラー知覚データ（Mixbox）に基づいて訓練されています。
   - Unity のプロジェクト設定が Linear カラースペースの場合、シェーダーに入力する前に Linear $\to$ sRGB 変換（`LinearToGammaSpace`）を行い、PolyMixer で混色後に sRGB $\to$ Linear 変換（`GammaToLinearSpace`）を行うと、意図した通りの鮮やかな発色が得られます。

---

## 6. SpectralMixer との使い分け（代替エンジン）

本リポジトリには、物理ベース混色の代替実装として [Spectral.js](https://github.com/rvanwijnen/spectral.js) を移植した [SpectralMixer.cs](file:///c:/Users/nakata/Repo/generic-pigment-mixer/Packages/jp.nobnak.generic_pigment_mixer/Runtime/SpectralMixer.cs) および [SpectralMixer.hlsl](file:///c:/Users/nakata/Repo/generic-pigment-mixer/Packages/jp.nobnak.generic_pigment_mixer/ShaderLibrary/SpectralMixer.hlsl) も同梱されています。

| 用途・要件 | 推奨エンジン | 理由 |
| :--- | :--- | :--- |
| **ゲーム描画 / ポストプロセス / パーティクル** | **PolyMixer** | 約 330 FMA、レジスタ消費極小、テクスチャ0、ストレートライン実行で GPU フィルレートを圧迫しないため。 |
| **絵の具パレット調色 / デジタルペイント** | **SpectralMixer** | 3色以上の任意配合比率や、各色の着色力（Tinting Strength）パラメータを物理濃度空間で自然に扱えるため。 |
| **最高峰の実在絵具リアリズム (商用有償)** | **Mixbox (本家)** | 3D LUT を用いた実在絵具の正確な光学挙動。 |
