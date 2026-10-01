using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;

namespace Tarneeb.Core.Bots
{
    public readonly struct BidDecision
    {
        public readonly bool Pass;
        public readonly int Amount;

        private BidDecision(bool pass, int amount)
        {
            Pass = pass;
            Amount = amount;
        }

        public static BidDecision PassBid() => new BidDecision(true, 0);
        public static BidDecision Bid(int amount) => new BidDecision(false, amount);
    }

    /// <summary>A bot only sees its own hand (via the engine state for its seat) plus public events.</summary>
    public interface IBot
    {
        void Observe(GameEvent e);
        BidDecision ChooseBid(TarneebEngine engine, int seat);
        Suit ChooseTrump(TarneebEngine engine, int seat);
        Card ChooseCard(TarneebEngine engine, int seat);
    }
}
