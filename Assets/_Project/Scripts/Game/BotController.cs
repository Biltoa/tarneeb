using System;
using System.Collections;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Presentation;
using UnityEngine;

namespace Tarneeb.Game
{
    public sealed class BotController : SeatController
    {
        private readonly HeuristicBot _bot;
        private readonly System.Random _rng;
        private readonly bool _think;

        public BotController(int seat, BotConfig config, int seed, bool thinkDelay = true) : base(seat)
        {
            _bot = new HeuristicBot(config, seed);
            _rng = new System.Random(seed ^ 0x5bd1e995);
            _think = thinkDelay;
        }

        public override void Observe(GameEvent e) => _bot.Observe(e);

        public override IEnumerator Act(TarneebEngine engine, TableView table, Action<PlayerCommand> submit)
        {
            // Think delay lives here (Unity layer) on purpose; the engine and bot are instantaneous.
            if (_think) yield return Tween.Wait(0.4f + (float)_rng.NextDouble() * 0.5f);
            submit(Decide(_bot, engine, Seat));
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
