using System;
using System.Collections;
using Tarneeb.Config;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tarneeb.Game
{
    /// <summary>Scene entry point: builds a match (single player by default), runs it, and handles Play Again / Menu.</summary>
    public sealed class GameController : MonoBehaviour
    {
        public const string MenuSceneName = "Menu";
        /// <summary>Names by engine seat for a local player in seat 0: the Figma cast (Ahmad at the bottom, Khalid right, Omar across, Sara left).</summary>
        public static readonly string[] SeatNames = { "Ahmad", "Khalid", "Omar", "Sara" };

        /// <summary>Test hook: the local seat is driven by a bot, and popups dismiss themselves.</summary>
        public static bool LocalIsBot;
        /// <summary>Test hook: skip all animations and popups (headless full-speed game).</summary>
        public static bool SkipPresentation;
        /// <summary>Set by the Net layer when online play is enabled; null means single player.</summary>
        public static Func<MatchSetup> OnlineMatchFactory;
        /// <summary>Set by the lobby when the master starts a room; the next Game scene plays online instead of against bots.</summary>
        public static bool OnlineRequested;
        /// <summary>Raised once per finished game with (winningTeam, seed).</summary>
        public static event Action<int, int> GameFinished;

        [SerializeField] private TableView table;
        [SerializeField] private int localSeat = 0;

        public MatchLoop CurrentLoop { get; private set; }

        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            Tween.SpeedMultiplier = GameSettings.GameSpeed;
            if (table == null) table = GetComponentInChildren<TableView>(true);
            if (table == null) table = FindFirstObjectByType<TableView>();
            ChatService.Reset();

            if (table != null)
            {
                var pause = table.GetComponentInChildren<PauseMenu>(true);
                Transform menu = table.transform.Find("HUD/Header/MenuButton");
                if (menu != null && pause != null) menu.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(pause.Open);
                Transform settings = table.transform.Find("HUD/Header/SettingsButton");
                var panel = table.GetComponentInChildren<SettingsPanel>(true);
                if (settings != null && panel != null) settings.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(panel.Open);
            }

            while (true)
            {
                GameOverView.Choice choice = GameOverView.Choice.None;
                yield return PlayOneGame(c => choice = c);
                bool online = OnlineRequested && OnlineMatchFactory != null;
                if ((choice == GameOverView.Choice.Menu || online) && Application.CanStreamedLevelBeLoaded(MenuSceneName))
                {
                    ScreenFader.Go(MenuSceneName);
                    yield break;
                }
                if (LocalIsBot) yield break;
            }
        }

        private IEnumerator PlayOneGame(Action<GameOverView.Choice> onChoice)
        {
            int seed = GameSettings.FixedSeed >= 0 ? GameSettings.FixedSeed : Environment.TickCount;
            MatchSetup setup = OnlineRequested && OnlineMatchFactory != null ? OnlineMatchFactory() : BuildSinglePlayer(seed);
            var loop = new MatchLoop(setup, table);
            CurrentLoop = loop;
            ChatService.LocalSeat = setup.LocalSeat;
            if (ChatService.Outgoing == null && !LocalIsBot)
            {
                BotChatter chatter = GetComponent<BotChatter>();
                if (chatter == null) chatter = gameObject.AddComponent<BotChatter>();
                chatter.Init(setup.LocalSeat, seed);
            }
            setup.Attach?.Invoke(loop);
            if (!setup.Online)                                                   // Settings can change the bot skill mid-game: running bots follow it
            {
                _botSeats = setup.Controllers;
                GameSettings.DifficultyChanged -= OnDifficultyChanged;
                GameSettings.DifficultyChanged += OnDifficultyChanged;
            }
            loop.Completed += winner => GameFinished?.Invoke(winner, seed);
            yield return loop.Run(SkipPresentation, LocalIsBot, onChoice);
        }

        private SeatController[] _botSeats;

        private void OnDifficultyChanged()
        {
            if (_botSeats == null) return;
            BotConfig cfg = GameSettings.CurrentBotConfig();
            foreach (SeatController c in _botSeats) if (c is BotController b) b.Reconfigure(cfg);
        }

        private void OnDestroy()
        {
            GameSettings.DifficultyChanged -= OnDifficultyChanged;
            ChatService.Reset();
            if (!OnlineRequested) return;
            OnlineRequested = false;                 // leaving the table: drop out of the room too
            LobbyProvider.Current?.Leave();
        }

        private MatchSetup BuildSinglePlayer(int seed)
        {
            var rules = new RulesConfig();
            GameSettings.TurnTimerSeconds = 0;       // an earlier online game may have set the human turn timer
            var engine = new TarneebEngine(rules, seed);
            BotConfig botConfig = GameSettings.CurrentBotConfig();
            var controllers = new SeatController[4];
            for (int s = 0; s < 4; s++)
            {
                if (s == localSeat && !LocalIsBot) controllers[s] = new LocalHumanController(s, seed + s);
                else controllers[s] = new BotController(s, botConfig, seed + s * 101, !SkipPresentation);
            }
            return new MatchSetup
            {
                Engine = engine,
                Controllers = controllers,
                LocalSeat = localSeat,
                SeatNames = SeatNames,
                Start = () => { engine.StartGame(); return null; }
            };
        }
    }
}
