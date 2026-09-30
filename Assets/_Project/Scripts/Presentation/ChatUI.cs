using System.Collections.Generic;
using System.Text;
using Tarneeb.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// The emoji and chat buttons, their two popup panels and the message log. It only talks to ChatService, so it
    /// behaves the same offline (bots answer) and online (the host relays).
    /// </summary>
    public sealed class ChatUI : MonoBehaviour
    {
        private const int LogLines = 9;
        private const float MinGap = 0.8f;
        private static readonly string[] Quick = { "Good luck!", "Nice play!", "Well played", "Oops!", "Thanks!", "Hurry up!" };

        private TableView _table;
        private GameObject _emojiPanel, _chatPanel, _unread;
        private TMP_Text _log;
        private TMP_InputField _input;
        private readonly List<string> _lines = new List<string>();
        private float _lastSend = -10f;
        private bool _bound;

        public bool AnyPanelOpen => _emojiPanel.activeSelf || _chatPanel.activeSelf;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _table = GetComponentInParent<TableView>();
            _emojiPanel = UiBind.Go(transform, "EmojiPanel");
            _chatPanel = UiBind.Go(transform, "ChatPanel");
            _unread = UiBind.Go(transform, "ChatButton/Unread");
            _log = UiBind.Get<TMP_Text>(transform, "ChatPanel/Panel/Log");
            _input = UiBind.Get<TMP_InputField>(transform, "ChatPanel/Panel/Input");

            UiBind.Get<Button>(transform, "EmojiButton").onClick.AddListener(() => Open(_emojiPanel));
            UiBind.Get<Button>(transform, "ChatButton").onClick.AddListener(() => Open(_chatPanel));
            UiBind.Get<Button>(transform, "EmojiPanel/Dim").onClick.AddListener(CloseAll);
            UiBind.Get<Button>(transform, "ChatPanel/Dim").onClick.AddListener(CloseAll);
            UiBind.Get<Button>(transform, "ChatPanel/Panel/Close").onClick.AddListener(CloseAll);
            UiBind.Get<Button>(transform, "ChatPanel/Panel/Send").onClick.AddListener(SendInput);
            _input.onSubmit.AddListener(_ => SendInput());
            _input.characterLimit = ChatText.MaxLength;

            for (int i = 0; i < ChatText.EmojiCount; i++)
            {
                int id = i;
                Transform t = transform.Find("EmojiPanel/Panel/Emoji_" + i);
                if (t == null) { Debug.LogError("[ChatUI] missing Emoji_" + i); continue; }
                t.Find("Icon").GetComponent<Image>().sprite = EmojiArt.Get(i);
                t.GetComponent<Button>().onClick.AddListener(() => SendEmoji(id));
            }
            for (int i = 0; i < Quick.Length; i++)
            {
                string phrase = Quick[i];
                Transform t = transform.Find("ChatPanel/Panel/Quick_" + i);
                if (t == null) { Debug.LogError("[ChatUI] missing Quick_" + i); continue; }
                t.Find("Label").GetComponent<TMP_Text>().text = phrase;
                t.GetComponent<Button>().onClick.AddListener(() => SendText(phrase));
            }

            _emojiPanel.SetActive(false);
            _chatPanel.SetActive(false);
            _unread.SetActive(false);
            RefreshLog();
        }

        private void OnEnable() { Bind(); ChatService.Received += OnReceived; }
        private void OnDisable() => ChatService.Received -= OnReceived;

        private void Open(GameObject panel)
        {
            _emojiPanel.SetActive(panel == _emojiPanel);
            _chatPanel.SetActive(panel == _chatPanel);
            if (panel == _chatPanel) _unread.SetActive(false);
            Transform inner = panel.transform.Find("Panel");
            if (inner != null) StartCoroutine(Tween.ScaleTo(inner, 0.85f, 1f, 0.25f, Ease.OutBack));
        }

        public void CloseAll()
        {
            _emojiPanel.SetActive(false);
            _chatPanel.SetActive(false);
        }

        private bool Cooldown()
        {
            if (Time.unscaledTime - _lastSend < MinGap) return true;
            _lastSend = Time.unscaledTime;
            return false;
        }

        private void SendEmoji(int id)
        {
            if (Cooldown()) return;
            ChatService.Send(ChatMessage.Emoji(id));
            CloseAll();
        }

        private void SendText(string text)
        {
            if (Cooldown()) return;
            ChatService.Send(ChatMessage.Say(text));
        }

        private void SendInput()
        {
            string text = _input.text;
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Cooldown()) return;
            if (ChatService.Send(ChatMessage.Say(text))) _input.text = "";
            _input.ActivateInputField();
        }

        private void OnReceived(int seat, ChatMessage m)
        {
            if (_table != null) _table.ShowChat(seat, m);
            if (m.IsEmoji) return;
            string who = seat == ChatService.LocalSeat ? "You" : (_table != null ? _table.NameOf(seat) : "Player");
            _lines.Add("<color=#F5CC5C>" + who + ":</color> " + m.Text);
            if (_lines.Count > LogLines) _lines.RemoveAt(0);
            RefreshLog();
            if (!_chatPanel.activeSelf && seat != ChatService.LocalSeat) _unread.SetActive(true);
        }

        private void RefreshLog()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _lines.Count; i++) { if (i > 0) sb.Append('\n'); sb.Append(_lines[i]); }
            _log.text = sb.Length == 0 ? "<color=#FFFFFF80>No messages yet</color>" : sb.ToString();
        }
    }
}
