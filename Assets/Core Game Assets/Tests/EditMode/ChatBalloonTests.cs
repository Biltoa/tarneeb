using NUnit.Framework;
using Tarneeb.Presentation;

namespace Tarneeb.Tests
{
    public class ChatBalloonTests
    {
        [Test]
        public void Shorten_KeepsShortText_AndCutsLongTextWithDots()
        {
            Assert.AreEqual("Nice bid, partner!", ChatBalloon.Shorten("Nice bid, partner!"));
            string cut = ChatBalloon.Shorten("Thanks! Good luck to everyone at this table");
            Assert.LessOrEqual(cut.Length, ChatBalloon.MaxChars);
            Assert.IsTrue(cut.EndsWith("..."));
            Assert.AreEqual("Thanks! Good luck to...", ChatBalloon.Shorten("Thanks! Good luck to everyone at this table", 24));
        }

        [Test]
        public void Shorten_HandlesUnbrokenStrings()
        {
            string cut = ChatBalloon.Shorten(new string('e', 60));
            Assert.AreEqual(ChatBalloon.MaxChars, cut.Length);
            Assert.IsTrue(cut.EndsWith("..."));
        }
    }
}
