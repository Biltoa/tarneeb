using System.Collections.Generic;
using Tarneeb.Core.Cards;

namespace Tarneeb.Core.Bots
{
    public static class HandEvaluator
    {
        /// <summary>Estimated tricks for a hand if bestTrump becomes trump. Picks the longest/strongest suit as trump.</summary>
        public static double EstimateTricks(Hand hand, BotConfig cfg, out Suit bestTrump)
        {
            double honors = 0;
            var len = new int[4];
            var has = new bool[4, 15];
            for (int i = 0; i < hand.Count; i++)
            {
                Card c = hand.Cards[i];
                len[(int)c.Suit]++;
                has[(int)c.Suit, (int)c.Rank] = true;
            }

            var suitStrength = new double[4];
            for (int s = 0; s < 4; s++)
            {
                double v = 0;
                if (has[s, (int)Rank.Ace]) v += cfg.AceValue;
                if (has[s, (int)Rank.King]) v += len[s] >= 2 ? cfg.ProtectedKingValue : cfg.BareKingValue;
                if (has[s, (int)Rank.Queen] && len[s] >= 3) v += cfg.ProtectedQueenValue;
                suitStrength[s] = v;
                honors += v;
            }

            // Best trump: longest suit, ties broken by honor strength.
            int best = 0;
            for (int s = 1; s < 4; s++)
            {
                if (len[s] > len[best] || (len[s] == len[best] && suitStrength[s] > suitStrength[best])) best = s;
            }
            bestTrump = (Suit)best;

            double longBonus = len[best] > cfg.LongSuitThreshold ? (len[best] - cfg.LongSuitThreshold) * cfg.LongSuitBonusPerCard : 0;
            return honors + longBonus;
        }

        public static int CountSuit(IReadOnlyList<Card> cards, Suit s)
        {
            int n = 0;
            for (int i = 0; i < cards.Count; i++) if (cards[i].Suit == s) n++;
            return n;
        }
    }
}
