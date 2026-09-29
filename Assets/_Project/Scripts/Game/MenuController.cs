using Tarneeb.Config;
using Tarneeb.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tarneeb.Game
{
    /// <summary>Menu scene. Children: PlayButton, OnlineButton, DifficultyButton, SpeedButton, SoundButton (each with a Label).</summary>
    public sealed class MenuController : MonoBehaviour
    {
        public const string GameSceneName = "Game";

        private TMP_Text _difficulty, _speed, _sound, _online;

        private void Awake()
        {
            Tween.SpeedMultiplier = 1f;
            UiBind.Get<Button>(transform, "PlayButton").onClick.AddListener(() => { AudioManager.Play(Sfx.Click); SceneManager.LoadScene(GameSceneName); });

            Button online = UiBind.Get<Button>(transform, "OnlineButton");
            _online = UiBind.Get<TMP_Text>(transform, "OnlineButton/Label");
            online.interactable = GameSettings.OnlineEnabled;
            _online.text = GameSettings.OnlineEnabled ? "Play Online" : "Play Online (coming soon)";

            _difficulty = UiBind.Get<TMP_Text>(transform, "DifficultyButton/Label");
            UiBind.Get<Button>(transform, "DifficultyButton").onClick.AddListener(() =>
            {
                GameSettings.Difficulty = GameSettings.Difficulty == BotDifficulty.Easy ? BotDifficulty.Normal : BotDifficulty.Easy;
                Refresh();
            });

            _speed = UiBind.Get<TMP_Text>(transform, "SpeedButton/Label");
            UiBind.Get<Button>(transform, "SpeedButton").onClick.AddListener(() =>
            {
                GameSettings.GameSpeed = GameSettings.GameSpeed > 1.5f ? 1f : 2f;
                Refresh();
            });

            _sound = UiBind.Get<TMP_Text>(transform, "SoundButton/Label");
            UiBind.Get<Button>(transform, "SoundButton").onClick.AddListener(() =>
            {
                GameSettings.SoundOn = !GameSettings.SoundOn;
                Refresh();
                AudioManager.Play(Sfx.Click);
            });
            Refresh();
        }

        private void Refresh()
        {
            _difficulty.text = "Bots: " + (GameSettings.Difficulty == BotDifficulty.Easy ? "Easy" : "Normal");
            _speed.text = "Speed: " + (GameSettings.GameSpeed > 1.5f ? "Fast" : "Normal");
            _sound.text = "Sound: " + (GameSettings.SoundOn ? "On" : "Off");
        }
    }
}
