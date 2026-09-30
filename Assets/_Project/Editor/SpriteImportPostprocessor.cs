#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Tarneeb.EditorTools
{
    /// <summary>
    /// Import defaults for generated art so files dropped in by Tools/gen_*.py need no manual setup:
    /// Resources/UI PNGs become sprites (a "_9s48" suffix sets a 48 px 9-slice border); Resources/Cards and Resources/Emoji are
    /// runtime atlases (no mipmaps, clamped, no NPOT scaling).
    /// </summary>
    public sealed class SpriteImportPostprocessor : AssetPostprocessor
    {
        private static readonly Regex Slice = new Regex(@"_9s(\d+)\.png$");

        private void OnPreprocessAudio()
        {
            string p = assetPath;
            if (!p.Contains("/_Project/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            bool music = p.Contains("/Audio/Music/");
            ai.forceToMono = false;                                 // SFX are already mono; music keeps its stereo image
            ai.loadInBackground = music;
            var s = new AudioImporterSampleSettings
            {
                loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = music ? 0.45f : 0.6f,
                sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
                preloadAudioData = false
            };
            ai.defaultSampleSettings = s;
            ai.SetOverrideSampleSettings("WebGL", s);
        }

        private void OnPreprocessTexture()
        {
            string p = assetPath;
            var ti = (TextureImporter)assetImporter;
            if (p.Contains("/_Project/Resources/UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.spritePixelsPerUnit = 100f;
                ti.maxTextureSize = 2048;
                Match m = Slice.Match(p);
                if (m.Success) { int b = int.Parse(m.Groups[1].Value); ti.spriteBorder = new Vector4(b, b, b, b); }
            }
            else if (p.Contains("/_Project/Resources/Cards/") || p.Contains("/_Project/Resources/Emoji/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 2048;
            }
        }
    }
}
#endif
