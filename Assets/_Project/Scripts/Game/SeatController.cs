using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Presentation;

namespace Tarneeb.Game
{
    public enum CommandKind { Bid, Pass, Trump, Play }

    public struct PlayerCommand
    {
        public CommandKind Kind;
        public int Amount;
        public Suit Suit;
        public Card Card;

        public static PlayerCommand MakeBid(int amount) => new PlayerCommand { Kind = CommandKind.Bid, Amount = amount };
        public static PlayerCommand MakePass() => new PlayerCommand { Kind = CommandKind.Pass };
        public static PlayerCommand MakeTrump(Suit s) => new PlayerCommand { Kind = CommandKind.Trump, Suit = s };
        public static PlayerCommand MakePlay(Card c) => new PlayerCommand { Kind = CommandKind.Play, Card = c };
    }

    /// <summary>
    /// Decides what a seat does on its turn. The match loop calls Begin once when the turn starts, then polls every
    /// frame until Poll returns true. Human, bot and remote seats share this contract, which is what keeps bot-fill and
    /// multiplayer cheap.
    /// </summary>
    public abstract class SeatController
    {
        public int Seat { get; }

        protected SeatController(int seat) { Seat = seat; }

        /// <summary>Called immediately for every engine event (not delayed by animations).</summary>
        public virtual void Observe(GameEvent e) { }

        /// <summary>This seat's turn has started; the engine state is current.</summary>
        public abstract void Begin(TarneebEngine engine, TableView table);

        /// <summary>Returns true, once, when the seat has decided. The controller hides its own input widgets.</summary>
        public abstract bool Poll(out PlayerCommand command);

        /// <summary>The turn was taken away (e.g. the master resolved it); hide any input widgets.</summary>
        public virtual void Cancel() { }
    }
}
