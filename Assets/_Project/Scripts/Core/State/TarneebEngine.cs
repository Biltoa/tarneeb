using System;
using System.Collections.Generic;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;

namespace Tarneeb.Core.State
{
    /// <summary>
    /// Single source of truth for a game. Takes commands, validates them, mutates state and emits events.
    /// Illegal commands return a failed CommandResult; nothing throws for ordinary illegal moves.
    /// </summary>
    public sealed class TarneebEngine
    {
        private readonly Random _rng;
        private readonly List<Card> _legalScratch = new List<Card>(13);

        private bool _replica;
        private int _replicaLocalSeat = -1;

        public RulesConfig Config { get; }
        public GameState State { get; } = new GameState();

        /// <summary>
        /// True for a client-side mirror of an authoritative game: only the local seat's hand is known. Commands are
        /// the master's already-validated ones, and hidden cards are learned as they are played.
        /// </summary>
        public bool IsReplica => _replica;

        public event Action<GameEvent> EventEmitted;

        public TarneebEngine(RulesConfig config, int seed)
        {
            Config = config ?? new RulesConfig();
            _rng = new Random(seed);
        }

        // ---------------------------------------------------------------- game flow

        /// <summary>Starts a new game (random first dealer unless one is given) and deals round one.</summary>
        public void StartGame(int? firstDealer = null)
        {
            State.Dealer = firstDealer ?? _rng.Next(4);
            State.RoundNumber = 0;
            State.TeamScores[0] = 0;
            State.TeamScores[1] = 0;
            State.WinningTeam = -1;
            State.LastRound = null;
            BeginRound();
        }

        /// <summary>
        /// Starts a replica game: same flow as StartGame but nothing is shuffled; the local seat's hand must be
        /// supplied through ReplicaHandProvider.
        /// </summary>
        public void StartReplicaGame(int dealer, int localSeat)
        {
            _replica = true;
            _replicaLocalSeat = localSeat;
            StartGame(dealer);
        }

        /// <summary>Replica only: supplies the local seat's hand each time a round is dealt (in stream order).</summary>
        public Func<IReadOnlyList<Card>> ReplicaHandProvider;

        /// <summary>Starts the next round after RoundOver; the dealer rotates in play direction.</summary>
        public CommandResult StartNextRound()
        {
            if (State.Phase != GamePhase.RoundOver) return CommandResult.Fail(CommandError.WrongPhase);
            State.Dealer = TarneebRules.NextSeat(State.Dealer, Config);
            BeginRound();
            return CommandResult.Ok;
        }

        /// <summary>
        /// Scenario/replay entry point: starts round one with fixed hands (13 cards each) and dealer.
        /// </summary>
        public void StartRoundWithHands(Card[][] hands, int dealer)
        {
            State.Dealer = dealer;
            State.RoundNumber = 1;
            State.TeamScores[0] = 0;
            State.TeamScores[1] = 0;
            State.WinningTeam = -1;
            for (int s = 0; s < 4; s++)
            {
                State.Hands[s].Clear();
                for (int i = 0; i < hands[s].Length; i++) State.Hands[s].Add(hands[s][i]);
            }
            EnterBidding();
        }

        private void BeginRound()
        {
            State.RoundNumber++;
            DealCards();
            EnterBidding();
        }

        private void DealCards()
        {
            if (_replica)
            {
                for (int s = 0; s < 4; s++) State.Hands[s].Clear();
                IReadOnlyList<Card> mine = ReplicaHandProvider?.Invoke();
                if (mine != null)
                {
                    for (int i = 0; i < mine.Count; i++) State.Hands[_replicaLocalSeat].Add(mine[i]);
                    State.Hands[_replicaLocalSeat].SortForDisplay();
                }
                return;
            }

            var deck = Deck.Create();
            Deck.Shuffle(deck, _rng);
            for (int s = 0; s < 4; s++)
            {
                State.Hands[s].Clear();
                for (int i = 0; i < 13; i++) State.Hands[s].Add(deck[s * 13 + i]);
                State.Hands[s].SortForDisplay();
            }
        }

