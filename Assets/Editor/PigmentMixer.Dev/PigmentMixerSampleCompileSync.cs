using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Nobnak.PigmentMixer.Dev.Editor {
    /// <summary>
    /// Dev-only: copies Assets/Samples/Comparison into the embedded package Samples~ for UPM publish.
    /// Not shipped in jp.nobnak.generic_pigment_mixer (lives under Assets/Editor in this repository).
    /// </summary>
    [InitializeOnLoad]
    static class PigmentMixerSampleCompileSync {
        static PigmentMixerSampleCompileSync() {
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.delayCall += () => PigmentMixerSampleSync.CopyToPackageSamples();
        }

        static void OnCompilationFinished(object _) {
            PigmentMixerSampleSync.CopyToPackageSamples();
        }

        [MenuItem("Tools/Pigment Mixer/Sync Samples to Package")]
        public static void SyncManual() {
            PigmentMixerSampleSync.CopyToPackageSamples();
        }
    }

    static class PigmentMixerSampleSync {
        const string PackageName = "jp.nobnak.generic_pigment_mixer";
        const string SampleFolder = "Comparison";

        internal static void CopyToPackageSamples() {
            var src = Path.Combine(Application.dataPath, "Samples", SampleFolder);
            var packageRoot = Path.GetFullPath(Path.Combine("Packages", PackageName));
            if (!Directory.Exists(packageRoot) || !Directory.Exists(src)) return;

            var dstRoot = Path.Combine(packageRoot, "Samples~");
            var dst = Path.Combine(dstRoot, SampleFolder);

            if (Directory.Exists(dstRoot)) {
                Directory.Delete(dstRoot, recursive: true);
            }

            CopyDirectory(src, dst);
            Debug.Log($"[PigmentMixer.Dev] Synced samples: {src} -> {dst}");
        }

        static void CopyDirectory(string src, string dst) {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src)) {
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
            }
            foreach (var dir in Directory.GetDirectories(src)) {
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
            }
        }
    }
}
