#ifndef SPECTRAL_MIXER_16_HLSL
#define SPECTRAL_MIXER_16_HLSL

//  16-Band Reduced-Order Kubelka-Munk Spectral Pigment Mixer (HLSL)
//  Fully Linear RGB Input/Output for HDR & Modern Linear Pipeline.
//  Zero register-spills: Float4x4 SIMD vectorization (~480 ALU).

#include "Spectral16_Constants.inc.hlsl"

struct Spectral16 {
    float4 b0; // 380 - 460 nm
    float4 b1; // 470 - 550 nm
    float4 b2; // 560 - 630 nm
    float4 b3; // 640 - 750 nm
};

struct Spectral16_Accum {
    float4 ks0;
    float4 ks1;
    float4 ks2;
    float4 ks3;
    float  totalWeight;
};

// =================================================================
// Core Low-Level Vectorized KM Functions
// =================================================================

float4 Spectral16_KS_Vec(float4 r) {
    float4 oneMinusR = 1.0 - r;
    return (oneMinusR * oneMinusR) / (2.0 * max(1e-7, r));
}

float4 Spectral16_KM_Vec(float4 ks) {
    return 1.0 + ks - sqrt(ks * ks + 2.0 * ks);
}

Spectral16 Spectral16_FromLinear(float3 lrgb) {
    float w = min(lrgb.r, min(lrgb.g, lrgb.b));
    lrgb -= w;

    float c = min(lrgb.g, lrgb.b);
    float m = min(lrgb.r, lrgb.b);
    float y = min(lrgb.r, lrgb.g);

    float r = min(max(0.0, lrgb.r - lrgb.b), max(0.0, lrgb.r - lrgb.g));
    float g = min(max(0.0, lrgb.g - lrgb.b), max(0.0, lrgb.g - lrgb.r));
    float b = min(max(0.0, lrgb.b - lrgb.g), max(0.0, lrgb.b - lrgb.r));

    Spectral16 spec;
    spec.b0 = max(1e-7, w * BASES16_W[0] + c * BASES16_C[0] + m * BASES16_M[0] + y * BASES16_Y[0] + r * BASES16_R[0] + g * BASES16_G[0] + b * BASES16_B[0]);
    spec.b1 = max(1e-7, w * BASES16_W[1] + c * BASES16_C[1] + m * BASES16_M[1] + y * BASES16_Y[1] + r * BASES16_R[1] + g * BASES16_G[1] + b * BASES16_B[1]);
    spec.b2 = max(1e-7, w * BASES16_W[2] + c * BASES16_C[2] + m * BASES16_M[2] + y * BASES16_Y[2] + r * BASES16_R[2] + g * BASES16_G[2] + b * BASES16_B[2]);
    spec.b3 = max(1e-7, w * BASES16_W[3] + c * BASES16_C[3] + m * BASES16_M[3] + y * BASES16_Y[3] + r * BASES16_R[3] + g * BASES16_G[3] + b * BASES16_B[3]);
    return spec;
}

float3 Spectral16_ToXYZ(Spectral16 R) {
    float x = dot(R.b0, CMF16_X[0]) + dot(R.b1, CMF16_X[1]) + dot(R.b2, CMF16_X[2]) + dot(R.b3, CMF16_X[3]);
    float y = dot(R.b0, CMF16_Y[0]) + dot(R.b1, CMF16_Y[1]) + dot(R.b2, CMF16_Y[2]) + dot(R.b3, CMF16_Y[3]);
    float z = dot(R.b0, CMF16_Z[0]) + dot(R.b1, CMF16_Z[1]) + dot(R.b2, CMF16_Z[2]) + dot(R.b3, CMF16_Z[3]);
    return float3(x, y, z);
}

float3 Spectral16_XYZToLinear(float3 xyz) {
    float r = dot(float3( 3.2409699419045200, -1.537383177570090, -0.4986107602930030), xyz);
    float g = dot(float3(-0.9692436362808790,  1.875967501507720,  0.0415550574071756), xyz);
    float b = dot(float3( 0.0556300796969936, -0.203976958888976,  1.0569715142428700), xyz);
    return saturate(float3(r, g, b));
}

// =================================================================
// Accumulator API (Pixel Downsampling & Multi-Color Mixing)
// =================================================================

Spectral16_Accum Spectral16_Accum_New() {
    Spectral16_Accum acc;
    acc.ks0 = 0.0;
    acc.ks1 = 0.0;
    acc.ks2 = 0.0;
    acc.ks3 = 0.0;
    acc.totalWeight = 0.0;
    return acc;
}

void Spectral16_Accum_Add(inout Spectral16_Accum acc, float3 lrgb, float weight = 1.0, float tint = 1.0) {
    Spectral16 R = Spectral16_FromLinear(lrgb);
    float lum = dot(R.b0, CMF16_Y[0]) + dot(R.b1, CMF16_Y[1]) + dot(R.b2, CMF16_Y[2]) + dot(R.b3, CMF16_Y[3]);
    float conc = (weight * weight) * (tint * tint) * lum;

    acc.ks0 += Spectral16_KS_Vec(R.b0) * conc;
    acc.ks1 += Spectral16_KS_Vec(R.b1) * conc;
    acc.ks2 += Spectral16_KS_Vec(R.b2) * conc;
    acc.ks3 += Spectral16_KS_Vec(R.b3) * conc;
    acc.totalWeight += conc;
}

float3 Spectral16_Accum_Resolve(Spectral16_Accum acc) {
    if (acc.totalWeight <= 0.0) return float3(0.0, 0.0, 0.0);
    float invTotal = 1.0 / acc.totalWeight;

    Spectral16 R_mix;
    R_mix.b0 = Spectral16_KM_Vec(acc.ks0 * invTotal);
    R_mix.b1 = Spectral16_KM_Vec(acc.ks1 * invTotal);
    R_mix.b2 = Spectral16_KM_Vec(acc.ks2 * invTotal);
    R_mix.b3 = Spectral16_KM_Vec(acc.ks3 * invTotal);

    float3 xyz = Spectral16_ToXYZ(R_mix);
    return Spectral16_XYZToLinear(xyz);
}

// =================================================================
// High-Level Convenient Functions (Linear RGB In/Out)
// =================================================================

float3 Spectral16_Lerp(float3 lrgb1, float3 lrgb2, float t) {
    if (t <= 0.0) return lrgb1;
    if (t >= 1.0) return lrgb2;
    Spectral16_Accum acc = Spectral16_Accum_New();
    Spectral16_Accum_Add(acc, lrgb1, 1.0 - t);
    Spectral16_Accum_Add(acc, lrgb2, t);
    return Spectral16_Accum_Resolve(acc);
}

float3 Spectral16_Downsample2x2(float3 c00, float3 c10, float3 c01, float3 c11) {
    Spectral16_Accum acc = Spectral16_Accum_New();
    Spectral16_Accum_Add(acc, c00, 1.0);
    Spectral16_Accum_Add(acc, c10, 1.0);
    Spectral16_Accum_Add(acc, c01, 1.0);
    Spectral16_Accum_Add(acc, c11, 1.0);
    return Spectral16_Accum_Resolve(acc);
}

#endif // SPECTRAL_MIXER_16_HLSL
