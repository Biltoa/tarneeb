using System.Collections.Generic;
using NUnit.Framework;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;
using static Tarneeb.Tests.TestUtil;

namespace Tarneeb.Tests
{
    public class PlayAndScoringTests
    {
        private static readonly RulesConfig Cfg = new RulesConfig();

        private static Card[] Trick(int lead, params string[] inPlayOrder)
        {
            var bySeat = new Card[4];
            int seat = lead;
            foreach (var s in inPlayOrder)
            {
                bySeat[seat] = C(s);
                seat = TarneebRules.NextSeat(seat, Cfg);
            }
            return bySeat;
        }

        // ---- trick winner

        [Test]
        public void TrickWinner_LedSuitOnly_HighestOfLedSuitWins()
        {
            var t = Trick(0, "5H", "KH", "9H", "2H");
            Assert.AreEqual(1, TarneebRules.TrickWinner(t, 0, Suit.Spades, Cfg));
        }

        [Test]
        public void TrickWinner_SingleTrump_BeatsHigherLedSuit()
        {
            var t = Trick(0, "AH", "3S", "KH", "QH");
            Assert.AreEqual(1, TarneebRules.TrickWinner(t, 0, Suit.Spades, Cfg));
        }

        [Test]
        public void TrickWinner_MultipleTrumps_HighestTrumpWins()
        {
            var t = Trick(0, "AH", "3S", "9S", "QH");
            Assert.AreEqual(2, TarneebRules.TrickWinner(t, 0, Suit.Spades, Cfg));
        }

        [Test]
        public void TrickWinner_OffSuitDiscards_Ignored()
        {
            var t = Trick(0, "2H", "AC", "AD", "3H");
            Assert.AreEqual(3, TarneebRules.TrickWinner(t, 0, Suit.Spades, Cfg));
        }

        [Test]
        public void TrickWinner_WorksFromAnyLeader()
        {
            var t = Trick(2, "4D", "9D", "2S", "KD");
            Assert.AreEqual(0, TarneebRules.TrickWinner(t, 2, Suit.Spades, Cfg));
        }

        [Test]
        public void TrickWinner_TrumpLed_HighestTrumpWins()
        {
            var t = Trick(1, "5S", "9S", "AH", "KS");
            Assert.AreEqual(0, TarneebRules.TrickWinner(t, 1, Suit.Spades, Cfg));
        }

        // ---- follow suit

        private static TarneebEngine Scenario(int declarerSeatWanted)
        {
            // dealer 3 -> first bidder seat 0 (counterclockwise: +1). seat s holds suit s (C,D,H,S) entirely.
            var e = new TarneebEngine(new RulesConfig(), 1);
            e.StartRoundWithHands(SplitDeckBySuit(), 3);
            return e;
        }

        [Test]
        public void FollowSuit_IllegalCardRejected_WhenHoldingLedSuit()
        {
            // Custom hands: seat0 leads a heart; seat1 holds hearts and a club.
            var hands = new Card[4][];
            hands[0] = Cards("AH 2C 3C 4C 5C 6C 7C 8C 9C 10C JC QC KC");
            hands[1] = Cards("2H AC 2D 3D 4D 5D 6D 7D 8D 9D 10D JD QD");
            hands[2] = Cards("3H 4H 5H 6H 7H 8H 9H 10H JH QH KH 2S 3S");
            hands[3] = Cards("4S 5S 6S 7S 8S 9S 10S JS QS KS AS KD AD");
            var e = new TarneebEngine(new RulesConfig(), 1);
            e.StartRoundWithHands(hands, 3);
            MakeDeclarer(e, 0, 7, Suit.Spades);
            Assert.AreEqual(GamePhase.Playing, e.State.Phase);
            Assert.IsTrue(e.PlayCard(0, C("AH")).Success);
            var r = e.PlayCard(1, C("AC"));
            Assert.AreEqual(CommandError.MustFollowSuit, r.Error);
            Assert.IsTrue(e.PlayCard(1, C("2H")).Success);
        }

