using System.Collections.Generic;
using Tarneeb.Core.Cards;
using Tarneeb.Game;

namespace Tarneeb.Net
{
    /// <summary>Custom event codes (RaiseEvent). Game logic never uses PhotonViews.</summary>
    public static class NetCodes
    {
        /// <summary>Client to master: [CommandKind, a]. The master infers the seat from the sender.</summary>
        public const byte Request = 1;
        /// <summary>Master to all: an accepted command, [NetCommandKind, seat, a].</summary>
        public const byte Command = 10;
        /// <summary>Master to one player: that player's 13 cards (one byte each). Never broadcast.</summary>
        public const byte Hand = 11;
        /// <summary>Master to one player: [seat].</summary>
        public const byte Init = 12;
    }

    public enum NetCommandKind : byte { GameStart = 0, Bid = 1, Pass = 2, Trump = 3, Play = 4, NextRound = 5 }

    public struct NetCommand
    {
        public NetCommandKind Kind;
        public int Seat;
        public PlayerCommand Command;
        /// <summary>GameStart: the first dealer.</summary>
        public int Value;
    }

    /// <summary>Compact encodings: a card is one byte (suit * 13 + rank - 2).</summary>
    public static class NetCodec
    {
        public static byte[] EncodeRequest(PlayerCommand c) => new[] { (byte)c.Kind, ArgOf(c) };

        public static bool TryDecodeRequest(byte[] data, out PlayerCommand c)
        {
            c = default;
            if (data == null || data.Length < 2 || data[0] > (byte)CommandKind.Play) return false;
            c = FromArg((CommandKind)data[0], data[1]);
            return IsValidArg(c, data[1]);
        }

        public static byte[] EncodeCommand(NetCommand n)
        {
            byte a = n.Kind == NetCommandKind.GameStart ? (byte)n.Value : ArgOf(n.Command);
            return new[] { (byte)n.Kind, (byte)n.Seat, a };
        }

        public static bool TryDecodeCommand(byte[] data, out NetCommand n)
        {
            n = default;
            if (data == null || data.Length < 3 || data[0] > (byte)NetCommandKind.NextRound || data[1] > 3) return false;
            n.Kind = (NetCommandKind)data[0];
            n.Seat = data[1];
            switch (n.Kind)
            {
                case NetCommandKind.GameStart: n.Value = data[2]; return data[2] <= 3;
                case NetCommandKind.Bid: n.Command = PlayerCommand.MakeBid(data[2]); return true;
                case NetCommandKind.Pass: n.Command = PlayerCommand.MakePass(); return true;
                case NetCommandKind.Trump: n.Command = PlayerCommand.MakeTrump((Suit)data[2]); return data[2] <= 3;
                case NetCommandKind.Play: n.Command = PlayerCommand.MakePlay(Card.FromByte(data[2])); return data[2] <= 51;
                default: return true;
            }
        }

        public static NetCommand FromApplied(int seat, PlayerCommand c)
        {
            NetCommandKind kind;
            switch (c.Kind)
            {
                case CommandKind.Bid: kind = NetCommandKind.Bid; break;
                case CommandKind.Pass: kind = NetCommandKind.Pass; break;
                case CommandKind.Trump: kind = NetCommandKind.Trump; break;
                default: kind = NetCommandKind.Play; break;
            }
            return new NetCommand { Kind = kind, Seat = seat, Command = c };
        }

        public static byte[] EncodeHand(IReadOnlyList<Card> cards)
        {
            var b = new byte[cards.Count];
            for (int i = 0; i < b.Length; i++) b[i] = cards[i].ToByte();
            return b;
        }

        public static List<Card> DecodeHand(byte[] data)
        {
            var list = new List<Card>(data.Length);
            for (int i = 0; i < data.Length; i++) if (data[i] <= 51) list.Add(Card.FromByte(data[i]));
            return list;
        }

        private static byte ArgOf(PlayerCommand c)
        {
            switch (c.Kind)
            {
                case CommandKind.Bid: return (byte)c.Amount;
                case CommandKind.Trump: return (byte)c.Suit;
                case CommandKind.Play: return c.Card.ToByte();
                default: return 0;
            }
        }

        private static PlayerCommand FromArg(CommandKind k, byte a)
        {
            switch (k)
            {
                case CommandKind.Bid: return PlayerCommand.MakeBid(a);
                case CommandKind.Trump: return PlayerCommand.MakeTrump((Suit)a);
                case CommandKind.Play: return PlayerCommand.MakePlay(Card.FromByte(a));
                default: return PlayerCommand.MakePass();
            }
        }

        private static bool IsValidArg(PlayerCommand c, byte a)
        {
            switch (c.Kind)
            {
                case CommandKind.Trump: return a <= 3;
                case CommandKind.Play: return a <= 51;
                default: return true;
            }
        }
    }
}
