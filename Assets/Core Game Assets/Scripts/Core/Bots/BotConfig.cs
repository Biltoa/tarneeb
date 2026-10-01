namespace Tarneeb.Core.Bots
{
    public sealed class BotConfig
    {
        /// <summary>Tricks the bot assumes its partner will contribute when bidding.</summary>
        public double PartnerAllowance = 5.0;
        /// <summary>Chance to play a random legal card instead of the heuristic choice.</summary>
        public double RandomMoveChance = 0.0;

        public double AceValue = 1.0;
        public double ProtectedKingValue = 0.7;
        public double BareKingValue = 0.3;
        public double ProtectedQueenValue = 0.3;
        public double LongSuitBonusPerCard = 0.5;
        public int LongSuitThreshold = 4;

        /// <summary>Hard: determinized Monte Carlo search (SearchBot). Samples per decision and a play budget per card decision.</summary>
        public bool Search;
        public int SearchSamples = 48;
        public int SearchBudget = 90000;

        public static BotConfig Normal() => new BotConfig();
        public static BotConfig Hard() => new BotConfig { Search = true };
        public static BotConfig Easy() => new BotConfig { RandomMoveChance = 0.3 };
    }
}
