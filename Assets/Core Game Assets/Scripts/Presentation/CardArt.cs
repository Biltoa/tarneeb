using Tarneeb.Core.Cards;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// Card faces are drawn by Tools/gen_cards.py
    /// into Resources/Cards/card_atlas.png (9 x 6 cells of 224 x 313 px, cell index = Card.ToByte()). The back is composed from the Figma design.
    /// </summary>
    public static class CardArt
    {
        public const int Columns = 9, Rows = 6, CellW = 224, CellH = 313;
        private static Sprite[] _faces;
        private static Sprite _back;

        public static Sprite Face(Card card)
        {
            if (_faces == null || _faces[0] == null) LoadFaces();          // (a destroyed sprite reads as null: see the hideFlags note below)
            int i = card.ToByte();
            return _faces != null && i >= 0 && i < _faces.Length ? _faces[i] : null;
        }

        public static Sprite Back
        {
            get
            {
                if (_back == null)
                {
                    var tex = Resources.Load<Texture2D>("Cards/card_back");
                    if (tex == null) { Debug.LogError("[CardArt] Resources/Cards/card_back.png is missing"); return null; }
                    _back = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    _back.hideFlags = HideFlags.HideAndDontSave;
                }
                return _back;
            }
        }

        private static void LoadFaces()
        {
            var tex = Resources.Load<Texture2D>("Cards/card_atlas");
            if (tex == null) { Debug.LogError("[CardArt] Resources/Cards/card_atlas.png is missing"); return; }
            _faces = new Sprite[52];
            for (int i = 0; i < 52; i++)
            {
                int col = i % Columns, row = i / Columns;
                var rect = new Rect(col * CellW, tex.height - (row + 1) * CellH, CellW, CellH);      // sprite rects are bottom-up
                _faces[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _faces[i].hideFlags = HideFlags.HideAndDontSave;       // runtime-made sprites are otherwise swept by Resources.UnloadUnusedAssets (faces turned white in the Editor)
            }
        }

        public static Sprite SuitSprite(Suit s, bool white = false) => UiArt.SuitIcon(s, white);

        public static readonly Color Red = new Color(0.82f, 0.06f, 0.18f);
        public static readonly Color Black = new Color(0.10f, 0.10f, 0.13f);
        public static Color SuitColor(Suit s) => s == Suit.Hearts || s == Suit.Diamonds ? Red : Black;
        public static string SuitName(Suit s) => s.ToString();
    }
}
