using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;

namespace Tarneeb.Core.Bots
{
    /// <summary>Remembers played cards and which suits each seat has shown void in. Fed by engine events.</summary>
    public sealed class CardTracker
    {
        private readonly bool[] _played = new bool[Deck.Size];
        private readonly bool[,] _void = new bool[4, 4];

        public void Observe(GameEvent e)
        {
            if (e is CardsDealt)
            {
                Reset();
            }
            else if (e is CardPlayed p)
            {
                _played[p.Card.ToByte()] = true;
                if (p.Card.Suit != p.LedSuit) _void[p.Seat, (int)p.LedSuit] = true;
            }
        }

        public void Reset()
        {
            for (int i = 0; i < _played.Length; i++) _played[i] = false;
            for (int s = 0; s < 4; s++)
                for (int u = 0; u < 4; u++)
                    _void[s, u] = false;
        }

        public bool IsPlayed(Card c) => _played[c.ToByte()];
        public bool IsVoid(int seat, Suit suit) => _void[seat, (int)suit];

        /// <summary>True if no card of this suit that outranks c is still unseen (played) and not in the given hand.</summary>
        public bool IsTopRemaining(Card c, Hand ownHand)
        {
            for (int r = (int)c.Rank + 1; r <= (int)Rank.Ace; r++)
            {
                var higher = new Card(c.Suit, (Rank)r);
                if (!IsPlayed(higher) && !ownHand.Contains(higher)) return false;
            }
            return true;
        }

        /// <summary>Number of cards of the suit not yet played and not in ownHand (held by the other three seats).</summary>
        public int OutstandingCount(Suit suit, Hand ownHand)
        {
            int n = 0;
            for (int r = 2; r <= 14; r++)
            {
                var c = new Card(suit, (Rank)r);
                if (!IsPlayed(c) && !ownHand.Contains(c)) n++;
            }
            return n;
        }
    }
}