        [Test]
        public void VoidInLedSuit_MayPlayAnyCard_IncludingTrump()
        {
            var hands = new Card[4][];
            hands[0] = Cards("AH 2C 3C 4C 5C 6C 7C 8C 9C 10C JC QC KC");
            hands[1] = Cards("2S 3S 4S 5S 6S 7S 8S 9S 10S JS QS KS AS");
            hands[2] = Cards("3H 4H 5H 6H 7H 8H 9H 10H JH QH KH 2H 2D");
            hands[3] = Cards("3D 4D 5D 6D 7D 8D 9D 10D JD QD KD AD 2C");
            var e = new TarneebEngine(new RulesConfig(), 1);
            e.StartRoundWithHands(hands, 3);
            MakeDeclarer(e, 0, 7, Suit.Spades);
            e.PlayCard(0, C("AH"));
            Assert.IsTrue(e.PlayCard(1, C("2S")).Success);
        }

        [Test]
        public void PlayCard_NotInHand_OutOfTurn_WrongPhase_Rejected()
        {
            var e = Scenario(0);
            Assert.AreEqual(CommandError.WrongPhase, e.PlayCard(0, C("2C")).Error);
            MakeDeclarer(e, 0, 7, Suit.Spades);
            Assert.AreEqual(CommandError.NotYourTurn, e.PlayCard(1, C("2D")).Error);
            Assert.AreEqual(CommandError.CardNotInHand, e.PlayCard(0, C("2D")).Error);
        }

        [Test]
        public void TrickWinner_LeadsNextTrick_AndEventsAreEmitted()
        {
            var e = Scenario(0);
            var ev = Record(e);
            MakeDeclarer(e, 0, 7, Suit.Spades);
            // seat0 clubs, seat1 diamonds, seat2 hearts, seat3 spades(trump): seat 3 trumps and wins.
            e.PlayCard(0, C("2C"));
            e.PlayCard(1, C("2D"));
            e.PlayCard(2, C("2H"));
            e.PlayCard(3, C("2S"));
            var won = ev.Find(x => x is TrickWon) as TrickWon;
            Assert.IsNotNull(won);
            Assert.AreEqual(3, won.Winner);
            Assert.AreEqual(3, e.State.CurrentSeat);
            Assert.AreEqual(1, e.State.TricksByTeam[1]);
        }

        // ---- scoring

        [Test]
        public void Score_ContractMade_DeclarersScoreTricks_DefendersZero()
        {
            var s = TarneebRules.ScoreRound(0, 8, Suit.Hearts, new[] { 9, 4 }, new[] { 10, 10 }, Cfg);
            Assert.IsTrue(s.ContractMade);
            Assert.AreEqual(9, s.DeclaringDelta);
            Assert.AreEqual(0, s.DefendingDelta);
            Assert.AreEqual(19, s.TeamScoresAfter[0]);
            Assert.AreEqual(10, s.TeamScoresAfter[1]);
        }

        [Test]
        public void Score_ContractMadeExactly_Counts()
        {
            var s = TarneebRules.ScoreRound(1, 7, Suit.Clubs, new[] { 6, 7 }, new[] { 0, 0 }, Cfg);
            Assert.IsTrue(s.ContractMade);
            Assert.AreEqual(7, s.TeamScoresAfter[1]);
            Assert.AreEqual(0, s.TeamScoresAfter[0]);
        }

        [Test]
        public void Score_ContractFailed_DeclarersLoseBid_DefendersScoreTheirTricks()
        {
            var s = TarneebRules.ScoreRound(0, 9, Suit.Hearts, new[] { 6, 7 }, new[] { 5, 5 }, Cfg);
            Assert.IsFalse(s.ContractMade);
            Assert.AreEqual(-9, s.DeclaringDelta);
            Assert.AreEqual(7, s.DefendingDelta);
            Assert.AreEqual(-4, s.TeamScoresAfter[0]);
            Assert.AreEqual(12, s.TeamScoresAfter[1]);
        }

