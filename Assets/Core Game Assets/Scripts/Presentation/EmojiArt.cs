using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>Chat emoji sprites, cut from the Resources/Emoji/emoji_atlas.png grid (4 x 4 cells, 12 used, index counted from the top-left; power-of-two so Unity does not rescale it).</summary>
    public static class EmojiArt
    {
        public const int Columns = 4, Rows = 4;
        private static Sprite[] _sprites;

        public static Sprite Get(int id)
        {
            if (_sprites == null || _sprites[0] == null) Load();
            return id >= 0 && id < _sprites.Length ? _sprites[id] : null;
        }

        private static void Load()
        {
            _sprites = new Sprite[Columns * Rows];
            var tex = Resources.Load<Texture2D>("Emoji/emoji_atlas");
            if (tex == null) { Debug.LogError("[EmojiArt] Resources/Emoji/emoji_atlas.png is missing"); return; }
            float cw = tex.width / (float)Columns, ch = tex.height / (float)Rows;
            for (int i = 0; i < _sprites.Length; i++)
            {
                int col = i % Columns, row = i / Columns;
                var rect = new Rect(col * cw, tex.height - (row + 1) * ch, cw, ch);   // sprite rects are bottom-up
                _sprites[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
                _sprites[i].hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }
}
