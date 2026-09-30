#if UNITY_EDITOR
using Tarneeb.Presentation;
using UnityEditor;
using UnityEngine;

namespace Tarneeb.EditorTools
{
    /// <summary>Shared scene-build helpers (audio wiring) used by the Game and Menu assemblers.</summary>
    public static class EditorSceneHelpers
    {
        private static AudioClip Clip(string folder, string name, string ext) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/" + folder + "/" + name + "." + ext);

        /// <summary>Creates the "Audio" object with an AudioManager whose clips follow the Sfx enum order.</summary>
        public static void EnsureAudio()
        {
            var old = GameObject.Find("Audio");
            if (old != null) Object.DestroyImmediate(old);
            var go = new GameObject("Audio");
            var am = go.AddComponent<AudioManager>();
            string[] names = { "card_flip", "card_place", "trick_win", "round_win", "round_lose", "click", "card_deal", "card_slide", "whoosh", "bid",
                "pass", "turn_ping", "timer_tick", "deny", "pop", "sparkle", "game_win", "game_lose", "coin", "toggle_on", "toggle_off" };
            System.Array values = System.Enum.GetValues(typeof(Sfx));
            if (values.Length != names.Length) Debug.LogError("[Audio] Sfx enum has " + values.Length + " entries but " + names.Length + " clips are mapped");
            am.sfxClips = new AudioClip[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                am.sfxClips[i] = Clip("Sfx", names[i], "wav");
                if (am.sfxClips[i] == null) Debug.LogError("[Audio] missing Sfx clip " + names[i]);
            }
            am.menuMusic = Clip("Music", "menu_theme", "ogg");
            am.tableMusic = Clip("Music", "table_theme", "ogg");
            if (am.menuMusic == null || am.tableMusic == null) Debug.LogError("[Audio] missing music clip");
            EditorUtility.SetDirty(am);
        }
    }
}
#endif
