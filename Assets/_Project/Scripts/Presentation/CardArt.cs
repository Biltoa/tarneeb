using Tarneeb.Core.Cards;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>Scene singleton holding sprites shared by every CardView.</summary>
    public sealed class CardArt : MonoBehaviour
    {
        public static CardArt Instance { get; private set; }

        public Sprite heart, diamond, spade, club, cardFace, cardBack;

        public static readonly Color Red = new Color(0.80f, 0.10f, 0.16f);
        public static readonly Color Black = new Color(0.10f, 0.10f, 0.13f);

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public Sprite SuitSprite(Suit s)
        {
            switch (s)
            {
                case Suit.Hearts: return heart;
                case Suit.Diamonds: return diamond;
                case Suit.Spades: return spade;
                default: return club;
            }
        }

        public static Color SuitColor(Suit s) => s == Suit.Hearts || s == Suit.Diamonds ? Red : Black;

        public static string SuitName(Suit s) => s.ToString();
    }
}
