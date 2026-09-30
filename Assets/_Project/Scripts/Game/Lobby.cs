using System;

namespace Tarneeb.Game
{
    /// <summary>
    /// What the menu needs from an online matchmaking backend. Implemented by PhotonLobby (only compiled when PUN is
    /// installed) and by LoopbackLobby (in-process, for tests). The menu never references Photon directly.
    /// </summary>
    public interface ILobby
    {
        /// <summary>Human-readable progress or error text for the status line.</summary>
        event Action<string> Status;
        /// <summary>Room membership or master changed; re-read the properties below.</summary>
        event Action Changed;
        /// <summary>Raised on every player when the master starts the match: load the Game scene now.</summary>
        event Action MatchStarting;

        bool InRoom { get; }
        bool IsMaster { get; }
        int PlayerCount { get; }
        int MaxPlayers { get; }
        /// <summary>Code to share for a private room; empty for quick match.</summary>
        string RoomCode { get; }

        void QuickMatch();
        void CreatePrivate();
        void JoinPrivate(string code);
        /// <summary>Master only. Empty seats are filled with bots.</summary>
        void StartMatch();
        /// <summary>Leave the room and disconnect.</summary>
        void Leave();
    }

    /// <summary>Registry filled by the backend at startup; null Factory means online play is not available in this build.</summary>
    public static class LobbyProvider
    {
        public static Func<ILobby> Factory;
        /// <summary>The lobby that owns the current room; kept across the Menu and Game scenes.</summary>
        public static ILobby Current;

        public static bool Available => Factory != null;

        public static ILobby GetOrCreate()
        {
            if (Current == null && Factory != null) Current = Factory();
            return Current;
        }
    }
}
