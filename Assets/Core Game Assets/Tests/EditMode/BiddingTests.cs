using System.Collections.Generic;
using NUnit.Framework;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Events;
using Tarneeb.Core.Rules;
using Tarneeb.Core.State;

namespace Tarneeb.Tests
{
    public class BiddingTests
    {
        [Test]
        public void Bid_BelowMinimum_Rejected()
        {
            var e = TestUtil.NewGame();
            var r = e.PlaceBid(e.State.CurrentSeat, 6);
            Assert.AreEqual(CommandError.BidBelowMinimum, r.Error);
        }

        [Test]
        public void Bid_AboveMaximum_Rejected()
        {
            var e = TestUtil.NewGame();
            Assert.AreEqual(CommandError.BidAboveMaximum, e.PlaceBid(e.State.CurrentSeat, 14).Error);
        }

        [Test]
        public void Bid_MustBeHigherThanCurrent()
        {
            var e = TestUtil.NewGame();
            int s0 = e.State.CurrentSeat;
            Assert.IsTrue(e.PlaceBid(s0, 8).Success);
            int s1 = e.State.CurrentSeat;
            Assert.AreEqual(CommandError.BidNotHigher, e.PlaceBid(s1, 8).Error);
            Assert.AreEqual(CommandError.BidNotHigher, e.PlaceBid(s1, 7).Error);
            Assert.IsTrue(e.PlaceBid(s1, 9).Success);
        }

        [Test]
        public void Bid_OutOfTurn_Rejected()
        {
            var e = TestUtil.NewGame();
            int other = (e.State.CurrentSeat + 1) & 3;
            Assert.AreEqual(CommandError.NotYourTurn, e.PlaceBid(other, 7).Error);
            Assert.AreEqual(CommandError.NotYourTurn, e.Pass(other).Error);
        }

        [Test]
        public void PlayerWhoPassed_CannotBidAgain_AndIsSkipped()
        {
            var e = TestUtil.NewGame();
            int first = e.State.CurrentSeat;
            Assert.IsTrue(e.Pass(first).Success);
            var seen = new List<int>();
            // Others keep raising; the passer must never get the turn again.
            for (int amount = 7; amount <= 10; amount++)
            {
                seen.Add(e.State.CurrentSeat);
                Assert.AreNotEqual(first, e.State.CurrentSeat);
                Assert.IsTrue(e.PlaceBid(e.State.CurrentSeat, amount).Success);
            }
            Assert.AreEqual(CommandError.NotYourTurn, e.PlaceBid(first, 12).Error);
        }

        [Test]
        public void Bidding_EndsWhenThreePassedAfterBid_HighestBidderIsDeclarer()
        {
            var e = TestUtil.NewGame();
            int a = e.State.CurrentSeat;
            e.PlaceBid(a, 7);
            int b = e.State.CurrentSeat;
            e.PlaceBid(b, 9);
            e.Pass(e.State.CurrentSeat);
            e.Pass(e.State.CurrentSeat);
            Assert.AreEqual(GamePhase.Bidding, e.State.Phase); // a still active
            e.Pass(e.State.CurrentSeat);                        // a passes, b wins
            Assert.AreEqual(GamePhase.ChoosingTrump, e.State.Phase);
            Assert.AreEqual(b, e.State.Declarer);
            Assert.AreEqual(b, e.State.CurrentSeat);
            Assert.AreEqual(9, e.State.HighBid);
        }

        [Test]
        public void SingleBid_ThenThreePasses_EndsBidding()
        {
            var e = TestUtil.NewGame();
            int a = e.State.CurrentSeat;
            e.PlaceBid(a, 7);
            e.Pass(e.State.CurrentSeat);
            e.Pass(e.State.CurrentSeat);
            e.Pass(e.State.CurrentSeat);
            Assert.AreEqual(GamePhase.ChoosingTrump, e.State.Phase);
            Assert.AreEqual(a, e.State.Declarer);
        }

        [Test]
        public void BidOf13_LeavesOnlyPassAvailable()
        {
            var e = TestUtil.NewGame();
            e.PlaceBid(e.State.CurrentSeat, 13);
            Assert.Greater(e.MinimumLegalBid(), e.Config.MaximumBid);
            Assert.IsFalse(e.CanBid(e.State.CurrentSeat));
        }

        [Test]
        public void AllPass_RedealsWithNextDealer()
        {
            var e = TestUtil.NewGame(5);
            var events = TestUtil.Record(e);
            int dealer = e.State.Dealer;
            var handsBefore = new List<Card>(e.State.Hands[0].Cards);
            for (int i = 0; i < 4; i++) Assert.IsTrue(e.Pass(e.State.CurrentSeat).Success);

            Assert.AreEqual(GamePhase.Bidding, e.State.Phase);
            Assert.AreEqual(TarneebRules.NextSeat(dealer, e.Config), e.State.Dealer);
            Assert.AreEqual(TarneebRules.NextSeat(e.State.Dealer, e.Config), e.State.CurrentSeat);
            Assert.IsTrue(events.Exists(x => x is RoundRedealt));
            Assert.AreEqual(0, e.State.HighBid);
            for (int s = 0; s < 4; s++) Assert.AreEqual(13, e.State.Hands[s].Count);
            CollectionAssert.AreNotEqual(handsBefore, e.State.Hands[0].Cards);
        }

        [Test]
        public void AllPass_WithRedealDisabled_ForcesDealerToMinimumBid()
        {
            var cfg = new RulesConfig { RedealOnAllPass = false };
            var e = TestUtil.NewGame(3, cfg);
            int dealer = e.State.Dealer;
            for (int i = 0; i < 4; i++) e.Pass(e.State.CurrentSeat);
            Assert.AreEqual(GamePhase.ChoosingTrump, e.State.Phase);
            Assert.AreEqual(dealer, e.State.Declarer);
            Assert.AreEqual(cfg.MinimumBid, e.State.HighBid);
        }

        [Test]
        public void ChooseTrump_OnlyDeclarer_OnlyInTrumpPhase()
        {
            var e = TestUtil.NewGame();
            Assert.AreEqual(CommandError.WrongPhase, e.ChooseTrump(0, Suit.Spades).Error);
            int a = e.State.CurrentSeat;
            e.PlaceBid(a, 7);
            for (int i = 0; i < 3; i++) e.Pass(e.State.CurrentSeat);
            Assert.AreEqual(CommandError.NotYourTurn, e.ChooseTrump((a + 1) & 3, Suit.Hearts).Error);
            Assert.IsTrue(e.ChooseTrump(a, Suit.Hearts).Success);
            Assert.AreEqual(Suit.Hearts, e.State.Trump);
            Assert.AreEqual(GamePhase.Playing, e.State.Phase);
        }

        [Test]
        public void Declarer_LeadsFirstTrick()
        {
            var e = TestUtil.NewGame();
            int a = e.State.CurrentSeat;
            e.PlaceBid(a, 7);
            for (int i = 0; i < 3; i++) e.Pass(e.State.CurrentSeat);
            e.ChooseTrump(a, Suit.Clubs);
            Assert.AreEqual(a, e.State.CurrentSeat);
            Assert.AreEqual(a, e.State.CurrentTrick.LeadSeat);
        }
    }
}
