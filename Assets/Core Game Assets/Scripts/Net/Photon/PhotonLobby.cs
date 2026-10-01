using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Tarneeb.Game;
using UnityEngine;

namespace Tarneeb.Net.Photon
{
    /// <summary>
    /// ILobby over Photon: Quick Match (random public room, max 4), Private Room (5-letter code) and a waiting room.
    /// The master presses Start; empty seats are filled with bots by NetHost. Fixed region keeps everyone in one pool.
    /// </summary>
    public sealed class PhotonLobby : MonoBehaviourPunCallbacks, ILobby
    {
        public const string Region = "eu";
        private const string StartKey = "go";
        private enum Intent { None, Quick, Create, Join }

        public event Action<string> Status;
        public event Action Changed;
        public event Action MatchStarting;

        private Intent _pending;
        private string _code = "";
        private bool _reconnectAfterDisconnect;
        private bool _started;
        private PhotonTransport _transport;

        public bool InRoom => PhotonNetwork.InRoom;
        public bool IsMaster => PhotonNetwork.IsMasterClient;
        public int PlayerCount => PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
        public int MaxPlayers => 4;
        public string RoomCode => _code;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => LobbyProvider.Factory = Create;

        private static ILobby Create()
        {
            var go = new GameObject("PhotonLobby");
            DontDestroyOnLoad(go);
            return go.AddComponent<PhotonLobby>();
        }

        public static string NewRoomCode()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // no look-alike characters
            var c = new char[5];
            for (int i = 0; i < c.Length; i++) c[i] = alphabet[UnityEngine.Random.Range(0, alphabet.Length)];
            return new string(c);
        }

        public void QuickMatch() { _code = ""; _pending = Intent.Quick; Connect(); }
        public void CreatePrivate() { _code = NewRoomCode(); _pending = Intent.Create; Connect(); }
        public void JoinPrivate(string code) { _code = code; _pending = Intent.Join; Connect(); }

        /// <summary>Master only: lock the room and tell everyone (including this client) to load the Game scene.</summary>
        public void StartMatch()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || _started) return;
            _started = true;
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { StartKey, 1 } });
        }

        public void Leave()
        {
            _pending = Intent.None;
            _started = false;
            _code = "";
            if (_transport != null) { Destroy(_transport.gameObject); _transport = null; }
            OnlineSession.Transport = null;
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            Changed?.Invoke();
        }

        private void Connect()
        {
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.GameVersion = Application.version;
            PhotonNetwork.NickName = "Player";
            if (PhotonNetwork.NetworkClientState == ClientState.Disconnecting) { _reconnectAfterDisconnect = true; Status?.Invoke("Connecting..."); return; }
            if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom) { OnConnectedToMaster(); return; }
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = Region;
            Status?.Invoke("Connecting...");
            if (!PhotonNetwork.ConnectUsingSettings()) Status?.Invoke("Could not connect. Check your connection and try again.");
        }

        public override void OnConnectedToMaster()
        {
            Intent intent = _pending;
            _pending = Intent.None;                         // OnConnectedToMaster also fires after leaving a room
            switch (intent)
            {
                case Intent.Quick: Status?.Invoke("Looking for a table..."); PhotonNetwork.JoinRandomRoom(null, (byte)MaxPlayers); break;
                case Intent.Create: PhotonNetwork.CreateRoom(_code, new RoomOptions { MaxPlayers = (byte)MaxPlayers, IsVisible = false }); break;
                case Intent.Join: PhotonNetwork.JoinRoom(_code); break;
            }
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = (byte)MaxPlayers });     // nobody waiting: open a table
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (returnCode == ErrorCode.GameIdAlreadyExists && _code.Length == 5)
            {
                _code = NewRoomCode();
                PhotonNetwork.CreateRoom(_code, new RoomOptions { MaxPlayers = (byte)MaxPlayers, IsVisible = false });
                return;
            }
            Status?.Invoke("Could not create the room: " + message);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            _code = "";
            Status?.Invoke(returnCode == ErrorCode.GameFull ? "That room is full" : "Room not found, or the game already started");
        }

        public override void OnJoinedRoom()
        {
            _started = false;
            if (_transport == null)
            {
                var go = new GameObject("PhotonTransport");
                DontDestroyOnLoad(go);
                _transport = go.AddComponent<PhotonTransport>();
            }
            OnlineSession.Transport = _transport;
            Changed?.Invoke();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer) => Changed?.Invoke();
        public override void OnPlayerLeftRoom(Player otherPlayer) => Changed?.Invoke();
        public override void OnMasterClientSwitched(Player newMasterClient) => Changed?.Invoke();

        public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changed)
        {
            if (changed != null && changed.ContainsKey(StartKey)) { _started = true; MatchStarting?.Invoke(); }
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (_reconnectAfterDisconnect) { _reconnectAfterDisconnect = false; Connect(); return; }
            if (cause != DisconnectCause.DisconnectByClientLogic) Status?.Invoke("Disconnected (" + cause + ")");
            Changed?.Invoke();
        }
    }
}
