using UnityEngine;

namespace Tarneeb.Config
{
    public enum BotDifficulty { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>Player-facing settings persisted in PlayerPrefs, plus the session mode chosen in the menu.</summary>
    public static class GameSettings
    {
        private const string DifficultyKey = "tarneeb.difficulty";
        private const string SoundKey = "tarneeb.sound";
        private const string SpeedKey = "tarneeb.speed";
        private const string MusicKey = "tarneeb.music";
        private const string MusicVolKey = "tarneeb.musicvol";
        private const string SfxVolKey = "tarneeb.sfxvol";

        /// <summary>Kill switch: set to false to hide online play even when Photon is installed (see Net/README).</summary>
        public const bool OnlineAllowed = true;

        /// <summary>True when a matchmaking backend (PUN 2) is compiled into this build.</summary>
        public static bool OnlineEnabled => OnlineAllowed && Game.LobbyProvider.Available;

        public static BotDifficulty Difficulty
        {
            get => (BotDifficulty)PlayerPrefs.GetInt(DifficultyKey, (int)BotDifficulty.Normal);
            set
            {
                bool changed = value != Difficulty;
                PlayerPrefs.SetInt(DifficultyKey, (int)value); PlayerPrefs.Save();
                if (changed) DifficultyChanged?.Invoke();
            }
        }

        /// <summary>Raised when the bot skill is changed (Settings, also in the middle of a game): running bots switch to it.</summary>
        public static event System.Action DifficultyChanged;

        /// <summary>The bot configuration for the skill chosen in Settings.</summary>
        public static Core.Bots.BotConfig CurrentBotConfig()
        {
            switch (Difficulty)
            {
                case BotDifficulty.Easy: return Core.Bots.BotConfig.Easy();
                case BotDifficulty.Hard: return Core.Bots.BotConfig.Hard();
                default: return Core.Bots.BotConfig.Normal();
            }
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>0..1, default 0.6.</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolKey, 0.6f);
            set { PlayerPrefs.SetFloat(MusicVolKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        /// <summary>0..1, default 1.</summary>
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolKey, 1f);
            set { PlayerPrefs.SetFloat(SfxVolKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
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
