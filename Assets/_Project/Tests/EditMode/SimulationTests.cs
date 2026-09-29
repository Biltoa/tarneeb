using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Tarneeb.Core.Bots;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Tests
{
    public class SimulationTests
    {
        public const int GameCount = 10000;

        [Test]
        public void Headless_TenThousandBotGames_NoErrors()
        {
            var sw = Stopwatch.StartNew();
            var seeder = new Random(20260929);
            long rounds = 0, redeals = 0, tricks = 0, cardsPlayed = 0, made = 0, failed = 0, kaboots = 0;
            long teamAWins = 0, teamBWins = 0;
            int maxRounds = 0;

            for (int g = 0; g < GameCount; g++)
            {
                int seed = seeder.Next();
                var engine = new TarneebEngine(new RulesConfig(), seed);
                var bots = new IBot[4];
                for (int i = 0; i < 4; i++) bots[i] = new HeuristicBot(BotConfig.Normal(), seed + i + 1);
                var runner = new BotRunner(engine, bots);

                int tricksThisRound = 0, cardsThisRound = 0, roundsThisGame = 0;
                engine.EventEmitted += e =>
                {
                    if (e is CardPlayed) { cardsThisRound++; cardsPlayed++; }
                    else if (e is TrickWon) { tricksThisRound++; tricks++; }
                    else if (e is RoundRedealt) { redeals++; tricksThisRound = 0; cardsThisRound = 0; }
                    else if (e is RoundEnded re)
                    {
                        Assert.AreEqual(13, tricksThisRound, "tricks in round (seed " + seed + ")");
                        Assert.AreEqual(52, cardsThisRound, "cards in round (seed " + seed + ")");
                        Assert.AreEqual(13, engine.State.TricksByTeam[0] + engine.State.TricksByTeam[1]);
                        rounds++; roundsThisGame++;
                        if (re.Summary.ContractMade) made++; else failed++;
                        if (re.Summary.Kaboot) kaboots++;
                        tricksThisRound = 0; cardsThisRound = 0;
                    }
                };

                engine.StartGame();
                runner.RunToEnd();

                Assert.AreEqual(GamePhase.GameOver, engine.State.Phase);
                Assert.GreaterOrEqual(Math.Max(engine.State.TeamScores[0], engine.State.TeamScores[1]), 41);
                if (engine.State.WinningTeam == 0) teamAWins++; else teamBWins++;
                if (roundsThisGame > maxRounds) maxRounds = roundsThisGame;
            }
            sw.Stop();

            string summary = string.Format(
                "{0} bot games, 0 errors, {1:F2} s ({2:F3} ms/game). rounds={3} (avg {4:F1}/game, max {5}), redeals={6}, tricks={7}, cards={8}, contractsMade={9}, contractsFailed={10}, kaboots={11}, teamA={12}, teamB={13}",
                GameCount, sw.Elapsed.TotalSeconds, sw.Elapsed.TotalMilliseconds / GameCount,
                rounds, (double)rounds / GameCount, maxRounds, redeals, tricks, cardsPlayed, made, failed, kaboots, teamAWins, teamBWins);
            TestContext.WriteLine(summary);
            UnityEngine.Debug.Log("[Simulation] " + summary);
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "SimulationResult.txt"), summary + Environment.NewLine); }
            catch { /* best effort */ }

            Assert.AreEqual(rounds * 13, tricks);
        }

        [Test]
        public void SameSeed_SameGame_Deterministic()
        {
            string Run(int seed)
            {
                var engine = new TarneebEngine(new RulesConfig(), seed);
                var bots = new IBot[4];
                for (int i = 0; i < 4; i++) bots[i] = new HeuristicBot(BotConfig.Normal(), seed + i);
                var runner = new BotRunner(engine, bots);
                var sb = new System.Text.StringBuilder();
                engine.EventEmitted += e =>
                {
                    if (e is CardPlayed cp) sb.Append(cp.Seat).Append(cp.Card).Append(' ');
                    else if (e is BidPlaced bp) sb.Append('b').Append(bp.Seat).Append(bp.Amount).Append(' ');
                };
                engine.StartGame();
                runner.RunToEnd();
                return sb.ToString();
            }
            Assert.AreEqual(Run(777), Run(777));
            Assert.AreNotEqual(Run(777), Run(778));
        }

        [Test]
        public void EasyBots_AlsoNeverPlayIllegally()
        {
            var seeder = new Random(5);
            for (int g = 0; g < 500; g++)
            {
                int seed = seeder.Next();
                var engine = new TarneebEngine(new RulesConfig(), seed);
                var bots = new IBot[4];
                for (int i = 0; i < 4; i++) bots[i] = new HeuristicBot(BotConfig.Easy(), seed + i);
                var runner = new BotRunner(engine, bots);
                engine.StartGame();
                runner.RunToEnd();
                Assert.AreEqual(GamePhase.GameOver, engine.State.Phase);
            }
        }
    }
}
