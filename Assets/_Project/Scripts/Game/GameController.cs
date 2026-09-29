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
        public static readonly string[] SeatNames = { "You", "Karim", "Nour", "Salma" };

        /// <summary>Test hook: the local seat is driven by a bot, and popups dismiss themselves.</summary>
        public static bool LocalIsBot;
        /// <summary>Test hook: skip all animations and popups (headless full-speed game).</summary>
        public static bool SkipPresentation;
        /// <summary>Set by the Net layer when online play is enabled; null means single player.</summary>
        public static Func<MatchSetup> OnlineMatchFactory;
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

            while (true)
            {
                GameOverView.Choice choice = GameOverView.Choice.None;
                yield return PlayOneGame(c => choice = c);
                bool online = GameSettings.OnlineEnabled && OnlineMatchFactory != null;
                if ((choice == GameOverView.Choice.Menu || online) && Application.CanStreamedLevelBeLoaded(MenuSceneName))
                {
                    SceneManager.LoadScene(MenuSceneName);
                    yield break;
                }
                if (LocalIsBot) yield break;
            }
        }

        private IEnumerator PlayOneGame(Action<GameOverView.Choice> onChoice)
        {
            int seed = GameSettings.FixedSeed >= 0 ? GameSettings.FixedSeed : Environment.TickCount;
            MatchSetup setup = GameSettings.OnlineEnabled && OnlineMatchFactory != null ? OnlineMatchFactory() : BuildSinglePlayer(seed);
            var loop = new MatchLoop(setup, table);
            CurrentLoop = loop;
            setup.Attach?.Invoke(loop);
            int winner = -1;
            yield return loop.Run(SkipPresentation, LocalIsBot, onChoice);
            winner = loop.WinningTeam;
            GameFinished?.Invoke(winner, seed);
        }

        private MatchSetup BuildSinglePlayer(int seed)
        {
            var rules = new RulesConfig();
            var engine = new TarneebEngine(rules, seed);
            BotConfig botConfig = GameSettings.Difficulty == BotDifficulty.Easy ? BotConfig.Easy() : BotConfig.Normal();
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
