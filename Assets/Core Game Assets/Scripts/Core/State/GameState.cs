using Tarneeb.Core.Cards;

namespace Tarneeb.Core.State
{
    public enum GamePhase
    {
        NotStarted,
        Bidding,
        ChoosingTrump,
        Playing,
        RoundOver,
        GameOver
    }

    public sealed class TrickState
    {
        public int LeadSeat;
        public readonly Card?[] CardsBySeat = new Card?[4];
        public int Count;

        public Suit? LedSuit => Count > 0 ? CardsBySeat[LeadSeat].Value.Suit : (Suit?)null;

        public void Reset(int leader)
        {
            LeadSeat = leader;
            Count = 0;
            for (int i = 0; i < 4; i++) CardsBySeat[i] = null;
        }
    }

    public sealed class GameState
    {
        public GamePhase Phase = GamePhase.NotStarted;
        public int Dealer;
        public int RoundNumber;
        public int CurrentSeat;

        public readonly Hand[] Hands = { new Hand(), new Hand(), new Hand(), new Hand() };

        // Bidding
        public readonly int[] Bids = new int[4];       // last bid by seat, 0 = none
        public readonly bool[] Passed = new bool[4];
        public int HighBid;
        public int HighBidder = -1;

        // Contract
        public int Declarer = -1;
        public bool TrumpChosen;
        public Suit Trump;

        // Play
        public readonly TrickState CurrentTrick = new TrickState();
        public int TricksCompleted;
        public readonly int[] TricksByTeam = new int[2];

        // Match
        public readonly int[] TeamScores = new int[2];
        public int WinningTeam = -1;
        public Rules.RoundSummary LastRound;

        public int PassedCount()
        {
            int n = 0;
            for (int i = 0; i < 4; i++) if (Passed[i]) n++;
            return n;
        }
    }
}
