using System;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Core.Bots
{
    /// <summary>Drives an engine with one bot per seat. Used by headless simulation and by bot-filled seats.</summary>
    public sealed class BotRunner
    {
        private readonly TarneebEngine _engine;
        private readonly IBot[] _bots;

        public BotRunner(TarneebEngine engine, IBot[] bots)
        {
            _engine = engine;
            _bots = bots;
            _engine.EventEmitted += e =>
            {
                for (int i = 0; i < _bots.Length; i++) _bots[i]?.Observe(e);
            };
        }

        /// <summary>Performs one action for the seat whose turn it is. Throws if a bot produces an illegal command.</summary>
        public void Step()
        {
            var st = _engine.State;
            int seat = st.CurrentSeat;
            switch (st.Phase)
            {
                case GamePhase.Bidding:
                {
                    var d = _bots[seat].ChooseBid(_engine, seat);
                    Require(d.Pass ? _engine.Pass(seat) : _engine.PlaceBid(seat, d.Amount), "bid", seat);
                    break;
                }
                case GamePhase.ChoosingTrump:
                    Require(_engine.ChooseTrump(seat, _bots[seat].ChooseTrump(_engine, seat)), "trump", seat);
                    break;
                case GamePhase.Playing:
                    Require(_engine.PlayCard(seat, _bots[seat].ChooseCard(_engine, seat)), "card", seat);
                    break;
                case GamePhase.RoundOver:
                    Require(_engine.StartNextRound(), "next round", seat);
                    break;
                default:
                    throw new InvalidOperationException("Cannot step in phase " + st.Phase);
            }
        }

        public void RunToEnd(int maxSteps = 100000)
        {
            int steps = 0;
            while (_engine.State.Phase != GamePhase.GameOver)
            {
                Step();
                if (++steps > maxSteps) throw new InvalidOperationException("Game did not terminate");
            }
        }

        private static void Require(CommandResult r, string what, int seat)
        {
            if (!r.Success) throw new InvalidOperationException("Bot " + what + " rejected for seat " + seat + ": " + r);
        }
    }
}
