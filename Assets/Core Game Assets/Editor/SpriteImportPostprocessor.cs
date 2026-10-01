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
            if (!p.Contains("/Core Game Assets/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            bool music = p.Contains("/Audio/Music/");
            ai.forceToMono = false;                                 // SFX are already mono; music keeps its stereo image
            ai.loadInBackground = music;
            bool tiny = !music && new System.IO.FileInfo(p).Length < 20000;          // short clips: uncompressed (tiny Vorbis/ADPCM clips such as 'click' failed to create an FMOD sound)
            var s = new AudioImporterSampleSettings
            {
                loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
                compressionFormat = music ? AudioCompressionFormat.Vorbis : tiny ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM,
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
            if (p.Contains("/Core Game Assets/Resources/UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaIsTransparency = true;
                ti.spritePixelsPerUnit = 100f;
                ti.maxTextureSize = 2048;
                var st = new TextureImporterSettings();                       // full-rect meshes: Unity's "tight" polygon outline simplified round icons
                ti.ReadTextureSettings(st); st.spriteMeshType = SpriteMeshType.FullRect; ti.SetTextureSettings(st);
                Match m = Slice.Match(p);
                if (m.Success) { int b = int.Parse(m.Groups[1].Value); ti.spriteBorder = new Vector4(b, b, b, b); }
            }
            else if (p.Contains("/Core Game Assets/Art/UI/") && ti.textureType == TextureImporterType.Sprite)
            {
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st); st.spriteMeshType = SpriteMeshType.FullRect; ti.SetTextureSettings(st);
            }
            else if (p.Contains("/Core Game Assets/Resources/Cards/") || p.Contains("/Core Game Assets/Resources/Emoji/"))
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
