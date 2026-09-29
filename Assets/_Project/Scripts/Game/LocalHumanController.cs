using System;
using System.Collections;
using System.Collections.Generic;
using Tarneeb.Config;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.State;
using Tarneeb.Presentation;
using UnityEngine;

namespace Tarneeb.Game
{
    public sealed class LocalHumanController : SeatController
    {
        private readonly HeuristicBot _fallback;
        private readonly List<Card> _legal = new List<Card>(13);

        public LocalHumanController(int seat, int seed) : base(seat)
        {
            _fallback = new HeuristicBot(BotConfig.Normal(), seed);
        }

        public override void Observe(GameEvent e) => _fallback.Observe(e);

        public override IEnumerator Act(TarneebEngine engine, TableView table, Action<PlayerCommand> submit)
        {
            bool done = false;
            PlayerCommand cmd = default;

            switch (engine.State.Phase)
            {
                case GamePhase.Bidding:
                    if (engine.MinimumLegalBid() > engine.Config.MaximumBid)
                    {
                        yield return Tween.Wait(0.5f);
                        submit(PlayerCommand.MakePass());
                        yield break;
                    }
                    table.ShowBidPanel(engine.MinimumLegalBid(),
                        amount => { cmd = PlayerCommand.MakeBid(amount); done = true; },
                        () => { cmd = PlayerCommand.MakePass(); done = true; });
                    break;

                case GamePhase.ChoosingTrump:
                    table.ShowTrumpPicker(suit => { cmd = PlayerCommand.MakeTrump(suit); done = true; });
                    break;

                default:
                    engine.GetLegalCards(Seat, _legal);
                    if (_legal.Count == 1)
                    {
                        // Only one legal card: play it without asking.
                        table.BeginPlayInput(_legal, _ => { });
                        yield return Tween.Wait(0.45f);
                        table.CancelInput();
                        submit(PlayerCommand.MakePlay(_legal[0]));
                        yield break;
                    }
                    table.BeginPlayInput(_legal, card => { cmd = PlayerCommand.MakePlay(card); done = true; });
                    break;
            }

            float limit = GameSettings.TurnTimerSeconds;
            float waited = 0f;
            while (!done)
            {
                waited += Time.deltaTime;
                if (limit > 0f && waited >= limit)
                {
                    table.CancelInput();
                    cmd = BotController.Decide(_fallback, engine, Seat);
                    break;
                }
                yield return null;
            }

            table.CancelInput();
            submit(cmd);
        }
    }
}
