using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Game;
using Tarneeb.Net;

namespace Tarneeb.Tests
{
    public class NetTests
    {
        /// <summary>Steps nested IEnumerators without Unity's coroutine runner, so match loops run in EditMode.</summary>
        private sealed class Co
        {
            private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
            public bool Done;

            public Co(IEnumerator root) { _stack.Push(root); }

            public void Step()
            {
                while (_stack.Count > 0)
                {
                    IEnumerator top = _stack.Peek();
                    if (!top.MoveNext()) { _stack.Pop(); continue; }
                    if (top.Current is IEnumerator inner) { _stack.Push(inner); continue; }
                    return;
                }
                Done = true;
            }
        }

        private sealed class Peer
        {
            public LoopbackTransport Transport;
            public MatchLoop Loop;
            public TarneebEngine Engine;
            public Co Routine;
            public readonly List<string> Rounds = new List<string>();
            public bool Connected = true;
        }

        private static readonly string[] Names = { "A", "B", "C", "D" };

        private static string Summarize(RoundSummary s) =>
            s.Declarer + ":" + s.Bid + ":" + s.Trump + ":" + s.DeclaringTricks + ":" + s.DeclaringDelta + ":" + s.DefendingDelta + ":" + s.TeamScoresAfter[0] + "/" + s.TeamScoresAfter[1];

        private static void Watch(Peer p)
        {
            p.Engine.EventEmitted += e => { if (e is RoundEnded r) p.Rounds.Add(Summarize(r.Summary)); };
        }

        private static (LoopbackHub hub, Peer host, List<Peer> clients, NetHost netHost) Build(int seed, int humanClients)
        {
            var hub = new LoopbackHub();
            var rules = new RulesConfig();
            var hostT = hub.AddPeer();
            var clientTs = new List<LoopbackTransport>();
            var seatByActor = new Dictionary<int, int>();
            for (int i = 0; i < humanClients; i++)
            {
                var t = hub.AddPeer();
                clientTs.Add(t);
                seatByActor[t.LocalActor] = i + 1;
            }

            var netHost = new NetHost(hostT, rules, seed, 0, seatByActor, s => new BotController(s, BotConfig.Normal(), seed + 500 + s, false), botThinkDelay: false);
            var hostSetup = netHost.Build(Names);
            var host = new Peer { Transport = hostT, Engine = hostSetup.Engine, Loop = new MatchLoop(hostSetup, null) };
            hostSetup.Attach(host.Loop);
            Watch(host);
            host.Routine = new Co(host.Loop.Run(true, true, null));

            var clients = new List<Peer>();
            foreach (var t in clientTs)
            {
                var client = new NetClient(t, rules, s => new BotController(s, BotConfig.Normal(), seed + 900 + s, false));
                var setup = client.Build(Names);
                var peer = new Peer { Transport = t, Engine = setup.Engine, Loop = new MatchLoop(setup, null) };
                Watch(peer);
                peer.Routine = new Co(peer.Loop.Run(true, true, null));
                clients.Add(peer);
            }
            return (hub, host, clients, netHost);
        }

        private static void RunAll(LoopbackHub hub, Peer host, List<Peer> clients, System.Func<int, bool> hook = null)
        {
            int guard = 0;
            var all = new List<Peer> { host };
            all.AddRange(clients);
            while (all.Any(p => p.Connected && !p.Routine.Done))
            {
                Assert.Less(++guard, 3000000, "online match did not finish");
                hub.Pump();
                foreach (var p in all) if (p.Connected && !p.Routine.Done) p.Routine.Step();
                if (hook != null && hook(guard)) break;
            }
            hub.Pump();
        }

        [Test]
        public void Codec_RoundTripsEveryCommand()
        {
            var cmds = new List<PlayerCommand> { PlayerCommand.MakeBid(9), PlayerCommand.MakePass(), PlayerCommand.MakeTrump(Suit.Hearts), PlayerCommand.MakePlay(new Card(Suit.Spades, Rank.Queen)) };
            foreach (var c in cmds)
            {
                Assert.IsTrue(NetCodec.TryDecodeRequest(NetCodec.EncodeRequest(c), out var back));
                Assert.AreEqual(c.Kind, back.Kind);
                var n = NetCodec.FromApplied(2, c);
                Assert.IsTrue(NetCodec.TryDecodeCommand(NetCodec.EncodeCommand(n), out var nb));
                Assert.AreEqual(2, nb.Seat);
                Assert.AreEqual(c.Kind, nb.Command.Kind);
                if (c.Kind == CommandKind.Play) Assert.AreEqual(c.Card, nb.Command.Card);
                if (c.Kind == CommandKind.Bid) Assert.AreEqual(9, nb.Command.Amount);
                if (c.Kind == CommandKind.Trump) Assert.AreEqual(Suit.Hearts, nb.Command.Suit);
            }
        }

