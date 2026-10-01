using System;
using System.Collections.Generic;

namespace Tarneeb.Core.Cards
{
    public static class Deck
    {
        public const int Size = 52;

        public static List<Card> Create()
        {
            var cards = new List<Card>(Size);
            for (int s = 0; s < 4; s++)
                for (int r = 2; r <= 14; r++)
                    cards.Add(new Card((Suit)s, (Rank)r));
            return cards;
        }

        /// <summary>Fisher-Yates using the injected random source, so seeds reproduce deals.</summary>
        public static void Shuffle(IList<Card> cards, Random rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Card tmp = cards[i];
                cards[i] = cards[j];
                cards[j] = tmp;
            }
        }
    }
}
