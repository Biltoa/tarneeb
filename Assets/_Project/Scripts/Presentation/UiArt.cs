using System.Collections.Generic;
using Tarneeb.Core.Cards;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>Runtime access to the generated UI sprites in Resources/UI (icons, suits, glows, particles), cached by name.</summary>
    public static class UiArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out Sprite s) && s != null) return s;
            s = Resources.Load<Sprite>("UI/" + name);
            if (s == null) Debug.LogError("[UiArt] Missing sprite Resources/UI/" + name);
            Cache[name] = s;
            return s;
        }

        /// <summary>The deck's own pip shape: coloured for the table, or a white silhouette to tint.</summary>
        public static Sprite SuitIcon(Suit suit, bool white = false)
        {
            string n;
            switch (suit)
            {
                case Suit.Hearts: n = "suit_heart"; break;
                case Suit.Diamonds: n = "suit_diamond"; break;
                case Suit.Spades: n = "suit_spade"; break;
                default: n = "suit_club"; break;
            }
            return Get(white ? n + "_w" : n);
        }

        // Palette from the Figma design
        public static readonly Color Gold = new Color32(255, 228, 154, 255);
        public static readonly Color GoldLight = new Color32(255, 248, 216, 255);
        public static readonly Color GoldDark = new Color32(231, 188, 93, 255);
        public static readonly Color Cream = new Color32(255, 240, 190, 255);
        public static readonly Color Midnight = new Color32(0, 22, 26, 255);
        public static readonly Color Teal = new Color32(0, 221, 232, 255);
        public static readonly Color Orange = new Color32(250, 153, 71, 255);
        public static readonly Color Good = new Color32(140, 240, 160, 255);
        public static readonly Color Bad = new Color32(255, 140, 128, 255);
    }
}
