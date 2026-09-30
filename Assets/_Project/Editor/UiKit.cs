#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Tarneeb.Presentation;

namespace Tarneeb.EditorTools
{
    /// <summary>
    /// Small factory for building uGUI in Figma coordinates: x/y are the CENTRE of the element measured from the parent's top-left
    /// corner, y pointing down, exactly like the design file (and like the nodes the Figma importer creates).
    /// </summary>
    public static class UiKit
    {
        public const string ResUi = "Assets/_Project/Resources/UI/";
        public const string FigmaSprites = "Assets/FigmaImports/Tarneeb VIP _ Unity Gameplay UI/";
        public static TMP_FontAsset Cinzel, Noto;

        public static readonly Color Gold = new Color32(255, 228, 154, 255);
        public static readonly Color Cream = new Color32(255, 240, 190, 255);
        public static readonly Color Midnight = new Color32(0, 22, 26, 255);
        public static readonly Color Dark = new Color32(0, 20, 22, 255);
        public static readonly Color Ink = new Color32(30, 20, 6, 255);

        public static void LoadFonts()
        {
            Cinzel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FigmaImports/Fonts/Cinzel-700 SDF.asset");
            Noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FigmaImports/Fonts/Noto Serif-700 SDF.asset");
            foreach (TMP_FontAsset f in new[] { Cinzel, Noto })
            {
                if (f == null) continue;
                f.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                f.isMultiAtlasTexturesEnabled = true;
                f.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_abcdefghijklmnopqrstuvwxyz{|}~");
                EditorUtility.SetDirty(f);
            }
        }

        public static Sprite Res(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(ResUi + name + ".png");
            if (s == null) Debug.LogError("[UiKit] missing sprite " + name);
            return s;
        }

        public static Sprite Fig(string id)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(FigmaSprites + id + ".png");
            if (s == null) Debug.LogError("[UiKit] missing figma sprite " + id);
            return s;
        }

