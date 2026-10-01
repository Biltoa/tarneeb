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
    /// It is created when the room is joined and lives across the Menu and Game scenes, so events that arrive while the
    /// Game scene is still loading are buffered until the first listener subscribes.
    /// </summary>
    public sealed class PhotonTransport : MonoBehaviourPunCallbacks, IOnEventCallback, INetTransport
    {
        private readonly List<int> _actors = new List<int>();
        private readonly List<(byte code, byte[] payload, int sender)> _pending = new List<(byte, byte[], int)>();
        private Action<byte, byte[], int> _received;

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

        public event Action<byte, byte[], int> Received
        {
            add { _received += value; Flush(); }
            remove { _received -= value; }
        }
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
            byte[] data = photonEvent.CustomData as byte[];
            if (_received == null) { _pending.Add((photonEvent.Code, data, photonEvent.Sender)); return; }
            _received(photonEvent.Code, data, photonEvent.Sender);
        }

        private void Flush()
        {
            if (_pending.Count == 0 || _received == null) return;
            var batch = _pending.ToArray();
            _pending.Clear();
            foreach (var e in batch) _received(e.code, e.payload, e.sender);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer) => PlayerLeft?.Invoke(otherPlayer.ActorNumber);

        // Host migration is out of scope: any change of master means the host is gone and the match ends.
        public override void OnMasterClientSwitched(Player newMasterClient) => MasterLeft?.Invoke();
    }
}
