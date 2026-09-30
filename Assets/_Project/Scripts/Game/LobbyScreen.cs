using System.Collections;
using Tarneeb.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tarneeb.Game
{
    /// <summary>
    /// The "Play Online" window in the Menu scene: quick match, private room by code, and the waiting room. All
    /// networking goes through ILobby, so this works with Photon or with the in-process test lobby.
    /// </summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        private const float AutoStartSeconds = 3f;

        private GameObject _menuView, _roomView, _start;
        private TMP_Text _status, _roomCode, _players;
        private TMP_InputField _code;
        private Button _quick, _create, _join;
        private ILobby _lobby;
        private Coroutine _countdown;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _menuView = UiBind.Go(transform, "Panel/MenuView");
            _roomView = UiBind.Go(transform, "Panel/RoomView");
            _status = UiBind.Get<TMP_Text>(transform, "Panel/Status");
            _roomCode = UiBind.Get<TMP_Text>(transform, "Panel/RoomView/RoomCode");
            _players = UiBind.Get<TMP_Text>(transform, "Panel/RoomView/Players");
            _start = UiBind.Go(transform, "Panel/RoomView/Start");
            _code = UiBind.Get<TMP_InputField>(transform, "Panel/MenuView/CodeInput");
            _quick = UiBind.Get<Button>(transform, "Panel/MenuView/Quick");
            _create = UiBind.Get<Button>(transform, "Panel/MenuView/Create");
            _join = UiBind.Get<Button>(transform, "Panel/MenuView/Join");

            _quick.onClick.AddListener(() => Begin("Looking for a table...", l => l.QuickMatch()));
            _create.onClick.AddListener(() => Begin("Creating a private room...", l => l.CreatePrivate()));
            _join.onClick.AddListener(JoinClicked);
            _code.characterLimit = 5;
            _code.onValueChanged.AddListener(v => { string u = v.ToUpperInvariant(); if (u != v) _code.text = u; });
            UiBind.Get<Button>(transform, "Panel/RoomView/Start").onClick.AddListener(() => { _lobby?.StartMatch(); });
            UiBind.Get<Button>(transform, "Panel/RoomView/Leave").onClick.AddListener(LeaveClicked);
            UiBind.Get<Button>(transform, "Panel/MenuView/Back").onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open()
        {
            Bind();
            _lobby = LobbyProvider.GetOrCreate();
            if (_lobby == null) return;
            gameObject.SetActive(true);
            _lobby.Status += OnStatus;
            _lobby.Changed += Refresh;
            _lobby.MatchStarting += OnMatchStarting;
            _status.text = "";
            Refresh();
        }

        public void Close()
        {
            Unhook();
            if (_lobby != null && _lobby.InRoom) _lobby.Leave();
            gameObject.SetActive(false);
        }

        private void Unhook()
        {
            if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }
            if (_lobby == null) return;
            _lobby.Status -= OnStatus;
            _lobby.Changed -= Refresh;
            _lobby.MatchStarting -= OnMatchStarting;
        }

        private void OnDestroy() => Unhook();

        private void Begin(string message, System.Action<ILobby> action)
        {
            _status.text = message;
            SetButtons(false);
            action(_lobby);
        }

        private void JoinClicked()
        {
            string code = _code.text.Trim().ToUpperInvariant();
            if (code.Length != 5) { _status.text = "Enter the 5-letter room code"; return; }
            Begin("Joining " + code + "...", l => l.JoinPrivate(code));
        }

        private void LeaveClicked()
        {
            if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }
            _lobby.Leave();
            _status.text = "";
            Refresh();
        }

        private void SetButtons(bool on)
        {
            _quick.interactable = on;
            _create.interactable = on;
            _join.interactable = on;
        }

        private void OnStatus(string text)
        {
            _status.text = text;
            if (_lobby != null && !_lobby.InRoom) SetButtons(true);     // a failure: let the player try again
        }

        private void Refresh()
        {
            bool inRoom = _lobby != null && _lobby.InRoom;
            _menuView.SetActive(!inRoom);
            _roomView.SetActive(inRoom);
            if (!inRoom) { SetButtons(true); return; }

            _roomCode.text = string.IsNullOrEmpty(_lobby.RoomCode) ? "Quick match" : "Room code: " + _lobby.RoomCode;
            _players.text = _lobby.PlayerCount + " / " + _lobby.MaxPlayers + " players";
            _start.SetActive(_lobby.IsMaster);
            if (!_lobby.IsMaster) _status.text = "Waiting for the host to start...";
            else if (_lobby.PlayerCount >= _lobby.MaxPlayers)
            {
                if (_countdown == null) _countdown = StartCoroutine(AutoStart());
            }
            else
            {
                if (_countdown != null) { StopCoroutine(_countdown); _countdown = null; }
                _status.text = string.IsNullOrEmpty(_lobby.RoomCode)
                    ? "Waiting for players. Start any time, bots fill empty seats."
                    : "Share the code with friends. Start any time, bots fill empty seats.";
            }
        }

        private IEnumerator AutoStart()
        {
            for (float t = AutoStartSeconds; t > 0f; t -= 1f)
            {
                _status.text = "Table full. Starting in " + Mathf.CeilToInt(t) + "...";
                yield return new WaitForSecondsRealtime(1f);
            }
            _countdown = null;
            _lobby?.StartMatch();
        }

        private void OnMatchStarting()
        {
            Unhook();
            GameController.OnlineRequested = true;
            ScreenFader.Go(MenuController.GameSceneName);
        }
    }
}
