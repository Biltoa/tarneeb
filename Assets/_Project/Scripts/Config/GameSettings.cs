using UnityEngine;

namespace Tarneeb.Config
{
    public enum BotDifficulty { Easy = 0, Normal = 1 }

    /// <summary>Player-facing settings persisted in PlayerPrefs, plus the session mode chosen in the menu.</summary>
    public static class GameSettings
    {
        private const string DifficultyKey = "tarneeb.difficulty";
        private const string SoundKey = "tarneeb.sound";
        private const string SpeedKey = "tarneeb.speed";

        /// <summary>Kill switch: set to false to hide online play even when Photon is installed (see Net/README).</summary>
        public const bool OnlineAllowed = true;

        /// <summary>True when a matchmaking backend (PUN 2) is compiled into this build.</summary>
        public static bool OnlineEnabled => OnlineAllowed && Game.LobbyProvider.Available;

        public static BotDifficulty Difficulty
        {
            get => (BotDifficulty)PlayerPrefs.GetInt(DifficultyKey, (int)BotDifficulty.Normal);
            set { PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save(); }
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>1 = normal, 2 = fast. Scales bot think time and animations.</summary>
        public static float GameSpeed
        {
            get => PlayerPrefs.GetFloat(SpeedKey, 1f);
            set { PlayerPrefs.SetFloat(SpeedKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>Seconds a human has per turn; 0 disables the timer (single player).</summary>
        public static int TurnTimerSeconds = 0;

        /// <summary>Optional fixed seed for reproducible games (debug); -1 = random.</summary>
        public static int FixedSeed = -1;
    }
}
