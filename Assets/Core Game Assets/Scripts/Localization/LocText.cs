using TMPro;
using UnityEngine;

namespace Tarneeb.Localization
{
    /// <summary>
    /// Keeps a TMP text translated: holds the English key (and optional format arguments), and on every language change writes the translated,
    /// shaped text. For Arabic text it also drops character spacing (spacing would break the joined letters) and scales the size slightly, since
    /// Arabic letters look smaller than the Latin capitals the layout was designed for.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocText : MonoBehaviour
    {
        public const float ArabicScale = 1.14f;

        public string key;
        /// <summary>Upper-case the English text (Latin only; Arabic has no case).</summary>
        public bool upper;
        private object[] _args;
        private TMP_Text _text;
        private float _size, _spacing;
        private bool _captured;

        private void Capture()
        {
            if (_captured) return;
            _text = GetComponent<TMP_Text>();
            if (_text == null) return;
            _captured = true;
            _size = _text.fontSize;
            _spacing = _text.characterSpacing;
        }

        private void Awake() => Capture();
        private void OnEnable() { Loc.Changed += Refresh; Refresh(); }
        private void OnDisable() => Loc.Changed -= Refresh;

        public void SetKey(string english, object[] args = null)
        {
            key = english;
            _args = args;
            Refresh();
        }

        private static object[] Translated(object[] args)            // names and suit words passed as arguments follow the language too
        {
            var r = new object[args.Length];
            for (int i = 0; i < args.Length; i++) r[i] = args[i] is string s && Loc.Has(s) ? Loc.T(s) : args[i];
            return r;
        }

        public void Refresh()
        {
            Capture();
            if (_text == null || key == null) return;
            string logical = _args != null && _args.Length > 0 ? string.Format(Loc.T(key), Translated(_args)) : Loc.T(key);
            bool arabic = ArabicShaper.NeedsShaping(logical);
            if (upper && !arabic) logical = logical.ToUpperInvariant();
            _text.fontSize = arabic ? _size * ArabicScale : _size;
            _text.characterSpacing = arabic ? 0f : _spacing;
            _text.text = Loc.Display(logical);
        }
    }
}
