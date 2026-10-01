using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Game;

namespace Tarneeb.Net
{
    /// <summary>
    /// Master client: owns the authoritative engine, runs the bots, validates every request and broadcasts accepted
    /// commands. Each player's hand is sent only to that player (never broadcast).
    /// </summary>
    public sealed class NetHost
    {
        private readonly INetTransport _net;
        private readonly RulesConfig _rules;
        private readonly int _seed;
        private readonly int _masterSeat;
        private readonly Dictionary<int, int> _seatByActor;          // remote human actors only
        private readonly Func<int, SeatController> _masterController;
        private readonly bool _botThink;
        private readonly SeatController[] _controllers = new SeatController[4];
        private readonly List<GameEvent> _roundEvents = new List<GameEvent>();
        private TarneebEngine _engine;
        private bool _connected;

        /// <param name="seatByActor">Remote human players (actor number to seat). Every other seat gets a bot.</param>
        /// <param name="masterController">Factory for the master's own seat (a LocalHumanController in the game; a bot in tests).</param>
        public NetHost(INetTransport net, RulesConfig rules, int seed, int masterSeat,
            IDictionary<int, int> seatByActor, Func<int, SeatController> masterController, bool botThinkDelay = true)
        {
            _botThink = botThinkDelay;
            _net = net;
            _rules = rules ?? new RulesConfig();
            _seed = seed;
            _masterSeat = masterSeat;
            _seatByActor = new Dictionary<int, int>(seatByActor);
            _masterController = masterController;
        }

        public TarneebEngine Engine => _engine;

        public MatchSetup Build(string[] names)
        {
            _engine = new TarneebEngine(_rules, _seed);
            BotConfig bot = BotConfig.Normal();
            for (int s = 0; s < 4; s++)
            {
                if (s == _masterSeat) _controllers[s] = _masterController(s);
                else if (SeatIsRemote(s, out _)) _controllers[s] = new RemotePlayerController(s, _seed + s);
                else _controllers[s] = new BotController(s, bot, _seed + s * 101, _botThink);
            }

            var setup = new MatchSetup
            {
                Engine = _engine,
                Controllers = _controllers,
                LocalSeat = _masterSeat,
                SeatNames = names,
                Online = true,
                Start = StartRoutine,
                Attach = Attach
            };
            return setup;
        }

        private bool SeatIsRemote(int seat, out int actor)
        {
            foreach (var kv in _seatByActor)
                if (kv.Value == seat) { actor = kv.Key; return true; }
            actor = 0;
            return false;
        }

        private IEnumerator StartRoutine()
        {
            foreach (var kv in _seatByActor) _net.Send(NetCodes.Init, new[] { (byte)kv.Value }, NetTarget.Actor, kv.Key);
            int dealer = new Random(_seed).Next(4);
            _engine.StartGame(dealer);                       // CardsDealt fires here and the hands go out
            _net.Send(NetCodes.Command, NetCodec.EncodeCommand(new NetCommand { Kind = NetCommandKind.GameStart, Value = dealer }), NetTarget.All);
            yield break;
        }

        private void Attach(MatchLoop loop)
        {
            _connected = true;
            loop.EngineEvent += OnEngineEvent;
            loop.CommandApplied += (seat, cmd) =>
                _net.Send(NetCodes.Command, NetCodec.EncodeCommand(NetCodec.FromApplied(seat, cmd)), NetTarget.All);
            loop.NextRoundStarted += () =>
                _net.Send(NetCodes.Command, NetCodec.EncodeCommand(new NetCommand { Kind = NetCommandKind.NextRound }), NetTarget.All);
            _net.Received += OnReceived;
            _net.PlayerLeft += OnPlayerLeft;
        }

        private void OnEngineEvent(GameEvent e)
        {
            if (e is CardsDealt) _roundEvents.Clear();
            _roundEvents.Add(e);
            if (!(e is CardsDealt)) return;
            foreach (var kv in _seatByActor)
                _net.Send(NetCodes.Hand, NetCodec.EncodeHand(_engine.State.Hands[kv.Value].Cards), NetTarget.Actor, kv.Key);
        }

        private void OnReceived(byte code, byte[] payload, int sender)
        {
            if (!_connected || code != NetCodes.Request) return;
            if (!_seatByActor.TryGetValue(sender, out int seat)) return;         // unknown or replaced by a bot
            if (!NetCodec.TryDecodeRequest(payload, out PlayerCommand cmd)) return;
            if (_engine.State.CurrentSeat != seat) return;                       // out of turn
            (_controllers[seat] as RemotePlayerController)?.Receive(cmd);        // the loop validates it with the engine
        }

        /// <summary>A non-master player left: a bot takes the seat immediately.</summary>
        private void OnPlayerLeft(int actor)
        {
            if (!_seatByActor.TryGetValue(actor, out int seat)) return;
            _seatByActor.Remove(actor);
            var bot = new BotController(seat, BotConfig.Normal(), _seed + seat * 977, _botThink);
            _controllers[seat].Cancel();
            _controllers[seat] = bot;
            // Catch the new bot up on the current round so its card tracker knows what has been played.
            foreach (GameEvent e in _roundEvents) bot.Observe(e);
        }
    }
}
