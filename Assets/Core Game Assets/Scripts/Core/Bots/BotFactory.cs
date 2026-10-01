namespace Tarneeb.Core.Bots
{
    public static class BotFactory
    {
        /// <summary>Heuristic bot for Easy/Normal, search bot for Hard.</summary>
        public static IBot Create(BotConfig config, int seed) =>
            config != null && config.Search ? (IBot)new SearchBot(config, seed) : new HeuristicBot(config, seed);
    }
}
