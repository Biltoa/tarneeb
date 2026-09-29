using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Presentation;
using UnityEngine;

namespace Tarneeb.Game
{
    /// <summary>Everything a match needs, so single player, host and client all run through the same loop.</summary>
    public sealed class MatchSetup
    {
        public TarneebEngine Engine;
        public SeatController[] Controllers;
        public int LocalSeat;
        public string[] SeatNames;
        /// <summary>Runs first: clients wait here for the master's Init so the seat and controllers are known.</summary>
        public Func<IEnumerator> Prepare;
        /// <summary>Starts the engine (StartGame on the master, StartReplicaGame on a client). May wait for the network.</summary>
        public Func<IEnumerator> Start;
        /// <summary>Awaited before StartNextRound. Clients wait here for the master's NextRound.</summary>
        public Func<IEnumerator> NextRoundGate;
        /// <summary>Called once the loop exists, so the network layer can subscribe to its events.</summary>
        public Action<MatchLoop> Attach;
        public bool Online;
        /// <summary>When it returns true the match is abandoned (e.g. the host left).</summary>
        public Func<bool> Aborted;
        public string AbortMessage;
    }

    /// <summary>
    /// Runs one game: queues engine events for the table to present in order, and only asks a seat to act once all
    /// pending animations have finished. Plain C# (no MonoBehaviour), so it is testable headless with table == null.
    /// </summary>
    public sealed class MatchLoop
    {
        private readonly MatchSetup _setup;
        private readonly TableView _table;
        private readonly Queue<GameEvent> _queue = new Queue<GameEvent>();
        private bool _skipPresentation;
        private bool _autoDismissPopups;

        public TarneebEngine Engine => _setup.Engine;
        public SeatController[] Controllers => _setup.Controllers;
        public bool Finished { get; private set; }
        public bool WasAborted { get; private set; }
        public int WinningTeam { get; private set; } = -1;

        /// <summary>Every raw engine event, raised synchronously as the engine emits it (before it is presented).</summary>
        public event Action<GameEvent> EngineEvent;
        /// <summary>A command was accepted by the engine.</summary>
        public event Action<int, PlayerCommand> CommandApplied;
        /// <summary>The engine is about to start the next round.</summary>
        public event Action NextRoundStarting;
        /// <summary>The engine has started the next round (its events have been raised).</summary>
        public event Action NextRoundStarted;

        public MatchLoop(MatchSetup setup, TableView table)
        {
            _setup = setup;
            _table = table;
        }

        public IEnumerator Run(bool skipPresentation, bool autoDismissPopups, Action<GameOverView.Choice> onChoice)
        {
            _skipPresentation = skipPresentation || _table == null;
            _autoDismissPopups = autoDismissPopups;
            if (_setup.Prepare != null) yield return _setup.Prepare();
            var engine = _setup.Engine;

            _table?.BeginGame(engine, new SeatMapper(_setup.LocalSeat, engine.Config.Counterclockwise), _setup.SeatNames);
            _queue.Clear();
            engine.EventEmitted += OnEngineEvent;

            if (_setup.Start != null) yield return _setup.Start();

            while (true)
            {
                yield return DrainQueue();
                GamePhase phase = engine.State.Phase;

                if (phase == GamePhase.RoundOver || phase == GamePhase.GameOver)
                {
                    bool over = phase == GamePhase.GameOver;
                    if (!_skipPresentation)
                        yield return _table.ShowRoundSummary(engine.State.LastRound, over, _autoDismissPopups ? 2.5f : 0f);
                    if (over) break;
                    if (_setup.NextRoundGate != null) yield return _setup.NextRoundGate();
                    NextRoundStarting?.Invoke();
                    engine.StartNextRound();
                    NextRoundStarted?.Invoke();
                    continue;
                }

                SeatController controller = _setup.Controllers[engine.State.CurrentSeat];
                controller.Begin(engine, _table);
                PlayerCommand command;
                bool abort = false;
                while (!controller.Poll(out command))
                {
                    if (_setup.Aborted != null && _setup.Aborted()) { abort = true; break; }
                    yield return null;
                }
                if (abort)
                {
                    controller.Cancel();
                    engine.EventEmitted -= OnEngineEvent;
                    WasAborted = true;
                    if (!_skipPresentation)
                    {
                        _table.Notify(_setup.AbortMessage ?? "The match was cancelled");
                        yield return Tween.Wait(1.8f);
                    }
                    onChoice?.Invoke(GameOverView.Choice.Menu);
                    yield break;
                }
                Apply(command, controller.Seat);
            }

            engine.EventEmitted -= OnEngineEvent;
            Finished = true;
            WinningTeam = engine.State.WinningTeam;

            GameOverView.Choice choice = GameOverView.Choice.PlayAgain;
            if (!_skipPresentation)
                yield return _table.ShowGameOver(WinningTeam, engine.State.TeamScores, c => choice = c, _autoDismissPopups ? 2.5f : 0f);
            onChoice?.Invoke(choice);
        }

        private void OnEngineEvent(GameEvent e)
        {
            _queue.Enqueue(e);
            for (int i = 0; i < _setup.Controllers.Length; i++) _setup.Controllers[i]?.Observe(e);
            EngineEvent?.Invoke(e);
        }

        private IEnumerator DrainQueue()
        {
            while (_queue.Count > 0)
            {
                GameEvent e = _queue.Dequeue();
                if (_skipPresentation) continue;
                IEnumerator routine = _table.Present(e);
                if (routine != null) yield return routine;
            }
        }

        private void Apply(PlayerCommand c, int seat)
        {
            var engine = _setup.Engine;
            CommandResult r;
            switch (c.Kind)
            {
                case CommandKind.Bid: r = engine.PlaceBid(seat, c.Amount); break;
                case CommandKind.Pass: r = engine.Pass(seat); break;
                case CommandKind.Trump: r = engine.ChooseTrump(seat, c.Suit); break;
                default: r = engine.PlayCard(seat, c.Card); break;
            }
            if (r.Success) CommandApplied?.Invoke(seat, c);
            else Debug.LogWarning("[Match] Seat " + seat + " command " + c.Kind + " rejected: " + r);
        }
    }
}
