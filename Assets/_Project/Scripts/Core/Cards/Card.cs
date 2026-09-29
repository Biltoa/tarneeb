using System;

namespace Tarneeb.Core.Cards
{
    public enum Suit : byte { Clubs = 0, Diamonds = 1, Hearts = 2, Spades = 3 }

    public enum Rank : byte
    {
        Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace
    }

    public readonly struct Card : IEquatable<Card>
    {
        public readonly Suit Suit;
        public readonly Rank Rank;

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        /// <summary>Compact wire form: suit * 13 + (rank - 2), 0..51.</summary>
        public byte ToByte() => (byte)((int)Suit * 13 + ((int)Rank - 2));

        public static Card FromByte(byte b) => new Card((Suit)(b / 13), (Rank)(b % 13 + 2));

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => ToByte();
        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);

        public override string ToString() => RankSymbol(Rank) + SuitSymbol(Suit);

        public static string SuitSymbol(Suit s)
        {
            switch (s)
            {
                case Suit.Clubs: return "♣";
                case Suit.Diamonds: return "♦";
                case Suit.Hearts: return "♥";
                default: return "♠";
            }
        }

        public static string RankSymbol(Rank r)
        {
            switch (r)
            {
                case Rank.Ace: return "A";
                case Rank.King: return "K";
                case Rank.Queen: return "Q";
                case Rank.Jack: return "J";
                case Rank.Ten: return "10";
                default: return ((int)r).ToString();
            }
        }
    }
}
