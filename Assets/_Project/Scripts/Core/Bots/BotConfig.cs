namespace Tarneeb.Core.Bots
{
    public sealed class BotConfig
    {
        /// <summary>Tricks the bot assumes its partner will contribute when bidding.</summary>
        public double PartnerAllowance = 3.0;
        /// <summary>Chance to play a random legal card instead of the heuristic choice.</summary>
        public double RandomMoveChance = 0.0;

        public double AceValue = 1.0;
        public double ProtectedKingValue = 0.7;
        public double BareKingValue = 0.3;
        public double ProtectedQueenValue = 0.3;
        public double LongSuitBonusPerCard = 0.5;
        public int LongSuitThreshold = 4;

        public static BotConfig Normal() => new BotConfig();
        public static BotConfig Easy() => new BotConfig { RandomMoveChance = 0.3 };
    }
}
