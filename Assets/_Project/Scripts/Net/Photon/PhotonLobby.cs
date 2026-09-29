using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Tarneeb.Net.Photon
{
    /// <summary>
    /// Quick Match (random room, max 4) and Private Room (short code). The room creator starts the match; empty seats are
    /// filled with bots by NetHost. UI is intentionally not wired up until the online feature is enabled.
    /// </summary>
    public sealed class PhotonLobby : MonoBehaviourPunCallbacks
    {
        public const string Region = "eu";                 // fixed region keeps latency predictable
        public const int MaxPlayers = 4;

        public event Action<string> StatusChanged;
        public event Action RoomReady;

        private string _pendingPrivateCode;
        private bool _quick;

        public static string NewRoomCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no look-alike characters
            var c = new char[5];
            for (int i = 0; i < c.Length; i++) c[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
            return new string(c);
        }

        public void QuickMatch()
        {
            _quick = true;
            Connect();
        }

        public void CreatePrivate(string code)
        {
            _quick = false;
            _pendingPrivateCode = code;
            Connect();
        }

        public void JoinPrivate(string code)
        {
            _quick = false;
            _pendingPrivateCode = code;
            Connect();
        }

        /// <summary>Master only: lock the room and start the match.</summary>
        public void StartMatch()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            RoomReady?.Invoke();
        }

        private void Connect()
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            if (PhotonNetwork.IsConnectedAndReady) { OnConnectedToMaster(); return; }
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = Region;
            PhotonNetwork.ConnectUsingSettings();
            StatusChanged?.Invoke("Connecting...");
        }

        public override void OnConnectedToMaster()
        {
            if (_quick) PhotonNetwork.JoinRandomRoom(null, MaxPlayers);
            else if (_pendingPrivateCode != null) PhotonNetwork.JoinRoom(_pendingPrivateCode);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = MaxPlayers });
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            // Private code not found: create it (the creator shares the code with friends).
            PhotonNetwork.CreateRoom(_pendingPrivateCode, new RoomOptions { MaxPlayers = MaxPlayers, IsVisible = false });
        }

        public override void OnJoinedRoom() => StatusChanged?.Invoke("In room " + PhotonNetwork.CurrentRoom.Name + " (" + PhotonNetwork.CurrentRoom.PlayerCount + "/" + MaxPlayers + ")");
        public override void OnPlayerEnteredRoom(Player newPlayer) => OnJoinedRoom();
        public override void OnDisconnected(DisconnectCause cause) => StatusChanged?.Invoke("Disconnected: " + cause);
    }
}
