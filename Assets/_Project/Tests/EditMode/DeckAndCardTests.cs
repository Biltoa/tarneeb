using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tarneeb.Core.Cards;
using Tarneeb.Core.State;
using Tarneeb.Core.Rules;

namespace Tarneeb.Tests
{
    public class DeckAndCardTests
    {
        [Test]
        public void Deck_HasFiftyTwoUniqueCards()
        {
            var deck = Deck.Create();
            Assert.AreEqual(52, deck.Count);
            Assert.AreEqual(52, new HashSet<Card>(deck).Count);
        }

        [Test]
        public void Card_ByteRoundTrip()
        {
            foreach (var c in Deck.Create())
                Assert.AreEqual(c, Card.FromByte(c.ToByte()));
        }

        [Test]
        public void Dealing_GivesThirteenUniqueCardsPerSeat_AndNoOverlap()
        {
            var e = TestUtil.NewGame(42);
            var all = new HashSet<Card>();
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(13, e.State.Hands[s].Count);
                var own = new HashSet<Card>(e.State.Hands[s].Cards);
                Assert.AreEqual(13, own.Count);
                all.UnionWith(own);
            }
            Assert.AreEqual(52, all.Count);
        }

        [Test]
        public void SameSeed_ProducesSameDeal_AndSameFirstDealer()
        {
            var a = TestUtil.NewGame(1234);
            var b = TestUtil.NewGame(1234);
            Assert.AreEqual(a.State.Dealer, b.State.Dealer);
            for (int s = 0; s < 4; s++)
                CollectionAssert.AreEqual(a.State.Hands[s].Cards, b.State.Hands[s].Cards);
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentDeals()
        {
            var a = TestUtil.NewGame(1);
            var b = TestUtil.NewGame(2);
            bool same = true;
            for (int s = 0; s < 4; s++)
                if (!System.Linq.Enumerable.SequenceEqual(a.State.Hands[s].Cards, b.State.Hands[s].Cards)) same = false;
            Assert.IsFalse(same);
        }

        [Test]
        public void FirstBidder_IsPlayerAfterDealerInPlayDirection()
        {
            var e = TestUtil.NewGame(7);
            Assert.AreEqual(TarneebRules.NextSeat(e.State.Dealer, e.Config), e.State.CurrentSeat);
        }

        [Test]
        public void Partners_SitOpposite()
        {
            Assert.AreEqual(0, TarneebRules.TeamOf(0));
            Assert.AreEqual(0, TarneebRules.TeamOf(2));
            Assert.AreEqual(1, TarneebRules.TeamOf(1));
            Assert.AreEqual(1, TarneebRules.TeamOf(3));
            Assert.AreEqual(2, TarneebRules.Partner(0));
            Assert.AreEqual(3, TarneebRules.Partner(1));
        }
    }
}
