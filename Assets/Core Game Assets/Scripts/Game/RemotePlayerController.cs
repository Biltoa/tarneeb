using Tarneeb.Config;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Presentation;

namespace Tarneeb.Game
{
    /// <summary>
    /// Seat driven by another machine, on the authoritative host. The network layer calls Receive when that player's
    /// request arrives. If the turn timer runs out, a bot plays for the seat.
    /// </summary>
    public sealed class RemotePlayerController : SeatController
    {
        private readonly HeuristicBot _fallback;
        private TarneebEngine _engine;
        private PlayerCommand? _pending;
        private bool _active;
        private double _timeoutAt;

        public RemotePlayerController(int seat, int seed) : base(seat)
        {
            _fallback = new HeuristicBot(BotConfig.Normal(), seed);
        }

        public bool IsWaiting => _active;

        public override void Observe(GameEvent e) => _fallback.Observe(e);

        /// <summary>
        /// Stores a request. The host only forwards requests while it is this seat's turn; the request may arrive
        /// before the match loop has finished animating and called Begin, so it is kept, not dropped.
        /// </summary>
        public void Receive(PlayerCommand command) => _pending = command;

        public override void Begin(TarneebEngine engine, TableView table)
        {
            _engine = engine;
            _active = true;
            double limit = GameSettings.TurnTimerSeconds > 0 ? GameSettings.TurnTimerSeconds : engine.Config.TurnTimerSeconds;
            _timeoutAt = limit > 0 ? Clock.Seconds + limit : -1;
        }

        public override bool Poll(out PlayerCommand command)
        {
            command = default;
            if (_pending.HasValue)
            {
                command = _pending.Value;
                _pending = null;
                _active = false;
                return true;
            }
            if (_timeoutAt > 0 && Clock.Seconds >= _timeoutAt)
            {
                command = BotController.Decide(_fallback, _engine, Seat);
                _active = false;
                return true;
            }
            return false;
        }

        public override void Cancel() { _active = false; }
    }
}
