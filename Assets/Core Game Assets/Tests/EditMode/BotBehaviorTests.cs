using System.Collections.Generic;
using NUnit.Framework;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Tests
{
    /// <summary>What the bots actually decide: difficulty ladder over many games, and bids / trump / play in crafted situations.</summary>
    public class BotBehaviorTests
    {
        private static int WinsOfA(BotConfig a, BotConfig b, int games, int seedBase)
        {
            int wins = 0;
            for (int g = 0; g < games; g++)
            {
                var engine = new TarneebEngine(new RulesConfig(), seedBase + g);
                bool aEven = g % 2 == 0;
                var bots = new IBot[4];
                for (int i = 0; i < 4; i++) bots[i] = BotFactory.Create(((i & 1) == 0) == aEven ? a : b, g * 11 + i + 3);
                engine.StartGame(g % 4);
                new BotRunner(engine, bots).RunToEnd();             // throws on an illegal command
                if ((engine.State.WinningTeam == 0) == aEven) wins++;
            }
            return wins;
        }

        [Test]
        public void Normal_BeatsEasy_OverManyGames()
        {
            int wins = WinsOfA(BotConfig.Normal(), BotConfig.Easy(), 300, 9000);
            Assert.GreaterOrEqual(wins, 180, "Normal won only " + wins + " of 300 against Easy");
        }

        [Test]
        public void Hard_BeatsEasy_AndMirrorMatchIsEven()
        {
            Assert.GreaterOrEqual(WinsOfA(BotConfig.Hard(), BotConfig.Easy(), 40, 9500), 28, "Hard should clearly beat Easy");
            int mirror = WinsOfA(BotConfig.Normal(), BotConfig.Normal(), 200, 9800);       // same skill: neither side should dominate
            Assert.That(mirror, Is.InRange(70, 130), "mirror match is skewed (" + mirror + " of 200)");
        }

        /// <summary>Puts <paramref name="mine"/> in the hand of the seat that bids first; the rest of the deck goes to the other seats.</summary>
        private static TarneebEngine Scenario(string mine, out int seat)
        {
            var e = new TarneebEngine(new RulesConfig(), 1);
            var mineCards = TestUtil.Cards(mine);
            var rest = new List<Card>();
            for (int s = 0; s < 4; s++) for (int r = 2; r <= 14; r++) rest.Add(new Card((Suit)s, (Rank)r));
            foreach (var c in mineCards) rest.Remove(c);
            e.StartRoundWithHands(new Card[4][] { mineCards, rest.GetRange(0, 13).ToArray(), rest.GetRange(13, 13).ToArray(), rest.GetRange(26, 13).ToArray() }, 3);
            seat = 0;
            Assert.AreEqual(0, e.State.CurrentSeat, "scenario assumes seat 0 bids first");
            return e;
        }

        [Test]
        public void WeakHand_Passes_StrongHand_Bids()
        {
            foreach (var cfg in new[] { BotConfig.Normal(), BotConfig.Hard() })
            {
                var weak = Scenario("2C 3C 4D 5D 6H 7H 8S 9S 3H 4S 5C 6D 7C", out int s);
                Assert.IsTrue(BotFactory.Create(cfg, 5).ChooseBid(weak, s).Pass, "a hand with no honours must pass");
                var strong = Scenario("AS KS QS JS 10S 9S AH KH AD KD AC 3C 2D", out s);
                var d = BotFactory.Create(cfg, 5).ChooseBid(strong, s);
                Assert.IsFalse(d.Pass, "a monster hand must bid");
                Assert.GreaterOrEqual(d.Amount, strong.MinimumLegalBid());
            }
        }

        [Test]
        public void Declarer_NamesTheLongestStrongSuit_AsTarneeb()
        {
            var e = Scenario("AH KH QH JH 9H 6H 2H 3S 4S 5D 7D 8C 9C", out int s);
            foreach (var cfg in new[] { BotConfig.Normal(), BotConfig.Hard() })
                Assert.AreEqual(Suit.Hearts, BotFactory.Create(cfg, 5).ChooseTrump(e, s));
        }

        [Test]
        public void FollowsSuit_AndAlwaysChoosesALegalCard_InEveryTrickOfManyGames()
        {
            // BotRunner throws on any rejected command, so a full game per seed proves every choice (all difficulties) was legal
            foreach (var cfg in new[] { BotConfig.Easy(), BotConfig.Normal(), BotConfig.Hard() })
                for (int g = 0; g < (cfg.Search ? 15 : 300); g++)
                {
                    var engine = new TarneebEngine(new RulesConfig(), 20000 + g);
                    var bots = new IBot[4];
                    for (int i = 0; i < 4; i++) bots[i] = BotFactory.Create(cfg, g + i);
                    engine.StartGame(g % 4);
                    Assert.DoesNotThrow(() => new BotRunner(engine, bots).RunToEnd());
                    Assert.AreEqual(GamePhase.GameOver, engine.State.Phase);
                }
        }
    }
}
