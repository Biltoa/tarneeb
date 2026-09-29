using System;
using System.Collections;
using Tarneeb.Core.State;
using Tarneeb.Presentation;

namespace Tarneeb.Game
{
    /// <summary>
    /// Seat driven by another machine. The network layer calls Receive when that player's request arrives at the
    /// authoritative host. Unused in single player.
    /// </summary>
    public sealed class RemotePlayerController : SeatController
    {
        private PlayerCommand? _pending;

        public RemotePlayerController(int seat) : base(seat) { }

        public void Receive(PlayerCommand command) => _pending = command;

        public override IEnumerator Act(TarneebEngine engine, TableView table, Action<PlayerCommand> submit)
        {
            while (!_pending.HasValue) yield return null;
            PlayerCommand c = _pending.Value;
            _pending = null;
            submit(c);
        }
    }
}