        [Test]
        public void Score_CanGoNegative()
        {
            var s = TarneebRules.ScoreRound(2, 10, Suit.Spades, new[] { 3, 10 }, new[] { 2, 0 }, Cfg);
            Assert.AreEqual(-8, s.TeamScoresAfter[0]);
        }

        [Test]
        public void Score_Kaboot_ScoresSixteen()
        {
            var s = TarneebRules.ScoreRound(0, 13, Suit.Spades, new[] { 13, 0 }, new[] { 20, 0 }, Cfg);
            Assert.IsTrue(s.Kaboot);
            Assert.AreEqual(16, s.DeclaringDelta);
            Assert.AreEqual(36, s.TeamScoresAfter[0]);
        }

        [Test]
        public void Score_Bid13Failed_LosesThirteen()
        {
            var s = TarneebRules.ScoreRound(0, 13, Suit.Spades, new[] { 12, 1 }, new[] { 0, 0 }, Cfg);
            Assert.IsFalse(s.Kaboot);
            Assert.AreEqual(-13, s.TeamScoresAfter[0]);
            Assert.AreEqual(1, s.TeamScoresAfter[1]);
        }

        [Test]
        public void Score_DefendersScoreWhenContractMade_ConfigOn()
        {
            var cfg = new RulesConfig { DefendersScoreWhenContractMade = true };
            var s = TarneebRules.ScoreRound(0, 7, Suit.Spades, new[] { 8, 5 }, new[] { 0, 0 }, cfg);
            Assert.AreEqual(5, s.DefendingDelta);
        }

        [Test]
        public void GameEnd_FirstToTarget_Wins()
        {
            Assert.AreEqual(-1, TarneebRules.DetermineGameWinner(new[] { 40, 30 }, 0, Cfg));
            Assert.AreEqual(0, TarneebRules.DetermineGameWinner(new[] { 41, 30 }, 1, Cfg));
            Assert.AreEqual(1, TarneebRules.DetermineGameWinner(new[] { 10, 45 }, 0, Cfg));
        }

        [Test]
        public void GameEnd_BothCross_DeclaringTeamWins()
        {
            Assert.AreEqual(1, TarneebRules.DetermineGameWinner(new[] { 44, 41 }, 1, Cfg));
            Assert.AreEqual(0, TarneebRules.DetermineGameWinner(new[] { 44, 41 }, 0, Cfg));
        }

        [Test]
        public void FullRound_ThirteenTricks_ThenRoundOver_AndNextRoundRotatesDealer()
        {
            var e = new TarneebEngine(new RulesConfig(), 99);
            e.StartGame();
            var bots = new Tarneeb.Core.Bots.IBot[4];
            for (int i = 0; i < 4; i++) bots[i] = new Tarneeb.Core.Bots.HeuristicBot(Tarneeb.Core.Bots.BotConfig.Normal(), i + 1);
            var runner = new Tarneeb.Core.Bots.BotRunner(e, bots);
            while (e.State.Phase != GamePhase.RoundOver && e.State.Phase != GamePhase.GameOver) runner.Step();
            Assert.AreEqual(13, e.State.TricksCompleted);
            Assert.AreEqual(13, e.State.TricksByTeam[0] + e.State.TricksByTeam[1]);
            if (e.State.Phase == GamePhase.RoundOver)
            {
                int dealer = e.State.Dealer;
                Assert.IsTrue(e.StartNextRound().Success);
                Assert.AreEqual(TarneebRules.NextSeat(dealer, e.Config), e.State.Dealer);
                Assert.AreEqual(GamePhase.Bidding, e.State.Phase);
            }
        }
    }
}
