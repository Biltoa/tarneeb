using System;
using System.Collections.Generic;

namespace Tarneeb.Net
{
    /// <summary>In-process message hub for tests: reliable, ordered, delivered when Pump is called.</summary>
    public sealed class LoopbackHub
    {
        public struct Packet { public int From; public byte Code; public byte[] Payload; public NetTarget Target; public int Actor; }

        private readonly List<LoopbackTransport> _peers = new List<LoopbackTransport>();
        private readonly Queue<Packet> _queue = new Queue<Packet>();
        public readonly List<Packet> Log = new List<Packet>();

        public int MasterActor { get; private set; } = 1;

        public LoopbackTransport AddPeer()
        {
            var t = new LoopbackTransport(this, _peers.Count + 1);
            _peers.Add(t);
            return t;
        }

        internal void Enqueue(Packet p)
        {
            var copy = p;
            copy.Payload = p.Payload == null ? null : (byte[])p.Payload.Clone();
            _queue.Enqueue(copy);
            Log.Add(copy);
        }

        internal IReadOnlyList<int> ActorList()
        {
            var l = new List<int>();
            foreach (var p in _peers) if (p.Connected) l.Add(p.LocalActor);
            return l;
        }

        public void Pump()
        {
            while (_queue.Count > 0)
            {
                Packet p = _queue.Dequeue();
                foreach (var peer in _peers.ToArray())
                {
                    if (!peer.Connected) continue;
                    bool deliver = p.Target == NetTarget.All
                        || (p.Target == NetTarget.MasterClient && peer.LocalActor == MasterActor)
                        || (p.Target == NetTarget.Actor && peer.LocalActor == p.Actor);
                    if (deliver) peer.Deliver(p);
                }
            }
        }

        public void Disconnect(LoopbackTransport peer)
        {
            peer.Connected = false;
            bool wasMaster = peer.LocalActor == MasterActor;
            foreach (var other in _peers)
            {
                if (!other.Connected) continue;
                if (wasMaster) other.RaiseMasterLeft(); else other.RaisePlayerLeft(peer.LocalActor);
            }
        }
    }

    public sealed class LoopbackTransport : INetTransport
    {
        private readonly LoopbackHub _hub;

        internal bool Connected = true;

        internal LoopbackTransport(LoopbackHub hub, int actor)
        {
            _hub = hub;
            LocalActor = actor;
        }

        public int LocalActor { get; }
        public bool IsMasterClient => LocalActor == _hub.MasterActor;
        public IReadOnlyList<int> Actors => _hub.ActorList();

        public event Action<byte, byte[], int> Received;
        public event Action<int> PlayerLeft;
        public event Action MasterLeft;

        public void Send(byte code, byte[] payload, NetTarget target, int actor = 0)
        {
            if (!Connected) return;
            _hub.Enqueue(new LoopbackHub.Packet { From = LocalActor, Code = code, Payload = payload, Target = target, Actor = actor });
        }

        internal void Deliver(LoopbackHub.Packet p) => Received?.Invoke(p.Code, p.Payload, p.From);
        internal void RaisePlayerLeft(int actor) => PlayerLeft?.Invoke(actor);
        internal void RaiseMasterLeft() => MasterLeft?.Invoke();
    }
}