        private void EnterBidding()
        {
            for (int i = 0; i < 4; i++)
            {
                State.Bids[i] = 0;
                State.Passed[i] = false;
            }
            State.HighBid = 0;
            State.HighBidder = -1;
            State.Declarer = -1;
            State.TrumpChosen = false;
            State.TricksCompleted = 0;
            State.TricksByTeam[0] = 0;
            State.TricksByTeam[1] = 0;
            State.CurrentTrick.Reset(0);
            State.Phase = GamePhase.Bidding;
            State.CurrentSeat = TarneebRules.NextSeat(State.Dealer, Config);
            Emit(new CardsDealt { Dealer = State.Dealer, RoundNumber = State.RoundNumber });
            Emit(new TurnChanged { Seat = State.CurrentSeat, Phase = State.Phase });
        }

        // ---------------------------------------------------------------- bidding

        public int MinimumLegalBid() => Math.Max(Config.MinimumBid, State.HighBid + 1);

        public bool CanBid(int seat) =>
            State.Phase == GamePhase.Bidding && seat == State.CurrentSeat && !State.Passed[seat]
            && MinimumLegalBid() <= Config.MaximumBid;

        public CommandResult PlaceBid(int seat, int amount)
        {
            var check = CheckTurn(seat, GamePhase.Bidding);
            if (!check.Success) return check;
            if (State.Passed[seat]) return CommandResult.Fail(CommandError.AlreadyPassed);
            if (amount < Config.MinimumBid) return CommandResult.Fail(CommandError.BidBelowMinimum);
            if (amount > Config.MaximumBid) return CommandResult.Fail(CommandError.BidAboveMaximum);
            if (amount <= State.HighBid) return CommandResult.Fail(CommandError.BidNotHigher);

            State.Bids[seat] = amount;
            State.HighBid = amount;
            State.HighBidder = seat;
            Emit(new BidPlaced { Seat = seat, Amount = amount });
            AdvanceBidding();
            return CommandResult.Ok;
        }

        public CommandResult Pass(int seat)
        {
            var check = CheckTurn(seat, GamePhase.Bidding);
            if (!check.Success) return check;
            if (State.Passed[seat]) return CommandResult.Fail(CommandError.AlreadyPassed);

            State.Passed[seat] = true;
            Emit(new PlayerPassed { Seat = seat });
            AdvanceBidding();
            return CommandResult.Ok;
        }

        private void AdvanceBidding()
        {
            int passed = State.PassedCount();

            if (State.HighBidder >= 0 && passed == 3)
            {
                State.Declarer = State.HighBidder;
                State.Phase = GamePhase.ChoosingTrump;
                State.CurrentSeat = State.Declarer;
                Emit(new BiddingWon { Declarer = State.Declarer, Bid = State.HighBid });
                Emit(new TurnChanged { Seat = State.CurrentSeat, Phase = State.Phase });
                return;
            }

            if (State.HighBidder < 0 && passed == 4 && !Config.RedealOnAllPass)
            {
                // Variant: the dealer is stuck with the minimum bid.
                State.Bids[State.Dealer] = Config.MinimumBid;
                State.HighBid = Config.MinimumBid;
                State.HighBidder = State.Dealer;
                State.Declarer = State.Dealer;
                State.Phase = GamePhase.ChoosingTrump;
                State.CurrentSeat = State.Declarer;
                Emit(new BiddingWon { Declarer = State.Declarer, Bid = State.HighBid });
                Emit(new TurnChanged { Seat = State.CurrentSeat, Phase = State.Phase });
                return;
            }

            if (State.HighBidder < 0 && passed == 4)
            {
                State.Dealer = TarneebRules.NextSeat(State.Dealer, Config);
                Emit(new RoundRedealt { NewDealer = State.Dealer });
                DealCards();
                EnterBidding();
                return;
            }

            int next = State.CurrentSeat;
            for (int i = 0; i < 4; i++)
            {
                next = TarneebRules.NextSeat(next, Config);
                if (!State.Passed[next]) break;
            }
            State.CurrentSeat = next;
            Emit(new TurnChanged { Seat = next, Phase = State.Phase });
        }

        // ---------------------------------------------------------------- trump

        public CommandResult ChooseTrump(int seat, Suit suit)
        {
            if (seat < 0 || seat > 3) return CommandResult.Fail(CommandError.InvalidSeat);
            if (State.Phase != GamePhase.ChoosingTrump) return CommandResult.Fail(CommandError.WrongPhase);
            if (seat != State.Declarer) return CommandResult.Fail(CommandError.NotYourTurn);
            if ((int)suit < 0 || (int)suit > 3) return CommandResult.Fail(CommandError.InvalidSuit);

            State.Trump = suit;
            State.TrumpChosen = true;
            State.Phase = GamePhase.Playing;
            int leader = Config.DeclarerLeadsFirstTrick ? State.Declarer : TarneebRules.NextSeat(State.Dealer, Config);
            State.CurrentTrick.Reset(leader);
            State.CurrentSeat = leader;
            Emit(new TrumpChosen { Seat = seat, Suit = suit });
            Emit(new TurnChanged { Seat = leader, Phase = State.Phase });
            return CommandResult.Ok;
        }