        public static RectTransform Rt(string name, Transform parent, float cx, float cy, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(cx, -cy);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Full-stretch child (overlays, dims).</summary>
        public static RectTransform Fill(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, float cx, float cy, float w, float h, Color? color = null,
            bool raycast = false, bool sliced = false)
        {
            RectTransform rt = Rt(name, parent, cx, cy, w, h);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.color = color ?? Color.white;
            im.raycastTarget = raycast;
            if (sliced) im.type = Image.Type.Sliced;
            return im;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float cx, float cy, float w, float h, float size, Color color,
            bool number = false, TextAlignmentOptions align = TextAlignmentOptions.Center, bool wrap = false)
        {
            RectTransform rt = Rt(name, parent, cx, cy, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = number ? Noto : Cinzel;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Button with a sliced background sprite and a centred label child named "Label".</summary>
        public static Button Btn(string name, Transform parent, string spriteName, float cx, float cy, float w, float h, string label, float fontSize,
            Color labelColor, bool number = false)
        {
            Image bg = Img(name, parent, Res(spriteName), cx, cy, w, h, Color.white, true, true);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<ButtonFx>();
            if (!string.IsNullOrEmpty(label))
            {
                TextMeshProUGUI t = Text("Label", bg.transform, label, w / 2f, h / 2f, w - 16f, h - 8f, fontSize, labelColor, number);
                t.fontStyle = FontStyles.Bold;
            }
            return b;
        }

        /// <summary>Round icon button: gold ring + emerald face sprite, with a centred icon child.</summary>
        public static Button IconBtn(string name, Transform parent, float cx, float cy, float d, string iconSprite, float iconSize)
        {
            Image bg = Img(name, parent, Res("button_round"), cx, cy, d, d, Color.white, true);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg; b.transition = Selectable.Transition.None;
            bg.gameObject.AddComponent<ButtonFx>();
            Img("Icon", bg.transform, Res(iconSprite), d / 2f, d / 2f, iconSize, iconSize);
            return b;
        }

        public static void Stretch(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static CanvasGroup Group(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            return cg != null ? cg : go.AddComponent<CanvasGroup>();        // (not '??': Unity's missing-component object is not a C# null)
        }

        /// <summary>TMP_InputField needs a viewport/text/placeholder hierarchy.</summary>
        public static TMP_InputField Input(string name, Transform parent, float cx, float cy, float w, float h, string placeholder, int limit, float fontSize = 34f)
        {
            Image bg = Img(name, parent, Res("button_gold_9s48"), cx, cy, w, h, new Color(1f, 1f, 1f, 0.96f), true, true);
            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(bg.transform, false);
            var art = (RectTransform)area.transform;
            art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one; art.offsetMin = new Vector2(22, 8); art.offsetMax = new Vector2(-22, -8);
            TextMeshProUGUI Mk(string n)
            {
                var t = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
                t.transform.SetParent(area.transform, false);
                var r = (RectTransform)t.transform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
                var tm = t.GetComponent<TextMeshProUGUI>();
                tm.font = Noto; tm.fontSize = fontSize; tm.alignment = TextAlignmentOptions.MidlineLeft; tm.raycastTarget = false;
                return tm;
            }
            TextMeshProUGUI text = Mk("Text"); text.color = Ink;
            TextMeshProUGUI ph = Mk("Placeholder"); ph.text = placeholder; ph.fontStyle = FontStyles.Italic; ph.color = new Color(0.1f, 0.07f, 0.02f, 0.45f);
            var inp = bg.gameObject.AddComponent<TMP_InputField>();
            inp.textViewport = art; inp.textComponent = text; inp.placeholder = ph;
            inp.characterLimit = limit; inp.lineType = TMP_InputField.LineType.SingleLine;
            return inp;
        }

        public static Slider MakeSlider(string name, Transform parent, float cx, float cy, float w, float h)
        {
            RectTransform root = Rt(name, parent, cx, cy, w, h);
            var slider = root.gameObject.AddComponent<Slider>();
            Image bg = Img("Background", root, Res("slider_track_9s16"), w / 2f, h / 2f, w, 26f, Color.white, true, true);
            RectTransform fillArea = Rt("Fill Area", root, w / 2f, h / 2f, w - 20f, 26f);
            Image fill = Img("Fill", fillArea, Res("slider_fill_9s16"), 0f, 13f, 20f, 20f, Color.white, false, true);
            RectTransform handleArea = Rt("Handle Slide Area", root, w / 2f, h / 2f, w - 30f, h);
            Image handle = Img("Handle", handleArea, Res("knob"), 0f, h / 2f, 46f, 46f, Color.white, true);
            // slider drives anchors of Fill/Handle: stretch fill, anchor handle
            var frt = fill.rectTransform; frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(0, 1); frt.pivot = new Vector2(0, 0.5f); frt.anchoredPosition = Vector2.zero; frt.sizeDelta = new Vector2(20, 0);
            var fa = fillArea; fa.anchorMin = new Vector2(0, 0.5f); fa.anchorMax = new Vector2(1, 0.5f); fa.pivot = new Vector2(0.5f, 0.5f); fa.anchoredPosition = Vector2.zero; fa.sizeDelta = new Vector2(-20, 26);
            var ha = handleArea; ha.anchorMin = Vector2.zero; ha.anchorMax = Vector2.one; ha.pivot = new Vector2(0.5f, 0.5f); ha.offsetMin = new Vector2(15, 0); ha.offsetMax = new Vector2(-15, 0);
            var hrt = handle.rectTransform; hrt.anchorMin = new Vector2(0, 0.5f); hrt.anchorMax = new Vector2(0, 0.5f); hrt.pivot = new Vector2(0.5f, 0.5f); hrt.sizeDelta = new Vector2(46, 46); hrt.anchoredPosition = Vector2.zero;
            var brt = bg.rectTransform; brt.anchorMin = new Vector2(0, 0.5f); brt.anchorMax = new Vector2(1, 0.5f); brt.pivot = new Vector2(0.5f, 0.5f); brt.anchoredPosition = Vector2.zero; brt.sizeDelta = new Vector2(0, 26);
            slider.fillRect = fill.rectTransform; slider.handleRect = hrt; slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight; slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            slider.transition = Selectable.Transition.None;
            return slider;
        }
    }
}
#endif
