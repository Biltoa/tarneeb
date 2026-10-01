using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Core.Events
{
    public abstract class GameEvent { }

    public sealed class CardsDealt : GameEvent
    {
        public int Dealer;
        public int RoundNumber;
    }

    public sealed class TurnChanged : GameEvent
    {
        public int Seat;
        public GamePhase Phase;
    }

    public sealed class BidPlaced : GameEvent
    {
        public int Seat;
        public int Amount;
    }

    public sealed class PlayerPassed : GameEvent
    {
        public int Seat;
    }

    public sealed class BiddingWon : GameEvent
    {
        public int Declarer;
        public int Bid;
    }

    /// <summary>All four passed; the round is dealt again by NewDealer (a CardsDealt follows).</summary>
    public sealed class RoundRedealt : GameEvent
    {
        public int NewDealer;
    }

    public sealed class TrumpChosen : GameEvent
    {
        public int Seat;
        public Suit Suit;
    }

    public sealed class CardPlayed : GameEvent
    {
        public int Seat;
        public Card Card;
        public Suit LedSuit;
        public int TrickNumber;
    }

    public sealed class TrickWon : GameEvent
    {
        public int Winner;
        public int TrickNumber;
        public int LeadSeat;
        public Card[] CardsBySeat;
    }

    public sealed class RoundEnded : GameEvent
    {
        public RoundSummary Summary;
    }

    public sealed class GameEnded : GameEvent
    {
        public int WinningTeam;
        public int[] Scores;
    }
}
