using System;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Core.Bots
{
    /// <summary>
    /// The "Hard" bot: determinized Monte Carlo search. It only uses what a real player knows (own hand, played cards, suits other seats
    /// have shown void in, the contract). Each decision deals the unseen cards to the other seats in many plausible ways that respect
    /// the known voids, plays every candidate out to the end of the round with the heuristic policy, and keeps the candidate with the
    /// best average score swing for its team. Bidding and trump choice use the same simulation.
    /// </summary>
    public sealed class SearchBot : IBot
    {
        private const ulong Full = (1UL << 52) - 1;
        private const double BidMargin = 0.6;

        private readonly BotConfig _cfg;
        private readonly HeuristicBot _heuristic;
        private readonly Random _rng;
        private readonly List<Card> _legal = new List<Card>(13);

        // scratch (single-threaded)
        private readonly ulong[] _dealt = new ulong[4];
        private readonly ulong[] _work = new ulong[4];
        private readonly int[] _trick = new int[4];
        private readonly int[] _tricks = new int[2];

        public SearchBot(BotConfig config, int seed)
        {
            _cfg = config ?? BotConfig.Hard();
            _heuristic = new HeuristicBot(BotConfig.Normal(), seed);
            _rng = new Random(seed * 31 + 7);
        }

        public void Observe(GameEvent e) => _heuristic.Observe(e);

        // ------------------------------------------------------------ bidding and trump

        public BidDecision ChooseBid(TarneebEngine engine, int seat)
        {
            int next = engine.MinimumLegalBid();
            var st = engine.State;
            if (next > engine.Config.MaximumBid) return BidDecision.PassBid();
            if (st.HighBidder >= 0 && TarneebRules.TeamOf(st.HighBidder) == TarneebRules.TeamOf(seat)) return BidDecision.PassBid();   // never outbid the partner

            ulong hand = HandMask(st.Hands[seat]);
            int n = _cfg.SearchSamples;
            double bestEv = double.NegativeInfinity;
            var ev = new double[4];
            for (int s = 0; s < n; s++)
            {
                DealUnseen(Full & ~hand, seat, st, null, hand, forBidding: true);
                for (int suit = 0; suit < 4; suit++)
                {
                    Array.Copy(_dealt, _work, 4);
                    _tricks[0] = _tricks[1] = 0;
                    for (int i = 0; i < 4; i++) _trick[i] = -1;
                    Rollout(_work, 0, _trick, 0, seat, seat, suit, seat, engine.Config);
                    int t = _tricks[TarneebRules.TeamOf(seat)];
                    ev[suit] += Score(t, next, engine.Config);
                }
            }
            for (int suit = 0; suit < 4; suit++) bestEv = Math.Max(bestEv, ev[suit] / n);
            return bestEv > BidMargin ? BidDecision.Bid(next) : BidDecision.PassBid();
        }

        public Suit ChooseTrump(TarneebEngine engine, int seat)
        {
            var st = engine.State;
            ulong hand = HandMask(st.Hands[seat]);
            int n = _cfg.SearchSamples * 2;
            var ev = new double[4];
            for (int s = 0; s < n; s++)
            {
                DealUnseen(Full & ~hand, seat, st, null, hand, forBidding: true);
                for (int suit = 0; suit < 4; suit++)
                {
                    Array.Copy(_dealt, _work, 4);
                    _tricks[0] = _tricks[1] = 0;
                    for (int i = 0; i < 4; i++) _trick[i] = -1;
                    Rollout(_work, 0, _trick, 0, seat, seat, suit, seat, engine.Config);
                    ev[suit] += Score(_tricks[TarneebRules.TeamOf(seat)], st.HighBid, engine.Config);
                }
            }
            int best = 0;
            for (int suit = 1; suit < 4; suit++) if (ev[suit] > ev[best]) best = suit;
            return (Suit)best;
        }

        // ------------------------------------------------------------ card play

        public Card ChooseCard(TarneebEngine engine, int seat)
        {
            engine.GetLegalCards(seat, _legal);
            if (_legal.Count == 1) return _legal[0];
            Card heur = _heuristic.ChooseCard(engine, seat);

            var st = engine.State;
            var trickState = st.CurrentTrick;
            ulong hand = HandMask(st.Hands[seat]);
            ulong played = 0;
            for (int i = 0; i < 52; i++) if (_heuristic.Tracker.IsPlayed(Card.FromByte((byte)i))) played |= 1UL << i;
            int count = trickState.Count;
            for (int s = 0; s < 4; s++) _trick[s] = trickState.CardsBySeat[s].HasValue ? trickState.CardsBySeat[s].Value.ToByte() : -1;

            // candidates: equivalent cards (adjacent ranks with no unseen card between them) are searched once, as the lowest of the run
            ulong unseen = Full & ~played & ~hand;
            var cand = new List<int>(13);
            for (int i = 0; i < _legal.Count; i++) cand.Add(_legal[i].ToByte());
            cand.Sort();
            for (int i = cand.Count - 1; i > 0; i--)
            {
                int hi = cand[i], lo = cand[i - 1];
                if (hi / 13 != lo / 13 || hi == heur.ToByte()) continue;
                bool gap = false;
                for (int x = lo + 1; x < hi; x++) if (((unseen >> x) & 1) != 0) { gap = true; break; }
                if (!gap) cand.RemoveAt(i);
            }
            if (cand.Count == 1) return Card.FromByte((byte)cand[0]);

            int remainingPlays = st.Hands[seat].Count * 4;
            int n = Math.Max(24, Math.Min(_cfg.SearchSamples * 3, _cfg.SearchBudget / Math.Max(1, cand.Count * remainingPlays)));
            int declarer = st.Declarer, bid = st.HighBid, myTeam = TarneebRules.TeamOf(seat);
            int trump = (int)st.Trump;
            int declTeam = TarneebRules.TeamOf(declarer);
            var util = new double[cand.Count];

            for (int s = 0; s < n; s++)
            {
                if (!DealUnseen(unseen, seat, st, trickState, hand, false)) return heur;
                for (int k = 0; k < cand.Count; k++)
                {
                    Array.Copy(_dealt, _work, 4);
                    int c = cand[k];
                    Array.Copy(_trick, _trickBuf, 4);
                    _trickBuf[seat] = c;
                    _work[seat] &= ~(1UL << c);
                    _tricks[0] = st.TricksByTeam[0]; _tricks[1] = st.TricksByTeam[1];
                    Rollout(_work, played | (1UL << c), _trickBuf, count + 1, trickState.LeadSeat, TarneebRules.NextSeat(seat, engine.Config), trump, declarer, engine.Config);
                    double sc = Score(_tricks[declTeam], bid, engine.Config);
                    util[k] += myTeam == declTeam ? sc : -sc;
                }
            }
            int bestK = 0;
            for (int k = 0; k < cand.Count; k++)
            {
                double u = util[k] + (cand[k] == heur.ToByte() ? 1e-6 : 0);
                double b = util[bestK] + (cand[bestK] == heur.ToByte() ? 1e-6 : 0);
                if (u > b) bestK = k;
            }
            return Card.FromByte((byte)cand[bestK]);
        }

        // ------------------------------------------------------------ simulation

        private readonly int[] _trickBuf = new int[4];

        private static ulong HandMask(Hand h)
        {
            ulong m = 0;
            for (int i = 0; i < h.Count; i++) m |= 1UL << h.Cards[i].ToByte();
            return m;
        }

        /// <summary>Score swing of the declaring team for a finished round (declaring minus defending), like TarneebRules.ScoreRound.</summary>
        private static double Score(int declTricks, int bid, RulesConfig cfg)
        {
            int defTricks = 13 - declTricks;
            if (declTricks >= bid)
            {
                int decl = bid == cfg.MaximumBid && declTricks == 13 ? cfg.KabootBonusScore : declTricks;
                return decl - (cfg.DefendersScoreWhenContractMade ? defTricks : 0);
            }
            return -bid - defTricks;
        }

        /// <summary>
        /// Randomly deals the unseen cards to the three other seats, honouring known voids and each seat's remaining card count.
        /// Result in _dealt (own seat = own hand). Falls back to ignoring voids if the constraints cannot be met.
        /// </summary>
        private bool DealUnseen(ulong unseen, int me, GameState st, TrickState trick, ulong ownHand, bool forBidding)
        {
            var cards = new int[52];
            int n = 0;
            for (int i = 0; i < 52; i++) if (((unseen >> i) & 1) != 0) cards[n++] = i;
            var need = new int[4];
            int total = 0;
            int myCount = forBidding ? 13 : st.Hands[me].Count;
            for (int s = 0; s < 4; s++)
            {
                if (s == me) continue;
                need[s] = forBidding ? 13 : myCount - (trick != null && trick.CardsBySeat[s].HasValue ? 1 : 0);
                total += need[s];
            }
            if (total != n) return false;

            var tracker = _heuristic.Tracker;
            var left = new int[4];
            for (int attempt = 0; attempt < 12; attempt++)
            {
                bool strict = attempt < 10;
                Shuffle(cards, n);
                Array.Copy(need, left, 4);
                for (int s = 0; s < 4; s++) _dealt[s] = 0;
                _dealt[me] = ownHand;
                bool ok = true;
                // hardest cards first: a card that only one seat can take is placed before the free ones
                for (int i = 0; i < n && ok; i++)
                {
                    int c = cards[i], suit = c / 13;
                    int a = -1, b = -1, cnt = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        if (s == me || left[s] == 0) continue;
                        if (strict && tracker.IsVoid(s, (Suit)suit)) continue;
                        if (cnt == 0) a = s; else if (cnt == 1) b = s; else { }
                        cnt++;
                    }
                    if (cnt == 0) { ok = false; break; }
                    int pick;
                    if (cnt == 1) pick = a;
                    else
                    {
                        // weight by remaining need so seats fill evenly
                        int wsum = 0;
                        for (int s = 0; s < 4; s++) if (s != me && left[s] > 0 && !(strict && tracker.IsVoid(s, (Suit)suit))) wsum += left[s];
                        int r = _rng.Next(wsum);
                        pick = -1;
                        for (int s = 0; s < 4; s++)
                        {
                            if (s == me || left[s] == 0 || (strict && tracker.IsVoid(s, (Suit)suit))) continue;
                            if (r < left[s]) { pick = s; break; }
                            r -= left[s];
                        }
                    }
                    _dealt[pick] |= 1UL << c;
                    left[pick]--;
                }
                if (ok) return true;
            }
            return false;
        }

        private void Shuffle(int[] a, int n)
        {
            for (int i = n - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int t = a[i]; a[i] = a[j]; a[j] = t;
            }
        }

        /// <summary>
        /// Plays the round out with the heuristic policy. hands are per-seat masks; trick holds the cards already on the table
        /// (count of them, led by 'leader', 'cur' to play next). Adds the tricks won to _tricks.
        /// </summary>
        private void Rollout(ulong[] hands, ulong played, int[] trick, int count, int leader, int cur, int trump, int declarer, RulesConfig cfg)
        {
            int step = cfg.Counterclockwise ? 1 : 3;
            while (true)
            {
                if (count == 4)
                {
                    int w = leader, best = trick[leader], seat = leader;
                    for (int i = 1; i < 4; i++)
                    {
                        seat = (seat + step) & 3;
                        if (Beats(trick[seat], best, trump)) { best = trick[seat]; w = seat; }
                    }
                    _tricks[w & 1]++;
                    if (_tricks[0] + _tricks[1] >= 13) return;
                    for (int i = 0; i < 4; i++) trick[i] = -1;
                    count = 0; leader = cur = w;
                    continue;
                }
                int c = Pick(hands[cur], played, trick, count, leader, cur, trump, step, declarer);
                hands[cur] &= ~(1UL << c);
                played |= 1UL << c;
                trick[cur] = c;
                count++;
                cur = (cur + step) & 3;
            }
        }

        private static bool Beats(int challenger, int best, int trump)
        {
            int cs = challenger / 13, bs = best / 13;
            if (cs == bs) return challenger > best;
            return cs == trump && bs != trump;
        }

        /// <summary>The rollout policy: the same rules of thumb as HeuristicBot, on bitmasks.</summary>
        private static int Pick(ulong hand, ulong played, int[] trick, int count, int leader, int seat, int trump, int step, int declarer)
        {
            ulong unplayed = Full & ~played;
            ulong trumpMask = 0x1FFFUL << (trump * 13);
            if (count == 0)
            {
                if (seat == declarer)
                {
                    ulong tr = hand & trumpMask;
                    if (PopCount(tr) >= 4 && ((unplayed & ~hand) & trumpMask) != 0) return Msb(tr);
                }
                int best = -1, bestScore = -1;
                for (int s = 0; s < 4; s++)
                {
                    ulong sm = 0x1FFFUL << (s * 13);
                    ulong mine = hand & sm;
                    if (mine == 0) continue;
                    int top = Msb(unplayed & sm);
                    if (((hand >> top) & 1) == 0) continue;
                    int score = (s == trump ? 0 : 100) + PopCount(mine);
                    if (score > bestScore) { bestScore = score; best = top; }
                }
                if (best >= 0) return best;
                int low = -1, lowRank = 99;
                for (int s = 0; s < 4; s++)
                {
                    if (s == trump) continue;
                    ulong mine = hand & (0x1FFFUL << (s * 13));
                    if (mine == 0) continue;
                    int c = Lsb(mine);
                    if (c % 13 < lowRank) { lowRank = c % 13; low = c; }
                }
                return low >= 0 ? low : Lsb(hand & trumpMask);
            }

            int led = trick[leader] / 13;
            ulong ledMask = 0x1FFFUL << (led * 13);
            bool follows = (hand & ledMask) != 0;
            ulong legal = follows ? hand & ledMask : hand;

            int winSeat = leader, winCard = trick[leader], sq = leader;
            for (int i = 1; i < count; i++)
            {
                sq = (sq + step) & 3;
                if (Beats(trick[sq], winCard, trump)) { winCard = trick[sq]; winSeat = sq; }
            }
            bool partnerWinning = ((winSeat ^ seat) & 1) == 0;
            if (!partnerWinning)
            {
                int wsuit = winCard / 13;
                ulong beat;
                if (wsuit == trump) beat = legal & trumpMask & ~((2UL << winCard) - 1);
                else
                {
                    beat = legal & (0x1FFFUL << (wsuit * 13)) & ~((2UL << winCard) - 1);
                    if (beat == 0) beat = legal & trumpMask;                     // ruff (legal only contains trump here when void)
                }
                if (beat != 0)
                {
                    // cheapest winner: non-trump before trump, then lowest
                    ulong nt = beat & ~trumpMask;
                    return nt != 0 ? Lsb(nt) : Lsb(beat);
                }
            }
            if (follows) return Lsb(legal);
            return WeakestDiscard(hand, trump);
        }

        private static int WeakestDiscard(ulong hand, int trump)
        {
            int weakest = -1, weakSum = int.MaxValue;
            for (int s = 0; s < 4; s++)
            {
                if (s == trump) continue;
                ulong mine = hand & (0x1FFFUL << (s * 13));
                if (mine == 0) continue;
                int sum = 0;
                for (ulong m = mine; m != 0; m &= m - 1) sum += Lsb(m) % 13 + 2;
                if (sum < weakSum) { weakSum = sum; weakest = s; }
            }
            if (weakest >= 0) return Lsb(hand & (0x1FFFUL << (weakest * 13)));
            return Lsb(hand);
        }

        private static int PopCount(ulong x) { int n = 0; while (x != 0) { x &= x - 1; n++; } return n; }

        private static int Lsb(ulong x)
        {
            int n = 0;
            if ((x & 0xFFFFFFFFUL) == 0) { n += 32; x >>= 32; }
            if ((x & 0xFFFFUL) == 0) { n += 16; x >>= 16; }
            if ((x & 0xFFUL) == 0) { n += 8; x >>= 8; }
            if ((x & 0xFUL) == 0) { n += 4; x >>= 4; }
            if ((x & 0x3UL) == 0) { n += 2; x >>= 2; }
            if ((x & 0x1UL) == 0) n += 1;
            return n;
        }

        private static int Msb(ulong x)
        {
            int n = 0;
            if ((x >> 32) != 0) { n += 32; x >>= 32; }
            if ((x >> 16) != 0) { n += 16; x >>= 16; }
            if ((x >> 8) != 0) { n += 8; x >>= 8; }
            if ((x >> 4) != 0) { n += 4; x >>= 4; }
            if ((x >> 2) != 0) { n += 2; x >>= 2; }
            if ((x >> 1) != 0) n += 1;
            return n;
        }
    }
}
