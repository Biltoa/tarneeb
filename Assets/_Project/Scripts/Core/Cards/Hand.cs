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

        /// <summary>Sorts by suit then rank descending, for display.</summary>
        public void SortForDisplay()
        {
            _cards.Sort((a, b) =>
            {
                int s = ((int)a.Suit).CompareTo((int)b.Suit);
                return s != 0 ? s : ((int)b.Rank).CompareTo((int)a.Rank);
            });
        }
    }
}