        // ---------------------------------------------------------------- play

        /// <summary>Fills result with the cards seat may legally play right now (empty if it is not their turn to play).</summary>
        public void GetLegalCards(int seat, List<Card> result)
        {
            result.Clear();
            if (State.Phase != GamePhase.Playing || seat != State.CurrentSeat) return;
            TarneebRules.GetLegalCards(State.Hands[seat], State.CurrentTrick.LedSuit, result);
        }

        public CommandResult PlayCard(int seat, Card card)
        {
            var check = CheckTurn(seat, GamePhase.Playing);
            if (!check.Success) return check;
            var hand = State.Hands[seat];
            bool hidden = _replica && seat != _replicaLocalSeat;
            if (hidden && !hand.Contains(card)) hand.Add(card);   // learn the card as it is played
            if (!hand.Contains(card)) return CommandResult.Fail(CommandError.CardNotInHand);

            Suit? led = State.CurrentTrick.LedSuit;
            if (!hidden && led.HasValue && card.Suit != led.Value && hand.HasSuit(led.Value))
                return CommandResult.Fail(CommandError.MustFollowSuit);

            hand.Remove(card);
            var trick = State.CurrentTrick;
            trick.CardsBySeat[seat] = card;
            trick.Count++;
            Emit(new CardPlayed
            {
                Seat = seat,
                Card = card,
                LedSuit = trick.LedSuit.Value,
                TrickNumber = State.TricksCompleted + 1
            });

            if (trick.Count < 4)
            {
                State.CurrentSeat = TarneebRules.NextSeat(seat, Config);
                Emit(new TurnChanged { Seat = State.CurrentSeat, Phase = State.Phase });
                return CommandResult.Ok;
            }

            ResolveTrick();
            return CommandResult.Ok;
        }

        private void ResolveTrick()
        {
            var trick = State.CurrentTrick;
            var cards = new Card[4];
            for (int i = 0; i < 4; i++) cards[i] = trick.CardsBySeat[i].Value;

            int winner = TarneebRules.TrickWinner(cards, trick.LeadSeat, State.Trump, Config);
            State.TricksCompleted++;
            State.TricksByTeam[TarneebRules.TeamOf(winner)]++;
            Emit(new TrickWon
            {
                Winner = winner,
                TrickNumber = State.TricksCompleted,
                LeadSeat = trick.LeadSeat,
                CardsBySeat = cards
            });

            if (State.TricksCompleted == TarneebRules.TricksPerRound)
            {
                EndRound();
                return;
            }

            trick.Reset(winner);
            State.CurrentSeat = winner;
            Emit(new TurnChanged { Seat = winner, Phase = State.Phase });
        }

        private void EndRound()
        {
            var summary = TarneebRules.ScoreRound(State.Declarer, State.HighBid, State.Trump, State.TricksByTeam, State.TeamScores, Config);
            State.TeamScores[0] = summary.TeamScoresAfter[0];
            State.TeamScores[1] = summary.TeamScoresAfter[1];
            State.LastRound = summary;
            State.Phase = GamePhase.RoundOver;
            Emit(new RoundEnded { Summary = summary });

            int winner = TarneebRules.DetermineGameWinner(State.TeamScores, summary.DeclaringTeam, Config);
            if (winner >= 0)
            {
                State.WinningTeam = winner;
                State.Phase = GamePhase.GameOver;
                Emit(new GameEnded { WinningTeam = winner, Scores = new[] { State.TeamScores[0], State.TeamScores[1] } });
            }
        }

        // ---------------------------------------------------------------- helpers

        private CommandResult CheckTurn(int seat, GamePhase expected)
        {
            if (seat < 0 || seat > 3) return CommandResult.Fail(CommandError.InvalidSeat);
            if (State.Phase != expected) return CommandResult.Fail(CommandError.WrongPhase);
            if (seat != State.CurrentSeat) return CommandResult.Fail(CommandError.NotYourTurn);
            return CommandResult.Ok;
        }

        private void Emit(GameEvent e) => EventEmitted?.Invoke(e);
    }
}
