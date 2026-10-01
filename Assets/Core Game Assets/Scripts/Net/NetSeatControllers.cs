using System;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Game;
using Tarneeb.Presentation;

namespace Tarneeb.Net
{
    /// <summary>Client-side seat that plays whatever the master announces for it.</summary>
    public sealed class NetSeatController : SeatController
    {
        private readonly NetCommandQueue _queue;

        public NetSeatController(int seat, NetCommandQueue queue) : base(seat) { _queue = queue; }

        public override void Begin(TarneebEngine engine, TableView table) { }

        public override bool Poll(out PlayerCommand command)
        {
            command = default;
            if (_queue.Count == 0) return false;
            NetCommand head = _queue.Peek();
            if (head.Seat != Seat || head.Kind == NetCommandKind.NextRound || head.Kind == NetCommandKind.GameStart) return false;
            command = _queue.Dequeue().Command;
            return true;
        }
    }

    /// <summary>
    /// Client-side controller for the local seat: shows the normal input widgets, sends the choice to the master as a
    /// request, and applies nothing until the master echoes the accepted command.
    /// </summary>
    public sealed class NetLocalController : SeatController
    {
        private readonly SeatController _input;
        private readonly NetCommandQueue _queue;
        private readonly Action<PlayerCommand> _send;
        private bool _requested;

        public NetLocalController(int seat, SeatController input, NetCommandQueue queue, Action<PlayerCommand> send) : base(seat)
        {
            _input = input;
            _queue = queue;
            _send = send;
        }

        public override void Observe(GameEvent e) => _input.Observe(e);

        public override void Begin(TarneebEngine engine, TableView table)
        {
            _requested = false;
            _input.Begin(engine, table);
        }

        public override bool Poll(out PlayerCommand command)
        {
            command = default;
            if (!_requested && _input.Poll(out PlayerCommand mine))
            {
                _requested = true;
                _send(mine);
            }
            if (_queue.Count == 0) return false;
            NetCommand head = _queue.Peek();
            if (head.Seat != Seat || head.Kind == NetCommandKind.NextRound || head.Kind == NetCommandKind.GameStart) return false;
            _input.Cancel();
            command = _queue.Dequeue().Command;
            return true;
        }

        public override void Cancel() => _input.Cancel();
    }
}
