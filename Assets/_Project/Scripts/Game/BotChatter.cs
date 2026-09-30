using System.Collections;
using UnityEngine;

namespace Tarneeb.Game
{
    /// <summary>Single player only: bots occasionally answer the local player's emoji and messages.</summary>
    public sealed class BotChatter : MonoBehaviour
    {
        private static readonly int[] Friendly = { 0, 1, 2, 3, 8, 9, 10, 11 };
        private static readonly string[] Replies = { "Good luck!", "Nice one!", "Thanks!", "Let's go!", "Well played", "Hehe" };

        private System.Random _rng;
        private int _localSeat;

        public void Init(int localSeat, int seed)
        {
            _localSeat = localSeat;
            _rng = new System.Random(seed);
        }

        private void OnEnable() => ChatService.Received += OnMessage;
        private void OnDisable() => ChatService.Received -= OnMessage;

        private void OnMessage(int seat, ChatMessage m)
        {
            if (_rng == null || seat != _localSeat || _rng.NextDouble() > 0.55) return;
            int bot = (_localSeat + 1 + _rng.Next(3)) % 4;
            StartCoroutine(Reply(bot, m.IsEmoji));
        }

        private IEnumerator Reply(int bot, bool emoji)
        {
            yield return new WaitForSeconds(0.9f + (float)_rng.NextDouble() * 1.4f);
            ChatService.Deliver(bot, emoji ? ChatMessage.Emoji(Friendly[_rng.Next(Friendly.Length)]) : ChatMessage.Say(Replies[_rng.Next(Replies.Length)]));
        }
    }
}
