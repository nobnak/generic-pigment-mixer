using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

public static class Spectral16TableExporter {
    [MenuItem("FilamentMixer/Export Spectral16 Constants")]
    public static void ExportConstants() {
        var profile = SpectralMixerN.D16;
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("// Auto-generated 16-band constants for SpectralMixer16");
        sb.AppendLine("// CMF16: float3 XYZ per band");

        // Format CMF16_X, Y, Z as 4 x float4
        sb.AppendLine("static const float4 CMF16_X[4] = {");
        for (int i = 0; i < 4; i++) {
            sb.AppendLine($"    float4({profile.Cmf[i*4+0].x:F14}f, {profile.Cmf[i*4+1].x:F14}f, {profile.Cmf[i*4+2].x:F14}f, {profile.Cmf[i*4+3].x:F14}f),");
        }
        sb.AppendLine("};");

        sb.AppendLine("static const float4 CMF16_Y[4] = {");
        for (int i = 0; i < 4; i++) {
            sb.AppendLine($"    float4({profile.Cmf[i*4+0].y:F14}f, {profile.Cmf[i*4+1].y:F14}f, {profile.Cmf[i*4+2].y:F14}f, {profile.Cmf[i*4+3].y:F14}f),");
        }
        sb.AppendLine("};");

        sb.AppendLine("static const float4 CMF16_Z[4] = {");
        for (int i = 0; i < 4; i++) {
            sb.AppendLine($"    float4({profile.Cmf[i*4+0].z:F14}f, {profile.Cmf[i*4+1].z:F14}f, {profile.Cmf[i*4+2].z:F14}f, {profile.Cmf[i*4+3].z:F14}f),");
        }
        sb.AppendLine("};");

        // Format BASES: 7 primaries (W, C, M, Y, R, G, B), each has 4 x float4
        string[] primaryNames = new string[] { "W", "C", "M", "Y", "R", "G", "B" };
        for (int k = 0; k < 7; k++) {
            sb.AppendLine($"static const float4 BASES16_{primaryNames[k]}[4] = {{");
            for (int i = 0; i < 4; i++) {
                sb.AppendLine($"    float4({profile.Bases[i*4+0, k]:F14}f, {profile.Bases[i*4+1, k]:F14}f, {profile.Bases[i*4+2, k]:F14}f, {profile.Bases[i*4+3, k]:F14}f),");
            }
            sb.AppendLine("};");
        }

        string outPath = Path.Combine(Application.dataPath, "Shaders/Spectral16_Constants.inc.hlsl");
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log($"Exported Spectral16 constants to: {outPath}");
    }
}
