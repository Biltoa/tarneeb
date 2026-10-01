using NUnit.Framework;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Tests
{
    public class HardBotTests
    {
        [Test]
        public void HardBots_PlayFullGames_WithoutIllegalMoves_AndBeatNormalBots()
        {
            const int games = 60;
            int hardWins = 0;
            for (int g = 0; g < games; g++)
            {
                var engine = new TarneebEngine(new RulesConfig(), 5000 + g);
                var bots = new IBot[4];
                bool hardEven = g % 2 == 0;
                for (int i = 0; i < 4; i++)
                    bots[i] = BotFactory.Create(((i & 1) == 0) == hardEven ? BotConfig.Hard() : BotConfig.Normal(), g * 7 + i + 1);
                var runner = new BotRunner(engine, bots);     // throws on any illegal command
                engine.StartGame(g % 4);
                runner.RunToEnd();
                if ((engine.State.WinningTeam == 0) == hardEven) hardWins++;
            }
            Assert.GreaterOrEqual(hardWins, 40, "Hard should clearly beat Normal (won " + hardWins + " of " + games + ")");
        }

        [Test]
        public void HandSort_IsDiamondsClubsHeartsSpades_HighToLow()
        {
            var hand = new Hand();
            foreach (var c in new[] { new Card(Suit.Spades, Rank.Ace), new Card(Suit.Hearts, Rank.Two), new Card(Suit.Clubs, Rank.Nine),
                new Card(Suit.Diamonds, Rank.Three), new Card(Suit.Diamonds, Rank.King), new Card(Suit.Clubs, Rank.Ace) }) hand.Add(c);
            hand.SortForDisplay();
            string order = "";
            foreach (var c in hand.Cards) order += c + " ";
            Assert.AreEqual("K♦ 3♦ A♣ 9♣ 2♥ A♠ ", order);
        }
    }
}
