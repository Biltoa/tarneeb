using System;
using System.Text;
using Tarneeb.Game;
using UnityEngine;

namespace Tarneeb.Net
{
    /// <summary>
    /// Chat and emoji over the room transport. Every player sends a request to the master, which works out the seat
    /// from the sender (so nobody can speak as another seat), rate-limits, cleans the text and relays it to everyone
    /// including itself; each peer shows what it receives back. Bots on the master never chat.
    /// </summary>
    public sealed class NetChat
    {
        private const byte KindText = 0, KindEmoji = 1;

        private readonly INetTransport _net;
        private readonly Func<int, int> _seatOfActor;       // master only: actor number to seat, -1 if unknown
        private readonly Func<int> _localSeat;
        private readonly Func<float> _clock;
        private readonly Action<int, ChatMessage> _deliver;
        private readonly ChatRateLimiter _limiter = new ChatRateLimiter();

        /// <param name="deliver">Where received messages go; ChatService.Deliver in the game, a list in tests.</param>
        public NetChat(INetTransport net, Func<int, int> seatOfActor, Func<int> localSeat, Func<float> clock = null,
            Action<int, ChatMessage> deliver = null)
        {
            _deliver = deliver ?? ChatService.Deliver;
            _net = net;
            _seatOfActor = seatOfActor;
            _localSeat = localSeat;
            _clock = clock ?? (() => Time.realtimeSinceStartup);
        }

        /// <param name="bindService">Route ChatService.Send through this instance (the game); tests pass false.</param>
        public void Attach(bool bindService = true)
        {
            _net.Received += OnReceived;
            if (!bindService) return;
            ChatService.Outgoing = Send;
            ChatService.Detach = () => _net.Received -= OnReceived;
        }

        public void Send(ChatMessage m)
        {
            byte[] body = Encode(m);
            if (body == null) return;
            if (_net.IsMasterClient) Relay(_localSeat(), body);
            else _net.Send(NetCodes.Chat, body, NetTarget.MasterClient);
        }

        private void OnReceived(byte code, byte[] payload, int sender)
        {
            if (code == NetCodes.Chat && _net.IsMasterClient)
            {
                int seat = _seatOfActor(sender);
                if (seat >= 0) Relay(seat, payload);
            }
            else if (code == NetCodes.ChatOut && payload != null && payload.Length >= 3 && payload[0] <= 3)
            {
                {
                    ChatService.LocalSeat = _localSeat();
                    if (TryDecode(payload, 1, out ChatMessage m)) _deliver(payload[0], m);
                }
            }
        }

        private void Relay(int seat, byte[] body)
        {
            if (!_limiter.Allow(seat, _clock())) return;
            if (!TryDecode(body, 0, out ChatMessage m)) return;          // re-cleans, so a modified client cannot bypass the filter
            byte[] clean = Encode(m);
            var outp = new byte[clean.Length + 1];
            outp[0] = (byte)seat;
            Buffer.BlockCopy(clean, 0, outp, 1, clean.Length);
            _net.Send(NetCodes.ChatOut, outp, NetTarget.All);
        }

        public static byte[] Encode(ChatMessage m)
        {
            if (m.IsEmoji) return new[] { KindEmoji, (byte)m.EmojiId };
            byte[] text = Encoding.UTF8.GetBytes(m.Text ?? "");
            var b = new byte[text.Length + 1];
            b[0] = KindText;
            Buffer.BlockCopy(text, 0, b, 1, text.Length);
            return b;
        }

        /// <summary>Decodes [kind, body] starting at offset; text is cleaned again and must be non-empty.</summary>
        public static bool TryDecode(byte[] data, int offset, out ChatMessage m)
        {
            m = default;
            if (data == null || data.Length < offset + 2) return false;
            if (data[offset] == KindEmoji)
            {
                if (data.Length != offset + 2 || data[offset + 1] >= ChatText.EmojiCount) return false;
                m = ChatMessage.Emoji(data[offset + 1]);
                return true;
            }
            if (data[offset] != KindText || data.Length - offset - 1 > ChatText.MaxLength * 4) return false;
            string raw = Encoding.UTF8.GetString(data, offset + 1, data.Length - offset - 1);
            if (!ChatText.TryClean(raw, out string clean)) return false;
            m = ChatMessage.Say(clean);
            return true;
        }
    }
}
