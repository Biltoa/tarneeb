using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Tarneeb.Localization
{
    /// <summary>
    /// Mirrors a row of controls for right-to-left languages: children's x positions are flipped around the row's centre and left/right aligned
    /// texts swap sides, so a label that sat on the left with its switch on the right reads label-on-the-right, switch-on-the-left in Arabic.
    /// Children must be anchored to the top-left (as UiKit builds them).
    /// </summary>
    public sealed class RtlMirror : MonoBehaviour
    {
        private bool _mirrored;

        private void OnEnable() { Loc.Changed += Apply; Apply(); }
        private void OnDisable() => Loc.Changed -= Apply;

        private void Apply()
        {
            bool want = Loc.IsRtl;
            if (want == _mirrored) return;
            _mirrored = want;
            float w = ((RectTransform)transform).rect.width;
            foreach (Transform c in transform)
            {
                var rt = (RectTransform)c;
                rt.anchoredPosition = new Vector2(w - rt.anchoredPosition.x, rt.anchoredPosition.y);
                var t = c.GetComponent<TMP_Text>();
                if (t != null) t.alignment = Flip(t.alignment);
            }
        }

        private static TextAlignmentOptions Flip(TextAlignmentOptions a)
        {
            switch (a)
            {
                case TextAlignmentOptions.Left: return TextAlignmentOptions.Right;
                case TextAlignmentOptions.Right: return TextAlignmentOptions.Left;
                case TextAlignmentOptions.TopLeft: return TextAlignmentOptions.TopRight;
                case TextAlignmentOptions.TopRight: return TextAlignmentOptions.TopLeft;
                case TextAlignmentOptions.BottomLeft: return TextAlignmentOptions.BottomRight;
                case TextAlignmentOptions.BottomRight: return TextAlignmentOptions.BottomLeft;
                case TextAlignmentOptions.MidlineLeft: return TextAlignmentOptions.MidlineRight;
                case TextAlignmentOptions.MidlineRight: return TextAlignmentOptions.MidlineLeft;
                default: return a;
            }
        }
    }
}
