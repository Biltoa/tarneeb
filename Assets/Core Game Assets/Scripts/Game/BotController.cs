using Tarneeb.Core.Bots;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Presentation;

namespace Tarneeb.Game
{
    public sealed class BotController : SeatController
    {
        private IBot _bot;
        private readonly int _seed;
        private readonly System.Random _rng;
        private readonly System.Collections.Generic.List<GameEvent> _roundEvents = new System.Collections.Generic.List<GameEvent>();
        private readonly bool _think;
        private PlayerCommand _decision;
        private double _readyAt;

        public BotController(int seat, BotConfig config, int seed, bool thinkDelay = true) : base(seat)
        {
            _seed = seed;
            Config = config;
            _bot = BotFactory.Create(config, seed);
            _rng = new System.Random(seed ^ 0x5bd1e995);
            _think = thinkDelay;
        }

        /// <summary>The skill this bot currently plays at.</summary>
        public BotConfig Config { get; private set; }
        public IBot Bot => _bot;

        public override void Observe(GameEvent e)
        {
            if (e is CardsDealt) _roundEvents.Clear();
            _roundEvents.Add(e);                                   // kept for the round so a new bot can catch up
            _bot.Observe(e);
        }

        /// <summary>
        /// Changes the bot's skill in the middle of a game: a new bot of the new kind replays this round's events (so it knows which cards were
        /// played and who showed a void) and plays from the next decision on. A decision already made for the current turn is kept.
        /// </summary>
        public void Reconfigure(BotConfig config)
        {
            Config = config;
            _bot = BotFactory.Create(config, _seed + 17 * (++_generation));
            for (int i = 0; i < _roundEvents.Count; i++) _bot.Observe(_roundEvents[i]);
        }

        private int _generation;

        /// <summary>Decides immediately for the current engine state (no think delay).</summary>
        public PlayerCommand DecideNow(TarneebEngine engine) => Decide(_bot, engine, Seat);

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
