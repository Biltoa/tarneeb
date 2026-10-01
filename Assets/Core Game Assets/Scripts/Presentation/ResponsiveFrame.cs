using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>
    /// Fits the 850 x 1850 design frame to any portrait screen. The frame is scaled to the screen's WIDTH, so the game always fills it side to side.
    /// A screen that is shorter than the design (a phone with its browser bars showing is about 0.55 wide-to-tall, the design is 0.46) is made to
    /// fit by shrinking only the table: everything above the table keeps its place from the top, everything below it keeps its place from the
    /// bottom, and elements on the table keep their size and move with it. A taller screen grows the table a little and centres the rest.
    /// Below <see cref="MinHeight"/> design units the frame is scaled by height instead and the sides show the backdrop (tablets). The menu spaces
    /// its elements evenly instead. All positions are remapped from the originals captured in Awake, so applying twice gives the same result.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ResponsiveFrame : MonoBehaviour
    {
        public const float DesignW = 850f, DesignH = 1850f;
        /// <summary>The shortest frame, in design units, the layout is built for; a shorter screen shows backdrop at the sides instead.</summary>
        public const float MinHeight = 1560f;
        public const float MaxGrow = 250f;
        private const float TableTop = 435f, TableHeight = 850f;
        private const float MenuMaxStretch = 1.12f;

        /// <summary>Scale the bid and trump panels pop up at: slightly smaller on a short screen so they fit between the side seats and the hand.</summary>
        public static float PanelScale = 1f;
        /// <summary>Scale of the cards played to the table and of the trick area that places them (smaller when the table shrinks).</summary>
        public static float TrickScale = 1f;

        /// <summary>Raised after every layout pass (scripts that cache positions of remapped elements refresh them here).</summary>
        public static event Action Applied;

        /// <summary>Layout numbers of the last pass, for tests.</summary>
        public float Scale { get; private set; }
        public float FrameHeight { get; private set; }
        public float TableShrink { get; private set; }

        private enum Kind { Point, Top, TrickArea, Panel, TableRoot, TableChild, TableShape, MenuPoint }

        private sealed class Item
        {
            public RectTransform Rt;
            public Kind Kind;
            public float Y, Height;
        }

        private static readonly string[] ShapePrefixes = { "Gold bezel", "Gold bevel", "Gold edge", "Green felt", "Felt weave" };

        private readonly List<Item> _items = new List<Item>();
        private RectTransform _rt, _background;
        private bool _game;
        private Vector2 _lastParent = new Vector2(-1f, -1f);
        private float _dt, _r, _shift, _hd;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _game = transform.Find("Table") != null;
            _background = transform.Find("Background") as RectTransform;
            if (_game) CollectGame(); else CollectMenu();
            Apply();
        }

        private void LateUpdate()
        {
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            Vector2 size = parent.rect.size;
            if (size != _lastParent) Apply();
        }

        // ------------------------------------------------------------ collecting the originals

        private void Add(Transform t, Kind kind)
        {
            var rt = t as RectTransform;
            if (rt == null) return;
            _items.Add(new Item { Rt = rt, Kind = kind, Y = -rt.anchoredPosition.y, Height = rt.sizeDelta.y });
        }

        private void AddChildren(string path, Kind kind)
        {
            Transform parent = Child(path);
            if (parent == null) return;
            foreach (Transform c in parent) Add(c, kind);
        }

        /// <summary>Path lookup by plain names (the design's own node names contain slashes, so Transform.Find cannot be used below Table).</summary>
        private Transform Child(string path)
        {
            Transform t = transform;
            foreach (string part in path.Split('/'))
            {
                t = t.Find(part);
                if (t == null) return null;
            }
            return t;
        }

        private void CollectGame()
        {
            Transform table = Child("Table");
            Add(table, Kind.TableRoot);
            foreach (Transform c in table)
            {
                bool shape = false;
                foreach (string p in ShapePrefixes) if (c.name.StartsWith(p, StringComparison.Ordinal)) shape = true;
                Add(c, shape ? Kind.TableShape : (c.name == "TrickArea" ? Kind.TrickArea : Kind.TableChild));
            }
            AddChildren("Jewels", Kind.Point);
            AddChildren("Seats", Kind.Point);
            AddChildren("HUD/Header", Kind.Point);
            AddChildren("HUD/Footer", Kind.Point);
            Add(Child("BidPanel"), Kind.Panel);
            Add(Child("TrumpPicker"), Kind.Panel);
            Add(Child("HUD/Hint"), Kind.Top);                                  // the status pill stays just below the top seat
            foreach (string p in new[] { "HUD/Toast", "Hand", "ChatUI/EmojiButton", "ChatUI/ChatButton", "ChatUI/EmojiPanel/Panel" })
                Add(Child(p), Kind.Point);
        }

        private void CollectMenu()
        {
            Transform menu = Child("Menu");
            if (menu == null) return;
            foreach (Transform c in menu) if (c.name != "Ambience") Add(c, Kind.MenuPoint);
            AddChildren("Menu/Ambience", Kind.MenuPoint);                       // rays and glow follow the cards
        }

        // ------------------------------------------------------------ layout

        /// <summary>Design y (from the top of the frame) to its place in the laid-out frame, before the centring shift.</summary>
        private float Map(float y)
        {
            if (y <= TableTop) return y;
            if (y >= TableTop + TableHeight) return y - _dt;
            return TableTop + (y - TableTop) * _r;
        }

        private static void SetY(RectTransform rt, float y) => rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -y);

        public void Apply()
        {
            var parent = _rt.parent as RectTransform;
            if (parent == null) return;
            Rect pr = parent.rect;
            if (pr.width <= 0f || pr.height <= 0f) return;
            _lastParent = pr.size;

            float sw = pr.width / DesignW;
            float s = sw;
            _hd = pr.height / sw;                                       // screen height in design units at width-fit
            if (_hd < MinHeight) { _hd = MinHeight; s = pr.height / MinHeight; }
            _rt.localScale = new Vector3(s, s, 1f);
            Scale = s;
            FrameHeight = _hd;

            float delta = DesignH - _hd;                                // > 0: the screen is shorter than the design
            _dt = Mathf.Clamp(delta, -MaxGrow, DesignH - MinHeight);
            _r = (TableHeight - _dt) / TableHeight;
            _shift = _dt * 0.5f;
            TableShrink = _dt;
            PanelScale = Mathf.Lerp(1f, 0.8f, Mathf.Clamp01(_dt / 290f));
            TrickScale = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(_dt / 290f));

            if (_background != null) _background.localScale = new Vector3(1f, _hd / DesignH, 1f);      // backdrop always covers the screen

            float menuH = Mathf.Min(_hd, DesignH * MenuMaxStretch);
            float menuK = menuH / DesignH, menuShift = (DesignH - menuH) * 0.5f;

            foreach (Item it in _items)
            {
                if (it.Rt == null) continue;
                switch (it.Kind)
                {
                    case Kind.Point: SetY(it.Rt, Map(it.Y) + _shift); break;
                    case Kind.Top: SetY(it.Rt, it.Y + _shift); break;
                    case Kind.TrickArea:
                        SetY(it.Rt, Map(TableTop + it.Y) - TableTop + 70f * Mathf.Clamp01(_dt / 290f));      // sits a little low: clear of the top seat's nameplate
                        it.Rt.localScale = new Vector3(TrickScale, TrickScale, 1f);
                        break;
                    case Kind.Panel:                                    // keeps its bottom edge a fixed gap above the hand
                        SetY(it.Rt, it.Y + it.Height * 0.5f - _dt - it.Height * 0.5f * PanelScale + _shift);
                        break;
                    case Kind.TableRoot: SetY(it.Rt, it.Y + _shift); break;
                    case Kind.TableChild: SetY(it.Rt, Map(TableTop + it.Y) - TableTop); break;
                    case Kind.TableShape:
                        SetY(it.Rt, Map(TableTop + it.Y) - TableTop);
                        it.Rt.sizeDelta = new Vector2(it.Rt.sizeDelta.x, it.Height * _r);
                        break;
                    case Kind.MenuPoint: SetY(it.Rt, menuShift + it.Y * menuK); break;
                }
            }
            Applied?.Invoke();
        }
    }
}
