using System.Collections.Generic;
using NUnit.Framework;
using Tarneeb.Game;
using Tarneeb.Net;

namespace Tarneeb.Tests
{
    public class ChatTests
    {
        [Test]
        public void Clean_normalises_spaces()
        {
            Assert.IsTrue(ChatText.TryClean("  hello   world  ", out string c));
            Assert.AreEqual("hello world", c);
        }

        [Test]
        public void Clean_removes_rich_text_brackets_and_control_characters()
        {
            Assert.IsTrue(ChatText.TryClean("a<color=red>b</color>\u0007‮ c", out string c));
            Assert.AreEqual("acolor=redb/color c", c);
            Assert.IsFalse(c.Contains("<") || c.Contains(">"));
        }

        [Test]
        public void Clean_rejects_empty_and_non_latin_only_text()
        {
            Assert.IsFalse(ChatText.TryClean("", out _));
            Assert.IsFalse(ChatText.TryClean("   \t ", out _));
            Assert.IsFalse(ChatText.TryClean("مرحبا", out _));    // Arabic: the font has no glyphs
        }

        [Test]
        public void Clean_truncates_to_max_length()
        {
            Assert.IsTrue(ChatText.TryClean(new string('a', 500), out string c));
            Assert.AreEqual(ChatText.MaxLength, c.Length);
        }

        [Test]
        public void Clean_masks_links_and_blocked_words()
        {
            Assert.IsTrue(ChatText.TryClean("visit www.spam.example now", out string c));
            Assert.AreEqual("visit " + new string('*', "www.spam.example".Length) + " now", c);
            Assert.IsTrue(ChatText.TryClean("you Bastard!", out c));
            Assert.AreEqual("you ********", c);
            Assert.IsTrue(ChatText.TryClean("classic assess pass", out c));      // no false positives on innocent words
            Assert.AreEqual("classic assess pass", c);
        }

        [Test]
        public void Rate_limiter_enforces_a_gap_per_seat()
        {
            var l = new ChatRateLimiter(1f);
            Assert.IsTrue(l.Allow(0, 10f));
            Assert.IsFalse(l.Allow(0, 10.5f));
            Assert.IsTrue(l.Allow(1, 10.5f));       // other seats are independent
            Assert.IsTrue(l.Allow(0, 11.1f));
            Assert.IsFalse(l.Allow(4, 50f));        // invalid seat
        }

        [Test]
        public void Service_offline_delivers_to_local_seat_and_cleans_text()
        {
            ChatService.Reset();
            ChatService.LocalSeat = 2;
            var got = new List<(int, ChatMessage)>();
            void Handler(int s, ChatMessage m) => got.Add((s, m));
            ChatService.Received += Handler;
            try
            {
                Assert.IsTrue(ChatService.Send(ChatMessage.Say("  hi   there ")));
                Assert.IsTrue(ChatService.Send(ChatMessage.Emoji(3)));
                Assert.IsFalse(ChatService.Send(ChatMessage.Emoji(ChatText.EmojiCount)));
                Assert.IsFalse(ChatService.Send(ChatMessage.Say("   ")));
            }
            finally { ChatService.Received -= Handler; }
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual(2, got[0].Item1);
            Assert.AreEqual("hi there", got[0].Item2.Text);
            Assert.IsTrue(got[1].Item2.IsEmoji);
        }

        // ---------------------------------------------------------------- over the loopback room

        private sealed class Room
        {
            public LoopbackHub Hub = new LoopbackHub();
            public float Now = 100f;
            public readonly List<(int seat, ChatMessage m)>[] Got = new List<(int, ChatMessage)>[3];
            public readonly NetChat[] Chat = new NetChat[3];
            public readonly LoopbackTransport[] T = new LoopbackTransport[3];

            public Room()
            {
                // Actor 1 is the master (seat 0); actors 2 and 3 sit at seats 1 and 2.
                var seatOf = new Dictionary<int, int> { { 2, 1 }, { 3, 2 } };
                for (int i = 0; i < 3; i++)
                {
                    int idx = i;
                    Got[i] = new List<(int, ChatMessage)>();
                    T[i] = Hub.AddPeer();
                    Chat[i] = new NetChat(T[i], i == 0 ? (System.Func<int, int>)(a => seatOf.TryGetValue(a, out int s) ? s : -1) : null,
                        () => idx, () => Now, (seat, m) => Got[idx].Add((seat, m)));
                    Chat[i].Attach(false);
                }
            }
        }

        [Test]
        public void Room_relays_messages_to_everyone_with_the_sender_seat()
        {
            var r = new Room();
            r.Chat[1].Send(ChatMessage.Say("hello table"));       // client at seat 1
            r.Hub.Pump(); r.Hub.Pump();
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(1, r.Got[i].Count, "peer " + i);
                Assert.AreEqual(1, r.Got[i][0].seat);
                Assert.AreEqual("hello table", r.Got[i][0].m.Text);
            }
            r.Now += 5f;
            r.Chat[0].Send(ChatMessage.Emoji(9));                  // the master's own emoji also reaches everybody
            r.Hub.Pump(); r.Hub.Pump();
            for (int i = 0; i < 3; i++) Assert.AreEqual(0, r.Got[i][1].seat);
            Assert.IsTrue(r.Got[2][1].m.IsEmoji);
        }

        [Test]
        public void Room_rate_limits_and_cleans_on_the_master()
        {
            var r = new Room();
            r.Chat[2].Send(ChatMessage.Say("one"));
            r.Chat[2].Send(ChatMessage.Say("two"));                // same instant: dropped by the master
            r.Hub.Pump(); r.Hub.Pump();
            Assert.AreEqual(1, r.Got[0].Count);
            r.Now += 2f;
            // A modified client skips the local filter and sends raw bytes straight to the master.
            r.T[2].Send(NetCodes.Chat, new byte[] { 0, (byte)'<', (byte)'b', (byte)'>', (byte)'w', (byte)'w', (byte)'w', (byte)'.', (byte)'x' }, NetTarget.MasterClient);
            r.Hub.Pump(); r.Hub.Pump();
            Assert.AreEqual(2, r.Got[1].Count);
            Assert.AreEqual("******", r.Got[1][1].m.Text);      // brackets stripped, then the link ("bwww.x") is masked
        }

        [Test]
        public void Room_ignores_garbage_and_spoofed_relays()
        {
            var r = new Room();
            r.T[1].Send(NetCodes.Chat, new byte[] { 1, 200 }, NetTarget.MasterClient);         // emoji id out of range
            r.T[1].Send(NetCodes.Chat, new byte[] { 7, 1, 2 }, NetTarget.MasterClient);        // unknown kind
            r.T[1].Send(NetCodes.Chat, null, NetTarget.MasterClient);
            r.Hub.Pump(); r.Hub.Pump();
            Assert.AreEqual(0, r.Got[0].Count);
            // ChatOut is only meaningful from the master, but even a forged one must not crash or show bad data.
            r.T[1].Send(NetCodes.ChatOut, new byte[] { 9, 1, 1 }, NetTarget.All);              // seat 9
            r.T[1].Send(NetCodes.ChatOut, new byte[] { 1 }, NetTarget.All);                    // truncated
            r.Hub.Pump(); r.Hub.Pump();
            Assert.AreEqual(0, r.Got[2].Count);
        }
    }
}
