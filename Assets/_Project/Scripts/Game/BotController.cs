using Tarneeb.Core.Bots;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Presentation;

namespace Tarneeb.Game
{
    public sealed class BotController : SeatController
    {
        private readonly HeuristicBot _bot;
        private readonly System.Random _rng;
        private readonly bool _think;
        private PlayerCommand _decision;
        private double _readyAt;

        public BotController(int seat, BotConfig config, int seed, bool thinkDelay = true) : base(seat)
        {
            _bot = new HeuristicBot(config, seed);
            _rng = new System.Random(seed ^ 0x5bd1e995);
            _think = thinkDelay;
        }

        public override void Observe(GameEvent e) => _bot.Observe(e);

        public override void Begin(TarneebEngine engine, TableView table)
        {
            _decision = Decide(_bot, engine, Seat);
            // The think delay lives here (Unity layer) on purpose; the engine and the bot are instantaneous.
            float delay = _think ? (0.4f + (float)_rng.NextDouble() * 0.5f) / Tween.SpeedMultiplier : 0f;
            _readyAt = Clock.Seconds + delay;
        }

        public override bool Poll(out PlayerCommand command)
        {
            command = _decision;
            return Clock.Seconds >= _readyAt;
        }

        public static PlayerCommand Decide(IBot bot, TarneebEngine engine, int seat)
        {
            switch (engine.State.Phase)
            {
                case GamePhase.Bidding:
                {
                    BidDecision d = bot.ChooseBid(engine, seat);
                    return d.Pass ? PlayerCommand.MakePass() : PlayerCommand.MakeBid(d.Amount);
                }
                case GamePhase.ChoosingTrump:
                    return PlayerCommand.MakeTrump(bot.ChooseTrump(engine, seat));
                default:
                    return PlayerCommand.MakePlay(bot.ChooseCard(engine, seat));
            }
        }
    }
}
