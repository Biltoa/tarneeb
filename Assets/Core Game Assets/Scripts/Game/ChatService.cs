using System;
using System.Text;

namespace Tarneeb.Game
{
    /// <summary>One chat item: either a quick emoji (EmojiId 0..EmojiCount-1) or a short line of text.</summary>
    public readonly struct ChatMessage
    {
        public readonly int EmojiId;
        public readonly string Text;

        private ChatMessage(int emojiId, string text) { EmojiId = emojiId; Text = text; }

        public bool IsEmoji => EmojiId >= 0;
        public static ChatMessage Emoji(int id) => new ChatMessage(id, null);
        public static ChatMessage Say(string text) => new ChatMessage(-1, text);
    }

    /// <summary>Pure text rules shared by the sender, the host and every receiver.</summary>
    public static class ChatText
    {
        public const int MaxLength = 60;
        public const int EmojiCount = 12;

        // Deliberately small: the host also rate-limits, and free text is English-only (the font has no Arabic glyphs).
        private static readonly string[] Blocked =
        {
            "fuck", "shit", "bitch", "asshole", "dick", "cunt", "nigger", "nigga", "faggot", "whore", "slut", "bastard", "retard",
            "شرموطة", "شرموط", "قحبة", "منيك", "عرص"
        };
        private static readonly string[] LinkMarks = { "http", "www.", "://", ".com", ".net", ".org", ".gg", ".io", ".ly" };

        /// <summary>
        /// Normalises player text: keeps printable Latin characters only (no rich-text brackets, no control or bidi
        /// characters), collapses spaces, cuts to MaxLength and masks links and blocked words. False if nothing is left.
        /// </summary>
        public static bool TryClean(string raw, out string clean)
        {
            clean = null;
            if (string.IsNullOrEmpty(raw)) return false;
            var sb = new StringBuilder(raw.Length);
            bool lastSpace = true;
            foreach (char c in raw)
            {
                bool ok = (c >= 0x20 && c <= 0x7E && c != '<' && c != '>') || (c >= 0xA0 && c <= 0xFF);
                if (char.IsWhiteSpace(c) || !ok)
                {
                    if (char.IsWhiteSpace(c) && !lastSpace) { sb.Append(' '); lastSpace = true; }
                    continue;
                }
                sb.Append(c);
                lastSpace = false;
                if (sb.Length >= MaxLength) break;
            }
            string s = sb.ToString().Trim();
            if (s.Length == 0) return false;

            string[] words = s.Split(' ');
            for (int i = 0; i < words.Length; i++) if (IsBad(words[i])) words[i] = new string('*', words[i].Length);
            clean = string.Join(" ", words);
            return true;
        }

        private static bool IsBad(string word)
        {
            string w = word.ToLowerInvariant();
            foreach (string m in LinkMarks) if (w.Contains(m)) return true;
            var letters = new StringBuilder(w.Length);
            foreach (char c in w) if (char.IsLetter(c)) letters.Append(c);
            string l = letters.ToString();
            foreach (string b in Blocked)
                if (l == b || l == b + "s" || l == b + "ed" || l == b + "er" || l == b + "ing" || l == b + "y") return true;
            return false;
        }
    }

    /// <summary>Minimum gap between messages from one seat.</summary>
    public sealed class ChatRateLimiter
    {
        private readonly float _gap;
        private readonly float[] _last = { -100f, -100f, -100f, -100f };

        public ChatRateLimiter(float minGapSeconds = 0.8f) { _gap = minGapSeconds; }

        public bool Allow(int seat, float now)
        {
            if (seat < 0 || seat > 3 || now - _last[seat] < _gap) return false;
            _last[seat] = now;
            return true;
        }
    }

    /// <summary>
    /// Bridge between the chat UI and whatever carries messages. Offline, a sent message is delivered straight back
    /// to the local seat; online, the Net layer installs Outgoing and delivers what the host relays.
    /// </summary>
    public static class ChatService
    {
        /// <summary>(engine seat, message). Raised for every message to show, including the local player's own.</summary>
        public static event Action<int, ChatMessage> Received;

        /// <summary>Set by the Net layer while a room is active; null means offline.</summary>
        public static Action<ChatMessage> Outgoing;
        /// <summary>Set by the Net layer so leaving a match unsubscribes it.</summary>
        public static Action Detach;
        public static int LocalSeat;

        public static bool Send(ChatMessage m)
        {
            if (m.IsEmoji)
            {
                if (m.EmojiId >= ChatText.EmojiCount) return false;
            }
            else
            {
                if (!ChatText.TryClean(m.Text, out string clean)) return false;
                m = ChatMessage.Say(clean);
            }
            if (Outgoing != null) Outgoing(m); else Deliver(LocalSeat, m);
            return true;
        }

        public static void Deliver(int seat, ChatMessage m)
        {
            if (seat >= 0 && seat <= 3) Received?.Invoke(seat, m);
        }

        /// <summary>Called when a game scene starts and ends so nothing leaks between matches.</summary>
        public static void Reset()
        {
            Detach?.Invoke();
            Detach = null;
            Outgoing = null;
        }
    }
}
