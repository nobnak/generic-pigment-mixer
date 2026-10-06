$header = Get-Content 'Assets/filament_mixer_raw.h' -Raw

# 1. Parse POWERS
$powersPattern = '(?s)POWERS\[330\]\[7\]\s*=\s*\{([^}]+(\{[^}]+\}[^}]+)*)\};'
if ($header -match $powersPattern) {
    $powersBlock = $Matches[1]
} else {
    throw "Failed to match POWERS"
}

$rowMatches = [regex]::Matches($powersBlock, '\{([0-9,\s]+)\}')
$powers = @()
foreach ($m in $rowMatches) {
    $parts = $m.Groups[1].Value.Split(',') | ForEach-Object { [int]($_.Trim()) }
    $powers += ,$parts
}

# 2. Parse COEF
$coefPattern = '(?s)COEF\[330\]\[3\]\s*=\s*\{([^;]+)\};'
if ($header -match $coefPattern) {
    $coefBlock = $Matches[1]
} else {
    throw "Failed to match COEF"
}

$coefRows = [regex]::Matches($coefBlock, '\{([^}]+)\}')
$coefs = @()
foreach ($m in $coefRows) {
    $parts = $m.Groups[1].Value.Split(',') | ForEach-Object { [double]($_.Trim()) }
    $coefs += ,$parts
}

# 3. Parse INTERCEPT
$interPattern = '(?s)INTERCEPT\[3\]\s*=\s*\{([^}]+)\};'
if ($header -match $interPattern) {
    $interBlock = $Matches[1]
} else {
    throw "Failed to match INTERCEPT"
}

$interParts = $interBlock.Split(',') | ForEach-Object { [double]($_.Trim()) }

Write-Output "Parsed $($powers.Count) powers, $($coefs.Count) coefs, $($interParts.Count) intercepts"

# 4. Generate PolyMixer.cs
New-Item -ItemType Directory -Force -Path 'Assets/Scripts' | Out-Null
$cs = [System.Text.StringBuilder]::new()
$cs.AppendLine("// Auto-generated PolyMixer C# implementation") | Out-Null
$cs.AppendLine("using System;") | Out-Null
$cs.AppendLine("using UnityEngine;") | Out-Null
$cs.AppendLine("") | Out-Null
$cs.AppendLine("public static class PolyMixer {") | Out-Null
$cs.AppendLine("    public static Color Lerp(Color c1, Color c2, float t) {") | Out-Null
$cs.AppendLine("        if (t <= 0f) return c1;") | Out-Null
$cs.AppendLine("        if (t >= 1f) return c2;") | Out-Null
$cs.AppendLine("") | Out-Null
$cs.AppendLine("        double r1 = c1.r * 255.0;") | Out-Null
$cs.AppendLine("        double g1 = c1.g * 255.0;") | Out-Null
$cs.AppendLine("        double b1 = c1.b * 255.0;") | Out-Null
$cs.AppendLine("        double r2 = c2.r * 255.0;") | Out-Null
$cs.AppendLine("        double g2 = c2.g * 255.0;") | Out-Null
$cs.AppendLine("        double b2 = c2.b * 255.0;") | Out-Null
$cs.AppendLine("        double dt = (double)t;") | Out-Null
$cs.AppendLine("") | Out-Null
$cs.AppendLine("        double[,] p = new double[7, 5];") | Out-Null
$cs.AppendLine("        double[] x = new double[] { r1, g1, b1, r2, g2, b2, dt };") | Out-Null
$cs.AppendLine("        for (int j = 0; j < 7; j++) {") | Out-Null
$cs.AppendLine("            p[j, 0] = 1.0;") | Out-Null
$cs.AppendLine("            p[j, 1] = x[j];") | Out-Null
$cs.AppendLine("            p[j, 2] = x[j] * x[j];") | Out-Null
$cs.AppendLine("            p[j, 3] = p[j, 2] * x[j];") | Out-Null
$cs.AppendLine("            p[j, 4] = p[j, 3] * x[j];") | Out-Null
$cs.AppendLine("        }") | Out-Null
$cs.AppendLine("") | Out-Null
$cs.AppendLine("        double outR = $($interParts[0].ToString('R'));") | Out-Null
$cs.AppendLine("        double outG = $($interParts[1].ToString('R'));") | Out-Null
$cs.AppendLine("        double outB = $($interParts[2].ToString('R'));") | Out-Null
$cs.AppendLine("        double f;") | Out-Null
$cs.AppendLine("") | Out-Null

for ($i = 0; $i -lt $powers.Count; $i++) {
    $terms = @()
    for ($j = 0; $j -lt 7; $j++) {
        if ($powers[$i][$j] -gt 0) {
            $terms += "p[$j, $($powers[$i][$j])]"
        }
    }
    $fExpr = if ($terms.Count -eq 0) { "1.0" } else { $terms -join " * " }
    $cs.AppendLine("        f = $fExpr;") | Out-Null
    $cs.AppendLine("        outR += f * $($coefs[$i][0].ToString('R'));") | Out-Null
    $cs.AppendLine("        outG += f * $($coefs[$i][1].ToString('R'));") | Out-Null
    $cs.AppendLine("        outB += f * $($coefs[$i][2].ToString('R'));") | Out-Null
}

