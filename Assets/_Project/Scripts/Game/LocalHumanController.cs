using System.Collections.Generic;
using Tarneeb.Config;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Presentation;

namespace Tarneeb.Game
{
    public sealed class LocalHumanController : SeatController
    {
        private readonly HeuristicBot _fallback;
        private readonly List<Card> _legal = new List<Card>(13);
        private TarneebEngine _engine;
        private TableView _table;
        private bool _done, _active;
        private PlayerCommand _command;
        private double _autoAt, _timeoutAt;

        public LocalHumanController(int seat, int seed) : base(seat)
        {
            _fallback = new HeuristicBot(BotConfig.Normal(), seed);
        }

        /// <summary>Set when a human decision (not the timer or auto-play) was made; used by NetLocalController to send it.</summary>
        public bool DecidedByPlayer { get; private set; }

        public override void Observe(GameEvent e) => _fallback.Observe(e);

        public override void Begin(TarneebEngine engine, TableView table)
        {
            _engine = engine;
            _table = table;
            _done = false;
            _active = true;
            DecidedByPlayer = false;
            _autoAt = -1;
            double limit = GameSettings.TurnTimerSeconds;
            _timeoutAt = limit > 0 ? Clock.Seconds + limit : -1;

            switch (engine.State.Phase)
            {
                case GamePhase.Bidding:
                    if (engine.MinimumLegalBid() > engine.Config.MaximumBid)
                    {
                        _command = PlayerCommand.MakePass();
                        _autoAt = Clock.Seconds + 0.5 / Tween.SpeedMultiplier;
                        break;
                    }
                    _table?.ShowBidPanel(engine.MinimumLegalBid(),
                        amount => Choose(PlayerCommand.MakeBid(amount)),
                        () => Choose(PlayerCommand.MakePass()));
                    break;

                case GamePhase.ChoosingTrump:
                    _table?.ShowTrumpPicker(suit => Choose(PlayerCommand.MakeTrump(suit)));
                    break;

                default:
                    engine.GetLegalCards(Seat, _legal);
                    if (_legal.Count == 1)
                    {
                        // Only one legal card: play it without asking.
                        _table?.BeginPlayInput(_legal, _ => { });
                        _command = PlayerCommand.MakePlay(_legal[0]);
                        _autoAt = Clock.Seconds + 0.45 / Tween.SpeedMultiplier;
                    }
                    else
                    {
                        _table?.BeginPlayInput(_legal, card => Choose(PlayerCommand.MakePlay(card)));
                    }
                    break;
            }
        }

        private void Choose(PlayerCommand c)
        {
            if (!_active || _done) return;
            _command = c;
            _done = true;
            DecidedByPlayer = true;
        }

        public override bool Poll(out PlayerCommand command)
        {
            command = _command;
            if (_done) { Finish(); return true; }
            if (_autoAt >= 0 && Clock.Seconds >= _autoAt) { Finish(); return true; }
            if (_timeoutAt > 0 && Clock.Seconds >= _timeoutAt)
            {
                command = BotController.Decide(_fallback, _engine, Seat);
                Finish();
                return true;
            }
            return false;
        }

        public override void Cancel() => Finish();

        private void Finish()
        {
            _active = false;
            _table?.CancelInput();
        }
    }
}
