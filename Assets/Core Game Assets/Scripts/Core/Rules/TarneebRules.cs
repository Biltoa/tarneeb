using System.Collections.Generic;
using Tarneeb.Core.Cards;

namespace Tarneeb.Core.Rules
{
    /// <summary>Pure rule functions. Seats 0 and 2 are Team A (0); seats 1 and 3 are Team B (1).</summary>
    public static class TarneebRules
    {
        public const int SeatCount = 4;
        public const int TricksPerRound = 13;

        public static int TeamOf(int seat) => seat & 1;
        public static int Partner(int seat) => (seat + 2) & 3;

        public static int NextSeat(int seat, RulesConfig cfg) =>
            cfg.Counterclockwise ? (seat + 1) & 3 : (seat + 3) & 3;

        /// <summary>Seat that wins a complete trick. cardsBySeat is indexed by seat; leader played first.</summary>
        public static int TrickWinner(Card[] cardsBySeat, int leader, Suit trump, RulesConfig cfg)
        {
            int winner = leader;
            Card best = cardsBySeat[leader];
            int seat = leader;
            for (int i = 1; i < SeatCount; i++)
            {
                seat = NextSeat(seat, cfg);
                Card c = cardsBySeat[seat];
                if (Beats(c, best, trump))
                {
                    best = c;
                    winner = seat;
                }
            }
            return winner;
        }

        /// <summary>True if challenger beats the current best card of the trick (best set the led suit or is a trump).</summary>
        public static bool Beats(Card challenger, Card best, Suit trump)
        {
            if (challenger.Suit == best.Suit) return challenger.Rank > best.Rank;
            return challenger.Suit == trump && best.Suit != trump;
        }

        public static void GetLegalCards(Hand hand, Suit? ledSuit, List<Card> result)
        {
            result.Clear();
            bool mustFollow = ledSuit.HasValue && hand.HasSuit(ledSuit.Value);
            for (int i = 0; i < hand.Count; i++)
            {
                Card c = hand.Cards[i];
                if (!mustFollow || c.Suit == ledSuit.Value) result.Add(c);
            }
        }

        public static RoundSummary ScoreRound(int declarer, int bid, Suit trump, int[] tricksByTeam, int[] scoresBefore, RulesConfig cfg)
        {
            int declTeam = TeamOf(declarer);
            int defTeam = 1 - declTeam;
            int declTricks = tricksByTeam[declTeam];
            int defTricks = tricksByTeam[defTeam];
            bool made = declTricks >= bid;
            bool kaboot = made && bid == cfg.MaximumBid && declTricks == TricksPerRound;

            int declDelta, defDelta;
            if (made)
            {
                declDelta = kaboot ? cfg.KabootBonusScore : declTricks;
                defDelta = cfg.DefendersScoreWhenContractMade ? defTricks : 0;
            }
            else
            {
                declDelta = -bid;
                defDelta = defTricks;
            }

            var after = new int[2];
            after[declTeam] = scoresBefore[declTeam] + declDelta;
            after[defTeam] = scoresBefore[defTeam] + defDelta;

            return new RoundSummary
            {
                Declarer = declarer,
                Bid = bid,
                Trump = trump,
                DeclaringTeam = declTeam,
                DeclaringTricks = declTricks,
                DefendingTricks = defTricks,
                ContractMade = made,
                Kaboot = kaboot,
                DeclaringDelta = declDelta,
                DefendingDelta = defDelta,
                TeamScoresAfter = after
            };
        }

        /// <summary>Returns the winning team or -1. If both cross the target the declaring team wins.</summary>
        public static int DetermineGameWinner(int[] scores, int declaringTeam, RulesConfig cfg)
        {
            bool a = scores[0] >= cfg.TargetScore;
            bool b = scores[1] >= cfg.TargetScore;
            if (a && b) return declaringTeam;
            if (a) return 0;
            if (b) return 1;
            return -1;
        }
    }

    public sealed class RoundSummary
    {
        public int Declarer;
        public int Bid;
        public Suit Trump;
        public int DeclaringTeam;
        public int DeclaringTricks;
        public int DefendingTricks;
        public bool ContractMade;
        public bool Kaboot;
        public int DeclaringDelta;
        public int DefendingDelta;
        public int[] TeamScoresAfter;
    }
}
