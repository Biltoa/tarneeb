using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Game;

namespace Tarneeb.Net
{
    /// <summary>
    /// Non-master client. Keeps a replica engine that only ever applies the master's accepted commands, so the same
    /// TableView and animation queue used in single player render an online game without any changes.
    /// </summary>
    public sealed class NetClient
    {
        private readonly INetTransport _net;
        private readonly RulesConfig _rules;
        private readonly Func<int, SeatController> _inputController;
        private readonly NetCommandQueue _queue = new NetCommandQueue();
        private readonly Queue<List<Card>> _hands = new Queue<List<Card>>();
        private TarneebEngine _engine;
        private int _seat = -1;

        public bool HostLeft { get; private set; }
        public int LocalSeat => _seat;

        /// <param name="inputController">Factory for the widgets/decision source of the local seat (LocalHumanController; a bot in tests).</param>
        public NetClient(INetTransport net, RulesConfig rules, Func<int, SeatController> inputController)
        {
            _net = net;
            _rules = rules ?? new RulesConfig();
            _inputController = inputController;
            _net.Received += OnReceived;
            _net.MasterLeft += () => HostLeft = true;
        }

        public TarneebEngine Engine => _engine;

        /// <summary>Builds the setup; Prepare (run first by the match loop) waits for Init and wires the controllers.</summary>
        public MatchSetup Build(string[] names)
        {
            _engine = new TarneebEngine(_rules, 0);
            _engine.ReplicaHandProvider = () => _hands.Count > 0 ? _hands.Dequeue() : null;
            var setup = new MatchSetup
            {
                Engine = _engine,
                Controllers = null,           // filled in by Prepare (needs the seat)
                LocalSeat = 0,
                SeatNames = names,
                Online = true,
                Start = null,
                NextRoundGate = NextRoundGate,
                Aborted = () => HostLeft,
                AbortMessage = "The host left the game"
            };
            setup.Prepare = () => Prepare(setup);
            return setup;
        }

        private IEnumerator Prepare(MatchSetup setup)
        {
            while (_seat < 0)
            {
                if (HostLeft) yield break;
                yield return null;
            }
            setup.LocalSeat = _seat;
            var c = new SeatController[4];
            for (int s = 0; s < 4; s++)
            {
                c[s] = s == _seat
                    ? (SeatController)new NetLocalController(s, _inputController(s), _queue, SendRequest)
                    : new NetSeatController(s, _queue);
            }
            setup.Controllers = c;
            setup.Start = StartRoutine;
        }

        private IEnumerator StartRoutine()
        {
            while (_queue.Count == 0 || _queue.Peek().Kind != NetCommandKind.GameStart)
            {
                if (HostLeft) yield break;
                yield return null;
            }
            NetCommand start = _queue.Dequeue();
            _engine.StartReplicaGame(start.Value, _seat);
        }

        private IEnumerator NextRoundGate()
        {
            while (_queue.Count == 0 || _queue.Peek().Kind != NetCommandKind.NextRound)
            {
                if (HostLeft) yield break;
                yield return null;
            }
            _queue.Dequeue();
        }

        private void SendRequest(PlayerCommand c) => _net.Send(NetCodes.Request, NetCodec.EncodeRequest(c), NetTarget.MasterClient);

        private void OnReceived(byte code, byte[] payload, int sender)
        {
            if (_net.IsMasterClient) return;
            switch (code)
            {
                case NetCodes.Init:
                    if (payload != null && payload.Length >= 1 && payload[0] <= 3) _seat = payload[0];
                    break;
                case NetCodes.Hand:
                    _hands.Enqueue(NetCodec.DecodeHand(payload));
                    break;
                case NetCodes.Command:
                    if (NetCodec.TryDecodeCommand(payload, out NetCommand n)) _queue.Enqueue(n);
                    break;
            }
        }
    }
}