        [Test]
        public void Codec_RejectsGarbage()
        {
            Assert.IsFalse(NetCodec.TryDecodeRequest(null, out _));
            Assert.IsFalse(NetCodec.TryDecodeRequest(new byte[] { 9, 0 }, out _));
            Assert.IsFalse(NetCodec.TryDecodeRequest(new byte[] { (byte)CommandKind.Play, 200 }, out _));
            Assert.IsFalse(NetCodec.TryDecodeCommand(new byte[] { 1, 7, 0 }, out _));
            Assert.IsFalse(NetCodec.TryDecodeCommand(new byte[] { 99, 0, 0 }, out _));
        }

        [Test]
        public void Hand_RoundTripsAsBytes()
        {
            var hand = new List<Card>(Deck.Create().Take(13));
            var bytes = NetCodec.EncodeHand(hand);
            Assert.AreEqual(13, bytes.Length);
            CollectionAssert.AreEqual(hand, NetCodec.DecodeHand(bytes));
        }

        [Test]
        public void FourPlayerOnlineGame_ClientsMirrorHost_AndHandsAreNeverBroadcast()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var (hub, host, clients, _) = Build(seed * 31, 3);
                RunAll(hub, host, clients);

                Assert.IsTrue(host.Loop.Finished, "host finished");
                Assert.Greater(host.Rounds.Count, 1);
                foreach (var c in clients)
                {
                    Assert.IsTrue(c.Loop.Finished, "client finished");
                    Assert.AreEqual(GamePhase.GameOver, c.Engine.State.Phase);
                    Assert.AreEqual(host.Engine.State.WinningTeam, c.Engine.State.WinningTeam);
                    CollectionAssert.AreEqual(host.Rounds, c.Rounds, "client round results mirror the host");
                    CollectionAssert.AreEqual(host.Engine.State.TeamScores, c.Engine.State.TeamScores);
                }

                // Hidden information: hands only ever travel as targeted messages, one per deal per player.
                var handPackets = hub.Log.Where(p => p.Code == NetCodes.Hand).ToList();
                Assert.IsTrue(handPackets.All(p => p.Target == NetTarget.Actor), "a hand was broadcast");
                Assert.IsTrue(handPackets.All(p => p.Actor >= 2 && p.Actor <= 4));
                Assert.IsTrue(handPackets.All(p => p.Payload.Length == 13));
                Assert.AreEqual(0, handPackets.Count % 3, "one hand per remote player per deal");
                Assert.GreaterOrEqual(handPackets.Count, host.Rounds.Count * 3);
            }
        }

        [Test]
        public void BotsFillEmptySeats_WithOnlyOneRemotePlayer()
        {
            var (hub, host, clients, _) = Build(77, 1);
            RunAll(hub, host, clients);
            Assert.IsTrue(host.Loop.Finished);
            Assert.IsTrue(clients[0].Loop.Finished);
            CollectionAssert.AreEqual(host.Rounds, clients[0].Rounds);
        }

        [Test]
        public void RemotePlayerLeaving_MidGame_BotTakesOver_MatchStillFinishes()
        {
            var (hub, host, clients, _) = Build(5, 3);
            var leaver = clients[1];
            bool left = false;
            RunAll(hub, host, clients, guard =>
            {
                if (!left && host.Rounds.Count >= 1)
                {
                    left = true;
                    leaver.Connected = false;
                    hub.Disconnect(leaver.Transport);
                }
                return false;
            });
            Assert.IsTrue(left, "the player never left");
            Assert.IsTrue(host.Loop.Finished, "host finished after the bot took over");
            foreach (var c in clients.Where(c => c.Connected)) Assert.IsTrue(c.Loop.Finished);
        }

        [Test]
        public void HostLeaving_EndsClientMatchesGracefully()
        {
            var (hub, host, clients, _) = Build(9, 3);
            bool left = false;
            RunAll(hub, host, clients, guard =>
            {
                if (!left && host.Rounds.Count >= 1)
                {
                    left = true;
                    host.Connected = false;
                    hub.Disconnect(host.Transport);
                }
                return false;
            });
            foreach (var c in clients)
            {
                Assert.IsTrue(c.Routine.Done, "client loop ended");
                Assert.IsTrue(c.Loop.WasAborted, "client noticed the host left");
            }
        }

        [Test]
        public void RequestsFromWrongSeat_OrOutOfTurn_AreIgnored()
        {
            var (hub, host, clients, netHost) = Build(3, 3);
            // Start everything, then have actor 4 (seat 3) spam an out-of-turn play request.
            int steps = 0;
            RunAll(hub, host, clients, guard =>
            {
                if (guard == 5)
                    clients[2].Transport.Send(NetCodes.Request, NetCodec.EncodeRequest(PlayerCommand.MakePlay(new Card(Suit.Spades, Rank.Ace))), NetTarget.MasterClient);
                steps++;
                return false;
            });
            Assert.IsTrue(host.Loop.Finished);
            CollectionAssert.AreEqual(host.Rounds, clients[2].Rounds);
        }
    }
}
