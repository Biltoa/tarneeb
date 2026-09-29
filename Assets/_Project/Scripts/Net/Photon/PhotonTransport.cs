using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace Tarneeb.Net.Photon
{
    /// <summary>
    /// INetTransport over Photon PUN 2 RaiseEvent. Compiled only when PUN is imported (the PHOTON_UNITY_NETWORKING
    /// define; see Tarneeb.Net.Photon.asmdef). PUN uses secure WebSockets automatically on WebGL.
    /// Not exercised yet: it needs a Photon App ID (see Net/README.md).
    /// </summary>
    public sealed class PhotonTransport : MonoBehaviourPunCallbacks, IOnEventCallback, INetTransport
    {
        private readonly List<int> _actors = new List<int>();

        public bool IsMasterClient => PhotonNetwork.IsMasterClient;
        public int LocalActor => PhotonNetwork.LocalPlayer.ActorNumber;

        public IReadOnlyList<int> Actors
        {
            get
            {
                _actors.Clear();
                foreach (Player p in PhotonNetwork.PlayerList) _actors.Add(p.ActorNumber);
                _actors.Sort();
                return _actors;
            }
        }

        public event Action<byte, byte[], int> Received;
        public event Action<int> PlayerLeft;
        public event Action MasterLeft;

        public override void OnEnable()
        {
            base.OnEnable();
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public void Send(byte code, byte[] payload, NetTarget target, int actor = 0)
        {
            var options = new RaiseEventOptions();
            switch (target)
            {
                case NetTarget.MasterClient: options.Receivers = ReceiverGroup.MasterClient; break;
                case NetTarget.Actor: options.TargetActors = new[] { actor }; break;
                default: options.Receivers = ReceiverGroup.All; break;
            }
            PhotonNetwork.RaiseEvent(code, payload, options, SendOptions.SendReliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code >= 200) return;                       // Photon-internal events
            Received?.Invoke(photonEvent.Code, photonEvent.CustomData as byte[], photonEvent.Sender);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            // If the master left, PUN has already migrated; host migration is out of scope, so the match ends.
            if (otherPlayer.ActorNumber == 1) MasterLeft?.Invoke();
            else PlayerLeft?.Invoke(otherPlayer.ActorNumber);
        }
    }
}
