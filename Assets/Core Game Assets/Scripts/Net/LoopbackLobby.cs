using System;
using Tarneeb.Game;
using UnityEngine;

namespace Tarneeb.Net
{
    /// <summary>
    /// In-process stand-in for a matchmaking backend: "joins" a room instantly with the local player as master (the
    /// other seats become bots) over a LoopbackHub. Used by the PlayMode tests to run the whole online path (menu
    /// window, host, chat) without a network; never registered in a real build.
    /// </summary>
    public sealed class LoopbackLobby : ILobby
    {
        private readonly LoopbackHub _hub = new LoopbackHub();
        private bool _in;
        private string _code = "";

        public event Action<string> Status;
        public event Action Changed;
        public event Action MatchStarting;

        /// <summary>Pretend this many other humans are in the room (only changes the counter; seats are still bots).</summary>
        public int PretendOthers;
        public bool FailNextJoin;

        public bool InRoom => _in;
        public bool IsMaster => true;
        public int PlayerCount => _in ? 1 + PretendOthers : 0;
        public int MaxPlayers => 4;
        public string RoomCode => _code;

        public void QuickMatch() => Enter("");
        public void CreatePrivate() => Enter("K7QXA");
        public void JoinPrivate(string code) => Enter(code);

        private void Enter(string code)
        {
            if (FailNextJoin) { FailNextJoin = false; Status?.Invoke("Room not found"); return; }
            _code = code;
            _in = true;
            Changed?.Invoke();
        }

        public void StartMatch()
        {
            if (!_in) return;
            LoopbackTransport me = _hub.AddPeer();          // the first peer is actor 1, the master
            OnlineSession.Transport = me;
            var pump = new GameObject("LoopbackPump").AddComponent<Pump>();
            pump.Hub = _hub;
            UnityEngine.Object.DontDestroyOnLoad(pump.gameObject);
            MatchStarting?.Invoke();
        }

        public void Leave()
        {
            _in = false;
            OnlineSession.Transport = null;
            Changed?.Invoke();
        }

        private sealed class Pump : MonoBehaviour
        {
            public LoopbackHub Hub;
            private void Update() => Hub?.Pump();
        }
    }
}
