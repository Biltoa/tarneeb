using System;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Core.Bots
{
    public sealed class HeuristicBot : IBot
    {
        private readonly BotConfig _cfg;
        private readonly Random _rng;
        private readonly CardTracker _tracker = new CardTracker();
        private readonly List<Card> _legal = new List<Card>(13);

        public HeuristicBot(BotConfig config, int seed)
        {
            _cfg = config ?? BotConfig.Normal();
            _rng = new Random(seed);
        }

        public CardTracker Tracker => _tracker;

        public void Observe(GameEvent e) => _tracker.Observe(e);

        // ------------------------------------------------------------ bidding

        public BidDecision ChooseBid(TarneebEngine engine, int seat)
        {
            double estimate = HandEvaluator.EstimateTricks(engine.State.Hands[seat], _cfg, out _);
            int ceiling = (int)Math.Floor(estimate + _cfg.PartnerAllowance);
            int next = engine.MinimumLegalBid();
            if (next <= engine.Config.MaximumBid && next <= ceiling) return BidDecision.Bid(next);
            return BidDecision.PassBid();
        }

        public Suit ChooseTrump(TarneebEngine engine, int seat)
        {
            HandEvaluator.EstimateTricks(engine.State.Hands[seat], _cfg, out Suit trump);
            return trump;
        }

        // ------------------------------------------------------------ card play

        public Card ChooseCard(TarneebEngine engine, int seat)
        {
            engine.GetLegalCards(seat, _legal);
            if (_legal.Count == 1) return _legal[0];

            if (_cfg.RandomMoveChance > 0 && _rng.NextDouble() < _cfg.RandomMoveChance)
                return _legal[_rng.Next(_legal.Count)];

            var state = engine.State;
            var trick = state.CurrentTrick;
            Suit trump = state.Trump;

            return trick.Count == 0
                ? ChooseLead(engine, seat, trump)
                : ChooseFollow(engine, seat, trump);
        }

        private Card ChooseLead(TarneebEngine engine, int seat, Suit trump)
        {
            var state = engine.State;
            var hand = state.Hands[seat];
            bool ourContract = TarneebRules.TeamOf(state.Declarer) == TarneebRules.TeamOf(seat);
            int myTrumps = hand.CountSuit(trump);

            // Draw trumps when declaring with length and opponents may still hold some.
            if (ourContract && state.Declarer == seat && myTrumps >= 4 && _tracker.OutstandingCount(trump, hand) > 0)
            {
                Card top = HighestOfSuit(_legal, trump);
                return top;
            }

            // Lead a winner: top remaining card of a suit (avoid leading a suit an opponent is void in, unless it is trump).
            Card? winner = null;
            for (int i = 0; i < _legal.Count; i++)
            {
                Card c = _legal[i];
                if (!_tracker.IsTopRemaining(c, hand)) continue;
                if (c.Suit != trump && OpponentVoid(seat, c.Suit)) continue;
                if (!winner.HasValue || (c.Suit != trump && winner.Value.Suit == trump)) winner = c;
                else if (c.Suit == winner.Value.Suit && c.Rank < winner.Value.Rank) winner = c;
            }
            if (winner.HasValue) return winner.Value;

            // Otherwise lead low in a suit with no known opponent void, preferring non-trump.
            Card? lowSafe = null;
            for (int i = 0; i < _legal.Count; i++)
            {
                Card c = _legal[i];
                if (c.Suit == trump || OpponentVoid(seat, c.Suit)) continue;
                if (!lowSafe.HasValue || c.Rank < lowSafe.Value.Rank) lowSafe = c;
            }
            if (lowSafe.HasValue) return lowSafe.Value;

            return LowestSideSuitDiscard(_legal, trump);
        }

        private Card ChooseFollow(TarneebEngine engine, int seat, Suit trump)
        {
            var state = engine.State;
            var trick = state.CurrentTrick;
            Suit led = trick.LedSuit.Value;
            bool follows = _legal[0].Suit == led;

            int winningSeat = CurrentWinner(engine);
            Card winningCard = trick.CardsBySeat[winningSeat].Value;
            bool partnerWinning = TarneebRules.TeamOf(winningSeat) == TarneebRules.TeamOf(seat);

            if (partnerWinning) return follows ? LowestOfSuit(_legal, led) : LowestSideSuitDiscard(_legal, trump);

            // Cheapest legal card that beats the current winner.
            Card? cheapest = null;
            for (int i = 0; i < _legal.Count; i++)
            {
                Card c = _legal[i];
                if (!TarneebRules.Beats(c, winningCard, trump)) continue;
                if (!cheapest.HasValue || Cheaper(c, cheapest.Value, trump)) cheapest = c;
            }
            if (cheapest.HasValue) return cheapest.Value;

            return follows ? LowestOfSuit(_legal, led) : LowestSideSuitDiscard(_legal, trump);
        }

        // ------------------------------------------------------------ helpers

        private static int CurrentWinner(TarneebEngine engine)
        {
            var trick = engine.State.CurrentTrick;
            int seat = trick.LeadSeat;
            int best = seat;
            Card bestCard = trick.CardsBySeat[seat].Value;
            for (int i = 1; i < trick.Count; i++)
            {
                seat = TarneebRules.NextSeat(seat, engine.Config);
                Card c = trick.CardsBySeat[seat].Value;
                if (TarneebRules.Beats(c, bestCard, engine.State.Trump))
                {
                    best = seat;
                    bestCard = c;
                }
            }
            return best;
        }

        /// <summary>True if a is a cheaper way to win than b (non-trump before trump, then lower rank).</summary>
        private static bool Cheaper(Card a, Card b, Suit trump)
        {
            bool aTrump = a.Suit == trump, bTrump = b.Suit == trump;
            if (aTrump != bTrump) return !aTrump;
            return a.Rank < b.Rank;
        }

        private bool OpponentVoid(int seat, Suit suit)
        {
            int left = (seat + 1) & 3, right = (seat + 3) & 3;
            return _tracker.IsVoid(left, suit) || _tracker.IsVoid(right, suit);
        }

        private static Card HighestOfSuit(List<Card> cards, Suit s)
        {
            Card best = default;
            bool found = false;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Suit != s) continue;
                if (!found || cards[i].Rank > best.Rank) { best = cards[i]; found = true; }
            }
            return best;
        }

        private static Card LowestOfSuit(List<Card> cards, Suit s)
        {
            Card best = default;
            bool found = false;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Suit != s) continue;
                if (!found || cards[i].Rank < best.Rank) { best = cards[i]; found = true; }
            }
            return best;
        }

        /// <summary>Discard the lowest card from the weakest non-trump suit; falls back to the lowest trump.</summary>
        private static Card LowestSideSuitDiscard(List<Card> cards, Suit trump)
        {
            var strength = new int[4];
            var count = new int[4];
            for (int i = 0; i < cards.Count; i++)
            {
                strength[(int)cards[i].Suit] += (int)cards[i].Rank;
                count[(int)cards[i].Suit]++;
            }

            int weakest = -1;
            for (int s = 0; s < 4; s++)
            {
                if (s == (int)trump || count[s] == 0) continue;
                if (weakest < 0 || strength[s] < strength[weakest]) weakest = s;
            }
            if (weakest >= 0) return LowestOfSuit(cards, (Suit)weakest);
            return LowestOfSuit(cards, trump);
        }
    }
}
