using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Config;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tarneeb.Game
{
    /// <summary>
    /// Runs a game: owns the engine and seat controllers, queues engine events for the table to present in order,
    /// and only asks a seat to act once all pending animations have finished.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public const string MenuSceneName = "Menu";
        public static readonly string[] SeatNames = { "You", "Karim", "Nour", "Salma" };

        /// <summary>Test hook: the local seat is driven by a bot, and popups dismiss themselves.</summary>
        public static bool LocalIsBot;
        /// <summary>Test hook: skip all animations and popups (headless full-speed game).</summary>
        public static bool SkipPresentation;
        /// <summary>Raised once per finished game with (winningTeam, seed).</summary>
        public static event Action<int, int> GameFinished;

        [SerializeField] private TableView table;
        [SerializeField] private int localSeat = 0;

        private TarneebEngine _engine;
        private SeatController[] _controllers;
        private readonly Queue<GameEvent> _queue = new Queue<GameEvent>();

        public TarneebEngine Engine => _engine;

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
                if (choice == GameOverView.Choice.Menu && Application.CanStreamedLevelBeLoaded(MenuSceneName))
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
            var rules = new RulesConfig();
            _engine = new TarneebEngine(rules, seed);
            BuildControllers(seed);
            table.BeginGame(_engine, new SeatMapper(localSeat, rules.Counterclockwise), SeatNames);

            _queue.Clear();
            _engine.EventEmitted += OnEngineEvent;
            _engine.StartGame();

            while (true)
            {
                yield return DrainQueue();
                GamePhase phase = _engine.State.Phase;

                if (phase == GamePhase.RoundOver || phase == GamePhase.GameOver)
                {
                    bool over = phase == GamePhase.GameOver;
                    if (!SkipPresentation) yield return table.ShowRoundSummary(_engine.State.LastRound, over, LocalIsBot ? 2.5f : 0f);
                    if (over) break;
                    _engine.StartNextRound();
                    continue;
                }

                SeatController controller = _controllers[_engine.State.CurrentSeat];
                PlayerCommand command = default;
                bool submitted = false;
                yield return controller.Act(_engine, table, c => { command = c; submitted = true; });
                if (!submitted) continue;
                Apply(command, controller.Seat);
            }

            _engine.EventEmitted -= OnEngineEvent;
            int winner = _engine.State.WinningTeam;
            GameFinished?.Invoke(winner, seed);

            GameOverView.Choice choice = GameOverView.Choice.PlayAgain;
            if (!SkipPresentation)
                yield return table.ShowGameOver(winner, _engine.State.TeamScores, c => choice = c, LocalIsBot ? 2.5f : 0f);
            onChoice(choice);
        }

        private void BuildControllers(int seed)
        {
            _controllers = new SeatController[4];
            BotConfig botConfig = GameSettings.Difficulty == BotDifficulty.Easy ? BotConfig.Easy() : BotConfig.Normal();
            for (int s = 0; s < 4; s++)
            {
                if (s == localSeat && !LocalIsBot) _controllers[s] = new LocalHumanController(s, seed + s);
                else _controllers[s] = new BotController(s, botConfig, seed + s * 101, !SkipPresentation);
            }
        }

        private void OnEngineEvent(GameEvent e)
        {
            _queue.Enqueue(e);
            for (int i = 0; i < _controllers.Length; i++) _controllers[i].Observe(e);
        }

        private IEnumerator DrainQueue()
        {
            while (_queue.Count > 0)
            {
                GameEvent e = _queue.Dequeue();
                if (SkipPresentation) continue;
                IEnumerator routine = table.Present(e);
                if (routine != null) yield return routine;
            }
        }

        private void Apply(PlayerCommand c, int seat)
        {
            CommandResult r;
            switch (c.Kind)
            {
                case CommandKind.Bid: r = _engine.PlaceBid(seat, c.Amount); break;
                case CommandKind.Pass: r = _engine.Pass(seat); break;
                case CommandKind.Trump: r = _engine.ChooseTrump(seat, c.Suit); break;
                default: r = _engine.PlayCard(seat, c.Card); break;
            }
            if (!r.Success) Debug.LogWarning("[Game] Seat " + seat + " command " + c.Kind + " rejected: " + r);
        }
    }
}
