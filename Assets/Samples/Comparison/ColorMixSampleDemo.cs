using UnityEngine;

namespace Nobnak.PigmentMixer.Samples {
    /// <summary>
    /// Sample MonoBehaviour demonstrating runtime pigment mixing with PolyMixer and SpectralMixer16.
    /// </summary>
    public class ColorMixSampleDemo : MonoBehaviour {
        [Header("Inputs (sRGB)")]
        [SerializeField] private Color colorA = new Color(0f / 255f, 33f / 255f, 133f / 255f);
        [SerializeField] private Color colorB = new Color(252f / 255f, 211f / 255f, 0f / 255f);
        [Range(0f, 1f)] [SerializeField] private float blendRatio = 0.5f;

        [Header("Mixed Outputs (Read-only preview)")]
        [SerializeField] private Color polyMixed;
        [SerializeField] private Color spectral16Mixed;

        public Color ColorA {
            get => colorA;
            set => colorA = value;
        }

        public Color ColorB {
            get => colorB;
            set => colorB = value;
        }

        public float BlendRatio {
            get => blendRatio;
            set => blendRatio = Mathf.Clamp01(value);
        }

        public Color PolyMixedColor => PolyMixer.Lerp(colorA, colorB, blendRatio);
        public Color SpectralMixedColor => SpectralMixer16.Lerp(colorA, colorB, blendRatio);

        private void Update() {
            polyMixed = PolyMixedColor;
            spectral16Mixed = SpectralMixedColor;
        }
    }
}
