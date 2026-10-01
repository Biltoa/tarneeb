using System.Collections.Generic;

namespace Tarneeb.Core.Cards
{
    public sealed class Hand
    {
        private readonly List<Card> _cards = new List<Card>(13);

        public IReadOnlyList<Card> Cards => _cards;
        public int Count => _cards.Count;

        public void Add(Card c) => _cards.Add(c);
        public bool Contains(Card c) => _cards.Contains(c);
        public bool Remove(Card c) => _cards.Remove(c);
        public void Clear() => _cards.Clear();

        public bool HasSuit(Suit s)
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].Suit == s) return true;
            return false;
        }

        public int CountSuit(Suit s)
        {
            int n = 0;
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i].Suit == s) n++;
            return n;
        }

        /// <summary>Left-to-right display order of the suits: diamonds, clubs, hearts, spades (red and black alternate).</summary>
        public static int DisplayOrder(Suit s)
        {
            switch (s)
            {
                case Suit.Diamonds: return 0;
                case Suit.Clubs: return 1;
                case Suit.Hearts: return 2;
                default: return 3;
            }
        }

        /// <summary>Sorts by display suit order, then rank descending, for display.</summary>
        public void SortForDisplay()
        {
            _cards.Sort((a, b) =>
            {
                int s = DisplayOrder(a.Suit).CompareTo(DisplayOrder(b.Suit));
                return s != 0 ? s : ((int)b.Rank).CompareTo((int)a.Rank);
            });
        }
    }
}