$cs.AppendLine("") | Out-Null
$cs.AppendLine("        float r = Mathf.Clamp01((float)(outR / 255.0));") | Out-Null
$cs.AppendLine("        float g = Mathf.Clamp01((float)(outG / 255.0));") | Out-Null
$cs.AppendLine("        float b = Mathf.Clamp01((float)(outB / 255.0));") | Out-Null
$cs.AppendLine("        return new Color(r, g, b, 1f);") | Out-Null
$cs.AppendLine("    }") | Out-Null
$cs.AppendLine("}") | Out-Null

[System.IO.File]::WriteAllText('Assets/Scripts/PolyMixer.cs', $cs.ToString())
Write-Output "Written Assets/Scripts/PolyMixer.cs"

# 5. Generate PolyMixer.hlsl
New-Item -ItemType Directory -Force -Path 'Assets/Shaders' | Out-Null
$hlsl = [System.Text.StringBuilder]::new()
$hlsl.AppendLine("// Auto-generated PolyMixer HLSL implementation") | Out-Null
$hlsl.AppendLine("#ifndef POLY_MIXER_INCLUDED") | Out-Null
$hlsl.AppendLine("#define POLY_MIXER_INCLUDED") | Out-Null
$hlsl.AppendLine("") | Out-Null
$hlsl.AppendLine("float3 PolyMixer_Lerp(float3 c1, float3 c2, float t) {") | Out-Null
$hlsl.AppendLine("    if (t <= 0.0) return c1;") | Out-Null
$hlsl.AppendLine("    if (t >= 1.0) return c2;") | Out-Null
$hlsl.AppendLine("") | Out-Null
$hlsl.AppendLine("    float r1 = c1.r * 255.0;") | Out-Null
$hlsl.AppendLine("    float g1 = c1.g * 255.0;") | Out-Null
$hlsl.AppendLine("    float b1 = c1.b * 255.0;") | Out-Null
$hlsl.AppendLine("    float r2 = c2.r * 255.0;") | Out-Null
$hlsl.AppendLine("    float g2 = c2.g * 255.0;") | Out-Null
$hlsl.AppendLine("    float b2 = c2.b * 255.0;") | Out-Null
$hlsl.AppendLine("    float dt = t;") | Out-Null
$hlsl.AppendLine("") | Out-Null
$hlsl.AppendLine("    float x[7] = { r1, g1, b1, r2, g2, b2, dt };") | Out-Null
$hlsl.AppendLine("    float p[7][5];") | Out-Null
$hlsl.AppendLine("    [unroll]") | Out-Null
$hlsl.AppendLine("    for (int j = 0; j < 7; ++j) {") | Out-Null
$hlsl.AppendLine("        p[j][0] = 1.0;") | Out-Null
$hlsl.AppendLine("        p[j][1] = x[j];") | Out-Null
$hlsl.AppendLine("        p[j][2] = x[j] * x[j];") | Out-Null
$hlsl.AppendLine("        p[j][3] = p[j][2] * x[j];") | Out-Null
$hlsl.AppendLine("        p[j][4] = p[j][3] * x[j];") | Out-Null
$hlsl.AppendLine("    }") | Out-Null
$hlsl.AppendLine("") | Out-Null
$hlsl.AppendLine("    float3 rgb = float3($([float]$interParts[0])f, $([float]$interParts[1])f, $([float]$interParts[2])f);") | Out-Null
$hlsl.AppendLine("    float f;") | Out-Null
$hlsl.AppendLine("") | Out-Null

for ($i = 0; $i -lt $powers.Count; $i++) {
    $terms = @()
    for ($j = 0; $j -lt 7; $j++) {
        if ($powers[$i][$j] -gt 0) {
            $terms += "p[$j][$($powers[$i][$j])]"
        }
    }
    $fExpr = if ($terms.Count -eq 0) { "1.0" } else { $terms -join " * " }
    $hlsl.AppendLine("    f = $fExpr;") | Out-Null
    $hlsl.AppendLine("    rgb += f * float3($([float]$coefs[$i][0])f, $([float]$coefs[$i][1])f, $([float]$coefs[$i][2])f);") | Out-Null
}

$hlsl.AppendLine("") | Out-Null
$hlsl.AppendLine("    return saturate(rgb / 255.0);") | Out-Null
$hlsl.AppendLine("}") | Out-Null
$hlsl.AppendLine("") | Out-Null
$hlsl.AppendLine("#endif // POLY_MIXER_INCLUDED") | Out-Null

[System.IO.File]::WriteAllText('Assets/Shaders/PolyMixer.hlsl', $hlsl.ToString())
Write-Output "Written Assets/Shaders/PolyMixer.hlsl"
