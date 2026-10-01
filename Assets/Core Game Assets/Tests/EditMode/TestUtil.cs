using System.Collections.Generic;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Tests
{
    internal static class TestUtil
    {
        /// <summary>Parses "AS KH 10D 2C" (rank then suit letter C D H S).</summary>
        public static Card C(string s)
        {
            string r = s.Substring(0, s.Length - 1);
            Suit suit;
            switch (s[s.Length - 1])
            {
                case 'C': suit = Suit.Clubs; break;
                case 'D': suit = Suit.Diamonds; break;
                case 'H': suit = Suit.Hearts; break;
                default: suit = Suit.Spades; break;
            }
            Rank rank;
            switch (r)
            {
                case "A": rank = Rank.Ace; break;
                case "K": rank = Rank.King; break;
                case "Q": rank = Rank.Queen; break;
                case "J": rank = Rank.Jack; break;
                case "10": rank = Rank.Ten; break;
                default: rank = (Rank)int.Parse(r); break;
            }
            return new Card(suit, rank);
        }

        public static Card[] Cards(string list)
        {
            var parts = list.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var result = new Card[parts.Length];
            for (int i = 0; i < parts.Length; i++) result[i] = C(parts[i]);
            return result;
        }

        public static TarneebEngine NewGame(int seed = 1, RulesConfig cfg = null)
        {
            var e = new TarneebEngine(cfg ?? new RulesConfig(), seed);
            e.StartGame();
            return e;
        }

        public static List<GameEvent> Record(TarneebEngine e)
        {
            var list = new List<GameEvent>();
            e.EventEmitted += list.Add;
            return list;
        }

        /// <summary>Hands for a deterministic scenario: seat0 all spades + hearts top etc. Built from the full deck so all 52 cards are present.</summary>
        public static Card[][] SplitDeckBySuit()
        {
            // seat s gets suit s entirely (13 cards).
            var hands = new Card[4][];
            for (int s = 0; s < 4; s++)
            {
                hands[s] = new Card[13];
                for (int r = 2; r <= 14; r++) hands[s][r - 2] = new Card((Suit)s, (Rank)r);
            }
            return hands;
        }

        /// <summary>Drives the engine so seat `declarer` (must be the first bidder's position or reachable) wins the bid at `bid`.</summary>
        public static void MakeDeclarer(TarneebEngine e, int declarer, int bid, Suit trump)
        {
            int guard = 0;
            while (e.State.Phase == GamePhase.Bidding && guard++ < 20)
            {
                int seat = e.State.CurrentSeat;
                if (seat == declarer && e.State.HighBid == 0) e.PlaceBid(seat, bid);
                else e.Pass(seat);
            }
            e.ChooseTrump(declarer, trump);
        }
    }
}
