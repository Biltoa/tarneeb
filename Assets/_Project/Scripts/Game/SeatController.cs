using System;
using System.Collections;
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

    /// <summary>Decides what a seat does when it is that seat's turn. Human, bot and remote seats share this contract.</summary>
    public abstract class SeatController
    {
        public int Seat { get; }

        protected SeatController(int seat) { Seat = seat; }

        /// <summary>Called immediately for every engine event (not delayed by animations).</summary>
        public virtual void Observe(GameEvent e) { }

        /// <summary>Coroutine that ends after calling submit exactly once.</summary>
        public abstract IEnumerator Act(TarneebEngine engine, TableView table, Action<PlayerCommand> submit);
    }
}
