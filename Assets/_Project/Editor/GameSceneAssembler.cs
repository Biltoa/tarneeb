#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Tarneeb.Game;
using Tarneeb.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tarneeb.EditorTools
{
    /// <summary>
    /// Turns the Figma import (Krykftn importer output under "Canvas") into the Game scene: keeps the artwork nodes, renames and
    /// re-parents them to the hierarchy the runtime views bind to, redraws the icons and jewels, and builds the popups and panels.
    /// Run by Tools/build_scenes.py through NAV MCP right after FigmaBundleImport.Import.
    /// </summary>
    public static class GameSceneAssembler
    {
        private const float W = 850f, H = 1850f;
        private static RectTransform _content;
        private static readonly List<string> Log = new List<string>();

        // ------------------------------------------------------------ small helpers
        private static RectTransform Find(string name, Transform root = null)
        {
            Transform r = root != null ? root : _content;
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return (RectTransform)t;
            Log.Add("MISSING node: " + name);
            return null;
        }

        private static Vector2 AbsTopLeft(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);                                  // 1 = top-left
            Vector3 local = _content.InverseTransformPoint(c[1]);   // relative to Content pivot (centre)
            return new Vector2(local.x + W / 2f, H / 2f - local.y);
        }

        /// <summary>Rt in absolute Figma frame coordinates (centre), whatever the parent's own position.</summary>
        private static RectTransform RtAbs(string name, RectTransform parent, float ax, float ay, float w, float h)
        {
            Vector2 tl = AbsTopLeft(parent);
            return UiKit.Rt(name, parent, ax - tl.x, ay - tl.y, w, h);
        }

        private static void Rename(RectTransform rt, string name) { if (rt != null) rt.name = name; }

        private static void Reparent(RectTransform rt, Transform parent, string newName = null)
        {
            if (rt == null) return;
            rt.SetParent(parent, true);
            if (newName != null) rt.name = newName;
        }

        internal static void StripFigmaComponents(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component c in t.GetComponents<Component>().ToArray())
                {
                    if (c == null) continue;
                    string n = c.GetType().Name;
                    if (n == "FigmaImportInfo" || n == "FigmaTextRounding" || n == "ContentSizeFitter" || n == "LayoutElement"
                        || n == "HorizontalLayoutGroup" || n == "VerticalLayoutGroup" || n == "GridLayoutGroup")
                        Object.DestroyImmediate(c);
                }
            }
        }

        private static void DisableRaycasts(Transform root)
        {
            foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        private static void MakeButton(RectTransform rt, Graphic target)
        {
            var b = rt.gameObject.AddComponent<Button>();
            target.raycastTarget = true;
            b.targetGraphic = target;
            b.transition = Selectable.Transition.None;
            rt.gameObject.AddComponent<ButtonFx>();
        }

        private static void CenterText(TMP_Text t, float width, float size = -1f, TextAlignmentOptions a = TextAlignmentOptions.Center)
        {
            if (t == null) return;
            var rt = (RectTransform)t.transform;
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            t.alignment = a;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            if (size > 0f) t.fontSize = size;
            t.raycastTarget = false;
        }

        // ------------------------------------------------------------ entry point
        public static string Assemble()
        {
            Log.Clear();
            UiKit.LoadFonts();

            // ---- canvas and content frame
            var canvas = Object.FindFirstObjectByType<Canvas>();
            canvas.name = "GameCanvas";
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var screen = (RectTransform)canvas.transform.GetChild(0);
            screen.name = "Content";
            _content = screen;
            StripFigmaComponents(_content);
            screen.anchorMin = screen.anchorMax = new Vector2(0.5f, 0.5f);
            screen.pivot = new Vector2(0.5f, 0.5f);
            screen.anchoredPosition = Vector2.zero;
            screen.sizeDelta = new Vector2(W, H);

            var back = UiKit.Fill("Background", canvas.transform);
            back.SetSiblingIndex(0);
            var bi = back.gameObject.AddComponent<Image>();
            bi.color = new Color32(0, 24, 26, 255);
            bi.raycastTarget = true;

            var fit = screen.gameObject.AddComponent<FitScale>();
            fit.designSize = new Vector2(W, H);
            screen.gameObject.AddComponent<TableView>();
            screen.gameObject.AddComponent<TableAmbience>();

            // ---- layers
            RectTransform bg = Find("01 / Background"); Rename(bg, "Background");
            RectTransform table = Find("02 / Table"); Rename(table, "Table");
            BuildTable(table);
            RectTransform jewels = BuildJewels();
            RectTransform vig = UiKit.Fill("Vignette", _content);
            var vi = vig.gameObject.AddComponent<Image>(); vi.sprite = UiKit.Res("vignette"); vi.color = new Color(1, 1, 1, 0.6f); vi.raycastTarget = false;
            RectTransform seats = UiKit.Rt("Seats", _content, W / 2f, H / 2f, W, H);
            BuildSeats(seats);
            RectTransform hand = UiKit.Rt("Hand", _content, 425f, 1408f, W, 210f);
            hand.gameObject.AddComponent<HandView>();
            RectTransform cardLayer = UiKit.Rt("CardLayer", _content, W / 2f, H / 2f, W, H);
            RectTransform hud = BuildHud();
            RectTransform fx = UiKit.Rt("FxLayer", _content, W / 2f, H / 2f, W, H);
            fx.gameObject.AddComponent<UiFx>();
            BuildChat();
            BuildBidPanel();
            BuildTrumpPicker();
            BuildRoundSummary();
            BuildGameOver();
            BuildPause();
            BuildSettings();

            // ---- discard the static demo content of the design
            foreach (string n in new[] { "Opponent hand / Omar", "Opponent hand / Sara", "Opponent hand / Khalid", "04 / Active player highlight",
                "Countdown / Active player", "05 / Current trick", "06 / Local player hand", "03 / Header and bidding", "07 / Footer controls",
                "Jewel / Left upper", "Jewel / Right upper", "Jewel / Left lower", "Jewel / Right lower", "Jewel / Bottom center" })
            {
                foreach (Transform t in _content.Cast<Transform>().ToArray())       // (Transform.Find would read the " / " in names as a path)
                    if (t.name == n) Object.DestroyImmediate(t.gameObject);
            }

            // ---- draw order under Content
            string[] order = { "Background", "Table", "Jewels", "Vignette", "Seats", "Hand", "CardLayer", "HUD", "FxLayer", "ChatUI", "BidPanel", "TrumpPicker",
                "RoundSummary", "GameOver", "PauseMenu", "SettingsPanel" };
            for (int i = 0; i < order.Length; i++) { Transform t = _content.Find(order[i]); if (t != null) t.SetSiblingIndex(i); else Log.Add("order: missing " + order[i]); }

            // ---- nested canvases: an animated element only rebuilds its own canvas, not the whole 700-object screen
            foreach (string n in new[] { "Background", "Table", "Jewels", "Vignette" }) SubCanvas(_content.Find(n), false);
            foreach (string n in new[] { "Seats", "CardLayer", "FxLayer" }) SubCanvas(_content.Find(n), false);
            foreach (string n in new[] { "Hand", "HUD", "ChatUI" }) SubCanvas(_content.Find(n), true);

            // ---- fader on the canvas, singletons outside it
            var fader = UiKit.Fill("Fader", canvas.transform);
            fader.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);       // ScreenFader turns it black at runtime
            fader.gameObject.AddComponent<ScreenFader>();
            fader.SetAsLastSibling();

            BuildSingletons();
            BuildCardPrefab();
            DisableUnwantedRaycasts();
            foreach (Button b in _content.GetComponentsInChildren<Button>(true)) if (b.GetComponent<ButtonFx>() == null) b.gameObject.AddComponent<ButtonFx>();
            Log.Insert(0, "assembled");
            return string.Join("\n", Log);
        }

        private static void SubCanvas(Transform t, bool interactive)
        {
            if (t == null) { Log.Add("SubCanvas: missing"); return; }
            t.gameObject.AddComponent<Canvas>();
            if (interactive) t.gameObject.AddComponent<GraphicRaycaster>();
        }

        private static void DisableUnwantedRaycasts()
        {
            // decoration must not eat clicks: only Buttons/Sliders/InputFields/dims keep their raycast targets
            foreach (Graphic g in _content.GetComponentsInChildren<Graphic>(true))
            {
                if (g.GetComponentInParent<Selectable>(true) != null) continue;
                if (g.name == "Dim") continue;
                g.raycastTarget = false;
            }
        }

        // ------------------------------------------------------------ table
        private static void BuildTable(RectTransform table)
        {
            RectTransform deck = RtAbs("DeckAnchor", table, 425f, 865f, 10f, 10f);
            // a small stack of card backs shown while dealing
            RectTransform pile = UiKit.Rt("Pile", deck, 5f, 5f, 90f, 130f);
            for (int i = 0; i < 4; i++)
            {
                var im = UiKit.Img("B" + i, pile, null, 45f + i * 1.5f, 65f - i * 1.5f, 62f, 94f);
                im.sprite = null;                                    // sprite is assigned at runtime by CardArt.Back (see TableView)
                im.gameObject.AddComponent<BackSprite>();
            }
            RectTransform area = RtAbs("TrickArea", table, 425f, 835f, 400f, 400f);
            var slots = new (string, float, float)[] { ("Bottom", 430.5f, 984.5f), ("Right", 518.5f, 833.5f), ("Top", 424.5f, 699.5f), ("Left", 330.5f, 833.5f) };
            foreach (var (n, x, y) in slots) RtAbs("Slot_" + n, area, x, y, 10f, 10f);
        }

        /// <summary>
        /// Five identical jewels centred on the gold bezel band. Positions come from measuring the bezel/felt silhouettes of the design
        /// (Tools notes in DEVLOG): the band centre at y is x = 147.2 - 0.0701 * (y - 600) from the left, mirrored about x = 425.
        /// </summary>
        private static RectTransform BuildJewels()
        {
            RectTransform g = UiKit.Rt("Jewels", _content, W / 2f, H / 2f, W, H);
            Sprite jewel = UiKit.Res("jewel");
            float[] ys = { 600f, 1060f };
            foreach (float y in ys)
            {
                float xl = 147.2f - 0.0701f * (y - 600f);
                UiKit.Img("Jewel_L" + (int)y, g, jewel, xl, y, 34f, 38f);
                UiKit.Img("Jewel_R" + (int)y, g, jewel, W - xl, y, 34f, 38f);
            }
            UiKit.Img("Jewel_Bottom", g, jewel, 425f, 1251.5f, 46f, 52f);
            return g;
        }

        // ------------------------------------------------------------ seats
        private static void BuildSeats(RectTransform seats)
        {
            var map = new (string figma, string view, float fanDy, bool bubble, Vector2 bubblePos, Vector2 chatPos)[]
            {
                ("Player / Omar",   "Top",    -83f, true,  new Vector2(255f, 83f),  new Vector2(285f, 30f)),
                ("Player / Sara",   "Left",   -95f, true,  new Vector2(85f, -92f),  new Vector2(205f, 270f)),
                ("Player / Khalid", "Right",  -95f, true,  new Vector2(85f, -92f),  new Vector2(-35f, 270f)),
                ("Player / Ahmad",  "Bottom", 0f,   false, Vector2.zero,            new Vector2(-169f, 22f)),
            };
            Sprite glow = UiKit.Fig("3_310"), rim = UiKit.Fig("3_311"), rays = UiKit.Fig("7_341");
            Sprite bezel = UiKit.Fig("3_353"), face = UiKit.Fig("3_354");
            foreach (var m in map)
            {
                RectTransform seat = Find(m.figma);
                if (seat == null) continue;
                Reparent(seat, seats, "Seat_" + m.view);
                Rename(Find("Gold avatar ring", seat), "Ring");
                Rename(Find("Inner ring highlight", seat), "RingHighlight");
                foreach (Transform t in seat)
                    if (t.name.StartsWith("Portrait artwork"))
                    {
                        t.name = "Portrait";
                        t.GetComponent<Image>().sprite = UiKit.Res("portrait_" + m.figma.Substring(m.figma.IndexOf('/') + 2).ToLowerInvariant());   // clean circle, see Tools/gen_portraits.py
                    }
                Rename(Find("Nameplate / decoration", seat), "Nameplate");
                Rename(Find("Player name", seat), "NameLabel");
                Rename(Find("Score capsule / decoration", seat), "TricksCapsule");
                Rename(Find("Tricks won", seat), "TricksText");
                CenterText(Find("NameLabel", seat).GetComponent<TMP_Text>(), 164f);
                CenterText(Find("TricksText", seat).GetComponent<TMP_Text>(), 74f);
                DisableRaycasts(seat);

                // fan of card backs sits behind everything else
                RectTransform fan = UiKit.Rt("Fan", seat, 85f, 83f + m.fanDy, 10f, 10f);
                fan.SetSiblingIndex(0);
                if (m.view != "Top" && m.view != "Bottom") fan.localScale = Vector3.one * 0.85f;

                // turn glow group (behind the ring, in front of the fan)
                RectTransform turn = UiKit.Rt("Turn", seat, 85f, 83f, 10f, 10f);
                UiKit.Group(turn.gameObject);
                UiKit.Img("Rays", turn, rays, 0f, 0f, 208f, 208f);
                UiKit.Img("Glow", turn, glow, 0f, 0f, 187f, 187f);
                UiKit.Img("Rim", turn, rim, 0f, 0f, 171f, 171f);
                foreach (Transform c in turn)              // centre-anchored so scaling and rotating pivot on the ring centre
                {
                    var rt = (RectTransform)c;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                }
                turn.SetSiblingIndex(1);

                // small team marker inside the nameplate
                UiKit.Img("TeamMark", seat, UiKit.Res("glow_white"), 17f, 152.5f, 14f, 14f, Color.white);
                // timer badge (shown for the local seat when a turn limit is active)
                RectTransform timer = UiKit.Rt("Timer", seat, 36f, 16f, 74f, 74f);
                UiKit.Img("Bezel", timer, bezel, 37f, 37f, 74f, 74f);
                UiKit.Img("Face", timer, face, 37f, 37f, 60f, 60f);
                Image arc = UiKit.Img("Arc", timer, UiKit.Res("ring_pulse"), 37f, 37f, 66f, 66f, UiKit.Gold);
                arc.type = Image.Type.Filled; arc.fillMethod = Image.FillMethod.Radial360; arc.fillOrigin = 2; arc.fillClockwise = false; arc.fillAmount = 1f;
                TextMeshProUGUI secs = UiKit.Text("Seconds", timer, "15", 37f, 38f, 60f, 40f, 28f, UiKit.Cream, true);
                secs.fontStyle = FontStyles.Bold;
                // dealer chip
                RectTransform dealer = UiKit.Rt("DealerChip", seat, 23f, 21f, 42f, 42f);
                var di = dealer.gameObject.AddComponent<Image>(); di.sprite = UiKit.Res("button_round"); di.raycastTarget = false;
                UiKit.Text("Text", dealer, "D", 21f, 22f, 40f, 40f, 24f, UiKit.Cream).fontStyle = FontStyles.Bold;
                // bid bubble
                if (m.bubble)
                {
                    Image bub = UiKit.Img("Bubble", seat, UiKit.Res("button_gold_9s48"), m.bubblePos.x, m.bubblePos.y, 150f, 50f, Color.white, false, true);
                    UiKit.Text("BubbleText", bub.transform, "PASS", 75f, 26f, 140f, 44f, 26f, UiKit.Ink).fontStyle = FontStyles.Bold;
                }
                // chat bubble and emoji badge
                Image chat = UiKit.Img("ChatBubble", seat, UiKit.Res("button_gold_9s48"), m.chatPos.x, m.chatPos.y, 300f, 100f, Color.white, false, true);
                UiKit.Text("Text", chat.transform, "Good luck!", 150f, 52f, 276f, 88f, 26f, UiKit.Ink, true, TextAlignmentOptions.Center, true);
                RectTransform badge = UiKit.Rt("EmojiBadge", seat, m.chatPos.x, m.chatPos.y, 120f, 120f);
                UiKit.Img("Icon", badge, null, 60f, 60f, 120f, 120f);

                // the VIP crown on the local player's portrait
                if (m.view == "Bottom") BuildVipBadge(seat, 156.5f, 111.5f, "VipBadge");
                seat.gameObject.AddComponent<SeatView>();
            }
            // the design's per-team accent for the nameplates is applied at runtime (SeatView.Setup)
        }

        /// <summary>Redrawn crown badge (the Figma vector crown is uneven): gold crown icon with a dark "VIP" label.</summary>
        private static RectTransform BuildVipBadge(Transform parent, float cx, float cy, string name)
        {
            RectTransform b = UiKit.Rt(name, parent, cx, cy, 75f, 69f);
            UiKit.Img("Crown", b, UiKit.Res("icon_crown"), 37.5f, 34.5f, 75f, 69f);
            TextMeshProUGUI t = UiKit.Text("Label", b, "VIP", 37.5f, 44f, 60f, 30f, 22f, new Color32(72, 44, 4, 255));
            t.fontStyle = FontStyles.Bold;
            return b;
        }

        // ------------------------------------------------------------ HUD
        private static RectTransform BuildHud()
        {
            RectTransform hud = UiKit.Rt("HUD", _content, W / 2f, H / 2f, W, H);
            hud.gameObject.AddComponent<HudView>();
            RectTransform header = UiKit.Rt("Header", hud, W / 2f, H / 2f, W, H);
            RectTransform footer = UiKit.Rt("Footer", hud, W / 2f, H / 2f, W, H);

            // menu + settings buttons (Figma gold-framed emerald controls with redrawn icons)
            foreach (var (src, iconNode, name, sprite, icon) in new[] { ("Control / Menu", "Menu icon", "MenuButton", "icon_menu", 40f), ("Control / Settings", "Settings icon", "SettingsButton", "icon_gear", 46f) })
            {
                RectTransform ctl = Find(src);
                Reparent(ctl, header, name);
                { RectTransform ic0 = Find(iconNode); if (ic0 != null) Object.DestroyImmediate(ic0.gameObject); }
                Graphic face = ctl.Find("Emerald button face").GetComponent<Graphic>();
                MakeButton(ctl, face);
                // icon centred on the button; the hamburger and gear now share one optical size and colour
                RectTransform ic = UiKit.Rt("Icon", ctl, ctl.rect.width / 2f, ctl.rect.height / 2f, icon, icon);
                var im = ic.gameObject.AddComponent<Image>(); im.sprite = UiKit.Res(sprite); im.raycastTarget = false;
            }
            // title and VIP badge
            RectTransform title = Find("Title"); Reparent(title, header, "Title");
            title.GetComponent<TMP_Text>().raycastTarget = false;
            title.anchoredPosition += new Vector2(12f, 0f);                     // the title + crown group was 12 px left of the frame centre
            RectTransform vipSrc = Find("Title VIP badge");
            Vector2 tl = AbsTopLeft(vipSrc);
            var vipPos = new Vector2(tl.x + 37.5f + 12f, tl.y + 34.5f);
            BuildVipBadge(header, vipPos.x, vipPos.y, "VipBadge");

            // scoreboard
            Reparent(Find("Team scoreboard / decoration"), header, "Scoreboard");
            foreach (var (src, dst) in new[] { ("Score / US", "LabelUs"), ("Score / 12", "ScoreUs"), ("Score / :", "Colon"), ("Score / 8", "ScoreThem"), ("Score / THEM", "LabelThem") })
                Reparent(Find(src), header, dst);
            CenterText(Find("ScoreUs", header).GetComponent<TMP_Text>(), 70f);
            CenterText(Find("ScoreThem", header).GetComponent<TMP_Text>(), 70f);
            ((TMP_Text)Find("LabelUs", header).GetComponent<TMP_Text>()).color = new Color32(0, 221, 232, 255);
            ((TMP_Text)Find("LabelThem", header).GetComponent<TMP_Text>()).color = new Color32(250, 153, 71, 255);
            Find("Team score / editable labels")?.gameObject.SetActive(false);

            // bid capsule and trump medallion
            Reparent(Find("Bid capsule / decoration"), header, "BidCapsule");
            Reparent(Find("Bid label"), header, "BidLabel");
            Reparent(Find("Bid value"), header, "BidValue");
            CenterText(Find("BidValue", header).GetComponent<TMP_Text>(), 44f);
            Find("BidLabel", header).anchoredPosition += new Vector2(-14f, 0f);       // room for two-digit bids
            Find("BidValue", header).anchoredPosition += new Vector2(6f, 0f);
            RectTransform med = Find("Trump medallion"); Reparent(med, header, "TrumpMedallion");
            Find("Trump suit")?.gameObject.SetActive(false);
            UiKit.Img("Icon", med, UiKit.Res("suit_spade"), 30f, 30f, 36f, 36f);
            UiKit.Text("Unknown", med, "?", 30f, 32f, 40f, 44f, 34f, new Color32(112, 72, 10, 255), true).fontStyle = FontStyles.Bold;
            foreach (Transform t in header) { var g = t.GetComponent<Graphic>(); if (g != null && g.GetComponentInParent<Button>() == null) g.raycastTarget = false; }
            // BidCapsule children (shadow/background) were laid out by the importer inside the capsule; keep them behind its texts
            Find("BidCapsule", header).SetSiblingIndex(1);
            Find("Scoreboard", header).SetSiblingIndex(0);

            // footer: contract chip, chat and emote buttons
            RectTransform chipSrc = Find("Contract / decoration");
            Reparent(chipSrc, footer, "ContractChip");
            Reparent(Find("Contract / bid count"), chipSrc, "TrickCount");
            CenterText(chipSrc.Find("TrickCount").GetComponent<TMP_Text>(), 70f);
            RectTransform chipIcon = UiKit.Rt("SuitIcon", chipSrc, 50f, 48.5f, 42f, 42f);
            var ci = chipIcon.gameObject.AddComponent<Image>(); ci.sprite = UiKit.Res("suit_spade_w"); ci.color = UiKit.Cream; ci.raycastTarget = false;
            // shift the count to the right of the icon
            ((RectTransform)chipSrc.Find("TrickCount")).anchoredPosition = new Vector2(133f, -50f);
            Find("Contract / suit")?.gameObject.SetActive(false);

            // hint pill and toast
            RectTransform hint = UiKit.Rt("Hint", hud, 425f, 585f, 440f, 46f);
            UiKit.Group(hint.gameObject);
            UiKit.Img("Bg", hint, UiKit.Res("panel_soft_9s52"), 220f, 23f, 440f, 46f, new Color(1, 1, 1, 0.92f), false, true);
            UiKit.Text("Text", hint, "Your turn", 220f, 24f, 420f, 44f, 26f, UiKit.Cream);
            RectTransform toast = UiKit.Rt("Toast", hud, 425f, 865f, 560f, 104f);
            UiKit.Group(toast.gameObject);
            UiKit.Img("Bg", toast, UiKit.Res("panel_dark_9s56"), 280f, 52f, 560f, 104f, Color.white, false, true);
            TextMeshProUGUI tt = UiKit.Text("Text", toast, "", 280f, 54f, 530f, 90f, 38f, UiKit.Gold);
            tt.fontStyle = FontStyles.Bold;
            return hud;
        }

        // ------------------------------------------------------------ chat ui (moved onto the Figma controls)
        private static void BuildChat()
        {
            RectTransform ui = UiKit.Rt("ChatUI", _content, W / 2f, H / 2f, W, H);
            ui.gameObject.AddComponent<ChatUI>();
            // Figma 'Control / Emotes' and 'Control / Chat' keep their look; the icons are redrawn
            foreach (var (src, iconNode, name, sprite, size) in new[] { ("Control / Emotes", "Smile icon", "EmojiButton", "icon_smile", 50f), ("Control / Chat", "Chat icon", "ChatButton", "icon_chat", 52f) })
            {
                RectTransform ctl = Find(src);
                Reparent(ctl, ui, name);
                ctl.anchoredPosition += new Vector2(15f, 0f);                    // right margin now matches the contract chip's left margin (36 px)
                { RectTransform ic0 = Find(iconNode); if (ic0 != null) Object.DestroyImmediate(ic0.gameObject); }
                MakeButton(ctl, ctl.Find("Emerald button face").GetComponent<Graphic>());
                ctl.Find("Gold button frame").GetComponent<Graphic>().raycastTarget = false;
                RectTransform ic = UiKit.Rt("Icon", ctl, 51f, 51f, size, size);
                var im = ic.gameObject.AddComponent<Image>(); im.sprite = UiKit.Res(sprite); im.raycastTarget = false;
                if (name == "ChatButton")
                {
                    RectTransform dot = UiKit.Rt("Unread", ctl, 84f, 16f, 26f, 26f);
                    var d = dot.gameObject.AddComponent<Image>(); d.sprite = UiKit.Res("button_round"); d.color = new Color(1f, 0.3f, 0.3f); d.raycastTarget = false;
                }
            }
            // emoji picker
            RectTransform ep = UiKit.Fill("EmojiPanel", ui);
            var dimRt = UiKit.Fill("Dim", ep);
            var dimImg = dimRt.gameObject.AddComponent<Image>(); dimImg.color = new Color(0, 0, 0, 0.55f);
            var dimBtn = dimRt.gameObject.AddComponent<Button>(); dimBtn.targetGraphic = dimImg; dimBtn.transition = Selectable.Transition.None;
            Image panel = UiKit.Img("Panel", ep, UiKit.Res("panel_dark_9s56"), 425f, 1140f, 720f, 600f, Color.white, true, true);
            float[] xs = { -255f, -85f, 85f, 255f }, ys = { -175f, 0f, 175f };
            for (int i = 0; i < 12; i++)
            {
                int col = i % 4, row = i / 4;
                Image cell = UiKit.Img("Emoji_" + i, panel.transform, UiKit.Res("slot_9s36"), 360f + xs[col], 300f + ys[row], 150f, 150f, Color.white, true, true);
                cell.gameObject.AddComponent<Button>().targetGraphic = cell;
                cell.GetComponent<Button>().transition = Selectable.Transition.None;
                cell.gameObject.AddComponent<ButtonFx>();
                UiKit.Img("Icon", cell.transform, null, 75f, 75f, 108f, 108f);
            }
            // chat window
            RectTransform cp = UiKit.Fill("ChatPanel", ui);
            var cdim = UiKit.Fill("Dim", cp); var cdi = cdim.gameObject.AddComponent<Image>(); cdi.color = new Color(0, 0, 0, 0.6f);
            var cb = cdim.gameObject.AddComponent<Button>(); cb.targetGraphic = cdi; cb.transition = Selectable.Transition.None;
            Image cpn = UiKit.Img("Panel", cp, UiKit.Res("panel_dark_9s56"), 425f, 800f, 740f, 900f, Color.white, true, true);
            UiKit.Text("Title", cpn.transform, "CHAT", 370f, 62f, 500f, 70f, 46f, UiKit.Gold).fontStyle = FontStyles.Bold;
            Button close = UiKit.IconBtn("Close", cpn.transform, 676f, 62f, 76f, "icon_close", 34f);
            Image logBg = UiKit.Img("LogBg", cpn.transform, UiKit.Res("panel_soft_9s52"), 370f, 330f, 660f, 400f, new Color(1, 1, 1, 0.5f), false, true);
            TextMeshProUGUI log = UiKit.Text("Log", cpn.transform, "", 370f, 330f, 620f, 370f, 28f, Color.white, true, TextAlignmentOptions.BottomLeft, true);
            log.overflowMode = TextOverflowModes.Truncate; log.richText = true;
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3, row = i / 3;
                UiKit.Btn("Quick_" + i, cpn.transform, "button_emerald_9s48", 130f + col * 240f, 590f + row * 84f, 224f, 68f, "Quick", 22f, UiKit.Cream);
            }
            UiKit.Input("Input", cpn.transform, 250f, 790f, 460f, 84f, "Type a message...", 60, 30f);
            UiKit.Btn("Send", cpn.transform, "button_gold_9s48", 598f, 790f, 170f, 84f, "SEND", 28f, UiKit.Ink);
            ep.gameObject.SetActive(true); cp.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------ bid + trump + summary + game over
        private static Image Panel(string name, float cx, float cy, float w, float h, string title, float titleY = 60f)
        {
            RectTransform root = UiKit.Rt(name, _content, cx, cy, w, h);
            var im = root.gameObject.AddComponent<Image>();
            im.sprite = UiKit.Res("panel_dark_9s56"); im.type = Image.Type.Sliced; im.raycastTarget = true;
            UiKit.Group(root.gameObject);
            if (!string.IsNullOrEmpty(title)) UiKit.Text("Title", root, title, w / 2f, titleY, w - 60f, 60f, 36f, UiKit.Gold).fontStyle = FontStyles.Bold;
            return im;
        }

        private static void BuildBidPanel()
        {
            Image p = Panel("BidPanel", 425f, 1130f, 660f, 300f, "YOUR BID", 46f);
            p.gameObject.AddComponent<BidPanel>();
            float[] xs = { 100f, 248f, 396f, 544f };
            for (int n = 7; n <= 13; n++)
            {
                int i = n - 7, row = i / 4, col = i % 4;
                UiKit.Btn("Bid" + n, p.transform, "button_gold_9s48", xs[col] + 6f, 128f + row * 100f, 130f, 84f, n.ToString(), 44f, UiKit.Ink, true);
            }
            UiKit.Btn("Pass", p.transform, "button_red_9s48", xs[3] + 6f, 128f + 100f, 130f, 84f, "PASS", 28f, Color.white);
        }

        private static void BuildTrumpPicker()
        {
            Image p = Panel("TrumpPicker", 425f, 1130f, 660f, 300f, "CHOOSE TRUMP", 46f);
            p.gameObject.AddComponent<TrumpPicker>();
            string[] names = { "Clubs", "Diamonds", "Hearts", "Spades" };
            float[] xs = { 100f, 248f, 396f, 544f };
            for (int i = 0; i < 4; i++)
            {
                Button b = UiKit.Btn("Pick_" + names[i], p.transform, "button_gold_9s48", xs[i] + 6f, 176f, 128f, 170f, "", 20f, UiKit.Ink);
                UiKit.Img("Icon", b.transform, null, 64f, 74f, 84f, 84f);
                UiKit.Text("Name", b.transform, names[i].ToUpperInvariant(), 64f, 142f, 124f, 30f, 16f, UiKit.Ink).fontStyle = FontStyles.Bold;
            }
        }

        private static void BuildRoundSummary()
        {
            RectTransform root = UiKit.Fill("RoundSummary", _content);
            UiKit.Group(root.gameObject);
            var dim = UiKit.Fill("Dim", root); var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.03f, 0.04f, 0.72f);
            Image p = UiKit.Img("Panel", root, UiKit.Res("panel_dark_9s56"), 425f, 900f, 680f, 760f, Color.white, true, true);
            UiKit.Img("Badge", p.transform, UiKit.Res("icon_check"), 340f, 96f, 96f, 96f, UiKit.Gold);
            UiKit.Text("Title", p.transform, "Contract made", 340f, 190f, 620f, 70f, 44f, UiKit.Gold).fontStyle = FontStyles.Bold;
            UiKit.Text("Body", p.transform, "", 340f, 400f, 600f, 320f, 30f, Color.white, true, TextAlignmentOptions.Center, true).richText = true;
            UiKit.Btn("Continue", p.transform, "button_gold_9s48", 340f, 660f, 400f, 92f, "NEXT ROUND", 32f, UiKit.Ink);
            root.gameObject.AddComponent<RoundSummaryView>();
        }

        private static void BuildGameOver()
        {
            RectTransform root = UiKit.Fill("GameOver", _content);
            UiKit.Group(root.gameObject);
            var dim = UiKit.Fill("Dim", root); var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.03f, 0.04f, 0.8f);
            Image p = UiKit.Img("Panel", root, UiKit.Res("panel_dark_9s56"), 425f, 900f, 680f, 800f, Color.white, true, true);
            UiKit.Img("Crown", p.transform, UiKit.Res("icon_crown"), 340f, 110f, 160f, 148f);
            UiKit.Text("Title", p.transform, "YOU WIN!", 340f, 250f, 620f, 90f, 60f, UiKit.Gold).fontStyle = FontStyles.Bold;
            UiKit.Text("Body", p.transform, "", 340f, 440f, 600f, 240f, 34f, Color.white, true, TextAlignmentOptions.Center, true).richText = true;
            UiKit.Btn("PlayAgain", p.transform, "button_gold_9s48", 180f, 690f, 290f, 92f, "PLAY AGAIN", 28f, UiKit.Ink);
            UiKit.Btn("MenuBtn", p.transform, "button_emerald_9s48", 500f, 690f, 290f, 92f, "MENU", 28f, UiKit.Cream);
            root.gameObject.AddComponent<GameOverView>();
        }

        // ------------------------------------------------------------ pause + settings
        private static void BuildPause()
        {
            RectTransform root = UiKit.Fill("PauseMenu", _content);
            UiKit.Group(root.gameObject);
            var dim = UiKit.Fill("Dim", root); var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.03f, 0.04f, 0.7f);
            var dimBtn = dim.gameObject.AddComponent<Button>(); dimBtn.targetGraphic = di; dimBtn.transition = Selectable.Transition.None;
            Image p = UiKit.Img("Panel", root, UiKit.Res("panel_dark_9s56"), 425f, 900f, 520f, 520f, Color.white, true, true);
            UiKit.Text("Title", p.transform, "PAUSED", 260f, 66f, 460f, 60f, 40f, UiKit.Gold).fontStyle = FontStyles.Bold;
            UiKit.Btn("Resume", p.transform, "button_gold_9s48", 260f, 170f, 360f, 92f, "RESUME", 30f, UiKit.Ink);
            UiKit.Btn("Settings", p.transform, "button_emerald_9s48", 260f, 282f, 360f, 92f, "SETTINGS", 30f, UiKit.Cream);
            UiKit.Btn("Leave", p.transform, "button_red_9s48", 260f, 394f, 360f, 92f, "LEAVE TABLE", 28f, Color.white);
            root.gameObject.AddComponent<PauseMenu>();
        }

        /// <summary>Shared by the Game and Menu scenes (SettingsPanel finds itself by name).</summary>
        public static RectTransform BuildSettingsPanel(Transform parent, float centerX, float centerY)
        {
            RectTransform root = UiKit.Fill("SettingsPanel", parent);
            UiKit.Group(root.gameObject);
            var dim = UiKit.Fill("Dim", root); var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.03f, 0.04f, 0.78f);
            var dimBtn = dim.gameObject.AddComponent<Button>(); dimBtn.targetGraphic = di; dimBtn.transition = Selectable.Transition.None;
            Image p = UiKit.Img("Panel", root, UiKit.Res("panel_dark_9s56"), centerX, centerY, 700f, 940f, Color.white, true, true);
            UiKit.Text("Title", p.transform, "SETTINGS", 350f, 68f, 500f, 70f, 46f, UiKit.Gold).fontStyle = FontStyles.Bold;
            UiKit.IconBtn("Close", p.transform, 640f, 66f, 76f, "icon_close", 34f);
            RectTransform main = UiKit.Rt("Main", p.transform, 350f, 470f, 700f, 760f);

            // sound effects and music: icon, label, switch, slider
            foreach (var (name, label, icon, y) in new[] { ("Sfx", "SOUND EFFECTS", "icon_sound_on", 70f), ("Music", "MUSIC", "icon_music_on", 230f) })
            {
                RectTransform row = UiKit.Rt(name, main, 350f, y, 640f, 130f);
                UiKit.Img("Icon", row, UiKit.Res(icon), 36f, 32f, 54f, 54f);
                UiKit.Text("Label", row, label, 260f, 32f, 360f, 44f, 28f, UiKit.Cream, false, TextAlignmentOptions.Left);
                RectTransform tg = UiKit.Rt("Toggle", row, 560f, 32f, 130f, 64f);
                UiKit.Img("Track", tg, UiKit.Res("toggle_track_on_9s40"), 65f, 32f, 130f, 64f, Color.white, true, true);
                UiKit.Img("Knob", tg, UiKit.Res("knob"), 95f, 32f, 50f, 50f);
                tg.gameObject.AddComponent<ToggleSwitch>();
                UiKit.MakeSlider("Slider", row, 320f, 100f, 600f, 50f);
            }
            // segmented options
            foreach (var (name, label, y, a, b) in new[] { ("Speed", "GAME SPEED", 400f, "NORMAL", "FAST"), ("Bots", "BOT SKILL", 545f, "EASY", "NORMAL") })
            {
                RectTransform row = UiKit.Rt(name, main, 350f, y, 640f, 110f);
                UiKit.Text("Label", row, label, 170f, 30f, 320f, 44f, 28f, UiKit.Cream, false, TextAlignmentOptions.Left);
                UiKit.Btn("Opt0", row, "button_emerald_9s48", 385f, 32f, 170f, 64f, a, 24f, UiKit.Cream);
                UiKit.Btn("Opt1", row, "button_emerald_9s48", 565f, 32f, 170f, 64f, b, 24f, UiKit.Cream);
            }
            UiKit.Btn("CreditsButton", main, "button_emerald_9s48", 350f, 690f, 420f, 76f, "CREDITS & LICENCES", 24f, UiKit.Cream);

            RectTransform cred = UiKit.Rt("Credits", p.transform, 350f, 470f, 700f, 760f);
                        TextMeshProUGUI body = UiKit.Text("Body", cred, Credits.Text, 350f, 370f, 620f, 620f, 20f, Color.white, true, TextAlignmentOptions.TopLeft, true);
            body.richText = true; body.overflowMode = TextOverflowModes.Truncate;
            UiKit.Btn("Back", cred, "button_gold_9s48", 350f, 715f, 300f, 74f, "BACK", 28f, UiKit.Ink);
            cred.gameObject.SetActive(false);
            root.gameObject.AddComponent<SettingsPanel>();
            return root;
        }

        private static void BuildSettings() => BuildSettingsPanel(_content, 425f, 925f);

        // ------------------------------------------------------------ singletons, prefab, audio
        private static void BuildSingletons()
        {
            foreach (string n in new[] { "UiFont", "Audio", "GameController", "EventSystem" })
            {
                var old = GameObject.Find(n);
                if (old != null) Object.DestroyImmediate(old);
            }
            var fonts = new GameObject("UiFont").AddComponent<UiFont>();
            fonts.title = UiKit.Cinzel; fonts.number = UiKit.Noto;
            EditorSceneHelpers.EnsureAudio();
            new GameObject("GameController").AddComponent<GameController>();
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0, 24, 26, 255); }
        }

        private static void BuildCardPrefab()
        {
            var root = new GameObject("CardView", typeof(RectTransform), typeof(CanvasGroup), typeof(CardView));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(CardView.Width, CardView.Height);
            var hit = root.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0f); hit.raycastTarget = true;
            Image Child(string n, Sprite s, Vector2 size, Vector2 off, Color c, bool sliced = false)
            {
                var g = new GameObject(n, typeof(RectTransform), typeof(Image));
                g.transform.SetParent(root.transform, false);
                var r = (RectTransform)g.transform; r.sizeDelta = size; r.anchoredPosition = off;
                var im = g.GetComponent<Image>(); im.sprite = s; im.color = c; im.raycastTarget = false;
                if (sliced) im.type = Image.Type.Sliced;
                return im;
            }
            Child("Shadow", UiKit.Res("card_shadow_9s50"), new Vector2(CardView.Width + 36f, CardView.Height + 36f), new Vector2(3f, -9f), new Color(0, 0, 0, 0.5f), true);
            Child("Face", null, new Vector2(CardView.Width, CardView.Height), Vector2.zero, Color.white);
            Child("Back", null, new Vector2(CardView.Width, CardView.Height), Vector2.zero, Color.white);
            Child("Dim", null, new Vector2(CardView.Width, CardView.Height), Vector2.zero, new Color(0, 0, 0, 0.5f));
            Child("Glow", UiKit.Res("card_glow_9s50"), new Vector2(CardView.Width + 30f, CardView.Height + 30f), Vector2.zero, Color.white, true);
            System.IO.Directory.CreateDirectory("Assets/_Project/Resources/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Resources/Prefabs/CardView.prefab");
            Object.DestroyImmediate(root);
        }
    }

    public static class Credits
    {
        public const string Text =
            "<b>Cards</b>\nVector Playing Cards 3.2 by Chris Aguilar, licensed under LGPL 3.0.\nhttps://totalnonsense.com/open-source-vector-playing-cards/\n\n" +
            "<b>Fonts</b>\nCinzel and Noto Serif, SIL Open Font License 1.1 (Google Fonts).\n\n" +
            "<b>Interface design</b>\nTarneeb VIP gameplay UI, designed in Figma.\n\n" +
            "<b>Art, sound and music</b>\nEmoji, icons, effects and all audio are original and generated for this project (CC0).\n\n" +
            "<b>Multiplayer</b>\nPhoton PUN 2 (Exit Games).\n\nMade with Unity.";
    }
}
#endif
