using NUnit.Framework;
using Tarneeb.Localization;

namespace Tarneeb.Tests
{
    public class ArabicShaperTests
    {
        private static string U(params int[] cps) { var sb = new System.Text.StringBuilder(); foreach (int c in cps) sb.Append((char)c); return sb.ToString(); }

        [Test]
        public void Letters_Take_Initial_Medial_Final_And_Isolated_Forms()
        {
            // marhaba: meem initial, raa final (does not connect on), haa initial, baa medial, alef final
            Assert.AreEqual(U(0xFEE3, 0xFEAE, 0xFEA3, 0xFE92, 0xFE8E), ArabicShaper.Shape("مرحبا"));
            // a lone letter is isolated
            Assert.AreEqual(U(0xFE8F), ArabicShaper.Shape("ب"));
        }

        [Test]
        public void Lam_Alef_Becomes_One_Ligature()
        {
            Assert.AreEqual(U(0xFEFB), ArabicShaper.Shape("لا"));
            // "salaam": seen initial, lam-alef ligature final (joined to the previous letter)... lam joins the seen, so the ligature is the final form
            Assert.AreEqual(U(0xFEB3, 0xFEFC), ArabicShaper.Shape("سلا"));
        }

        [Test]
        public void Visual_Order_Is_Right_To_Left_And_Keeps_Numbers_And_Latin_Words()
        {
            // logical "ba alef" -> visual order reversed
            Assert.AreEqual(U(0xFE8E, 0xFE91), ArabicShaper.ToVisual("با"));
            // the digits keep their order, and sit at the left end of a right-to-left line when they come last in reading order
            string v = ArabicShaper.ToVisual("باص 12");
            Assert.IsTrue(v.StartsWith("12"), v);
            // a Latin word inside an Arabic sentence keeps its letters in order
            string w = ArabicShaper.ToVisual("لاعب AHMAD");
            Assert.IsTrue(w.StartsWith("AHMAD"), w);
            Assert.AreEqual("plain English", ArabicShaper.ToVisual("plain English"));
        }

        [Test]
        public void Every_Translation_Is_Shaped_Without_Raw_Arabic_Letters_Left()
        {
            foreach (var kv in LocTable.Ar)
            {
                string v = ArabicShaper.ToVisual(kv.Value.Replace("{0}", "7").Replace("{1}", "9").Replace("{2}", "X"));
                Assert.IsFalse(ArabicShaper.NeedsShaping(v), kv.Key);
            }
        }
    }
}
