using System;
using System.Collections.Generic;

namespace Tarneeb.Net
{
    public enum NetTarget { All, MasterClient, Actor }

    /// <summary>
    /// Minimal reliable, ordered message channel between the players of a room. Implemented by PhotonTransport (PUN 2,
    /// only compiled when PUN is installed) and by LoopbackTransport (in-process, used by the tests).
    /// </summary>
    public interface INetTransport
    {
        bool IsMasterClient { get; }
        int LocalActor { get; }
        /// <summary>Actor numbers currently in the room, ascending.</summary>
        IReadOnlyList<int> Actors { get; }

        void Send(byte code, byte[] payload, NetTarget target, int actor = 0);

        /// <summary>(code, payload, senderActor)</summary>
        event Action<byte, byte[], int> Received;
        event Action<int> PlayerLeft;
        event Action MasterLeft;
    }
}
