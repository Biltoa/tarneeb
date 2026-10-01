using NUnit.Framework;
using Tarneeb.Config;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using Tarneeb.Game;

namespace Tarneeb.Tests
{
    public class BotSkillSwitchTests
    {
        [Test]
        public void Reconfigure_SwitchesTheBotKind_AndGamesStayLegal()
        {
            var engine = new TarneebEngine(new RulesConfig(), 99);
            var seats = new BotController[4];
            for (int s = 0; s < 4; s++) seats[s] = new BotController(s, BotConfig.Normal(), 1000 + s, false);
            engine.EventEmitted += e => { foreach (var c in seats) c.Observe(e); };
            Assert.IsInstanceOf<HeuristicBot>(seats[0].Bot);

            engine.StartGame(0);
            int step = 0;
            var kinds = new[] { BotConfig.Hard(), BotConfig.Easy(), BotConfig.Normal(), BotConfig.Hard() };
            while (engine.State.Phase != GamePhase.GameOver)
            {
                if (step % 17 == 0)                                                   // switch every seat's skill in the middle of the round, repeatedly
                {
                    BotConfig next = kinds[(step / 17) % kinds.Length];
                    foreach (var c in seats) c.Reconfigure(next);
                    Assert.AreEqual(next.Search, seats[1].Bot is SearchBot, "the bot kind follows the skill");
                }
                var st = engine.State;
                if (st.Phase == GamePhase.RoundOver) { Assert.IsTrue(engine.StartNextRound().Success); continue; }
                PlayerCommand cmd = seats[st.CurrentSeat].DecideNow(engine);
                CommandResult r;
                switch (cmd.Kind)
                {
                    case CommandKind.Bid: r = engine.PlaceBid(st.CurrentSeat, cmd.Amount); break;
                    case CommandKind.Pass: r = engine.Pass(st.CurrentSeat); break;
                    case CommandKind.Trump: r = engine.ChooseTrump(st.CurrentSeat, cmd.Suit); break;
                    default: r = engine.PlayCard(st.CurrentSeat, cmd.Card); break;
                }
                Assert.IsTrue(r.Success, "illegal command after a mid-game skill switch at step " + step + ": " + r);
                step++;
                Assert.Less(step, 20000);
            }
        }

        [Test]
        public void Difficulty_Setting_RaisesTheChangeEvent_AndMapsToConfigs()
        {
            int raised = 0;
            void H() => raised++;
            GameSettings.DifficultyChanged += H;
            BotDifficulty old = GameSettings.Difficulty;
            try
            {
                GameSettings.Difficulty = BotDifficulty.Hard;
                Assert.IsTrue(GameSettings.CurrentBotConfig().Search);
                GameSettings.Difficulty = BotDifficulty.Easy;
                Assert.Greater(GameSettings.CurrentBotConfig().RandomMoveChance, 0.0);
                GameSettings.Difficulty = BotDifficulty.Normal;
                Assert.IsFalse(GameSettings.CurrentBotConfig().Search);
            }
            finally { GameSettings.DifficultyChanged -= H; GameSettings.Difficulty = old; }
            Assert.GreaterOrEqual(raised, 2);
        }
    }
}
