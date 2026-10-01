using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Tarneeb.Config;
using Tarneeb.Game;
using Tarneeb.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tarneeb.Tests.PlayMode
{
    /// <summary>
    /// Renders the real scenes at the CSS viewport of every common phone and tablet (full screen, and with the browser's bars showing) and checks
    /// the layout numerically: the frame fills the width, every element is on screen, nothing important overlaps. Screenshots go to Logs/screens/resp.
    /// </summary>
    public class ResponsiveLayoutTests
    {
        private static readonly (string name, int w, int h)[] Devices =
        {
            ("iPhone_SE_375x667", 375, 667), ("iPhone_8_Plus_414x736", 414, 736), ("iPhone_13_mini_375x812", 375, 812),
            ("iPhone_11_414x896", 414, 896), ("iPhone_13_390x844", 390, 844), ("iPhone_13_ProMax_428x926", 428, 926),
            ("iPhone_15_393x852", 393, 852), ("iPhone_15_ProMax_430x932", 430, 932), ("iPhone_16_Pro_402x874", 402, 874),
            ("iPhone_16_ProMax_440x956", 440, 956),
            ("Android_small_360x640", 360, 640), ("Galaxy_S8_360x740", 360, 740), ("Galaxy_S23_360x780", 360, 780), ("Galaxy_A_360x800", 360, 800),
            ("Pixel_5_393x851", 393, 851), ("Pixel_7_412x915", 412, 915), ("Pixel_8_Pro_448x998", 448, 998), ("Xperia_21x9_411x960", 411, 960),
            ("Fold_cover_344x882", 344, 882), ("Fold_inner_673x841", 673, 841),
            ("Kindle_Fire_600x1024", 600, 1024), ("iPad_mini_744x1133", 744, 1133), ("iPad_10_810x1080", 810, 1080), ("iPad_Air_820x1180", 820, 1180),
            ("iPad_Pro11_834x1194", 834, 1194), ("Galaxy_Tab_800x1280", 800, 1280), ("iPad_Pro13_1024x1366", 1024, 1366),
        };

        private Camera _cam;
        private RenderTexture _rt;
        private Texture2D _tex;
        private Canvas _canvas;
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameController.OnlineRequested = false;
            GameSettings.SoundOn = false;
            GameSettings.FixedSeed = 4242;
            PlayerPrefs.SetFloat("tarneeb.speed", 4f);
            _dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "screens", "resp");
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown() { GameSettings.FixedSeed = -1; PlayerPrefs.DeleteKey("tarneeb.speed"); }

        /// <summary>Every device twice: the full screen, and the visible page with the browser's bars showing (about 17% shorter).</summary>
        private static IEnumerable<(string name, int w, int h)> Viewports()
        {
            foreach (var d in Devices)
            {
                yield return (d.name + "_full", d.w, d.h);
                yield return (d.name + "_bars", d.w, Mathf.RoundToInt(d.h * 0.83f));
            }
        }

        private IEnumerator Resize(int w, int h)
        {
            if (_rt != null) { _cam.targetTexture = null; _rt.Release(); Object.Destroy(_rt); Object.Destroy(_tex); }
            _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            _tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            _cam.targetTexture = _rt;
            yield return null; yield return null; yield return null;
        }

        private void UseCameraCanvas(string canvasName)
        {
            _canvas = GameObject.Find(canvasName).GetComponent<Canvas>();
            _cam = Camera.main;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f;
            _cam.orthographic = true; _cam.clearFlags = CameraClearFlags.SolidColor;
            _canvas.renderMode = RenderMode.ScreenSpaceCamera; _canvas.worldCamera = _cam; _canvas.planeDistance = 10f;
        }

        private void Shot(string name)
        {
            _cam.Render();
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0); _tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), _tex.EncodeToPNG());
        }

        // ------------------------------------------------------------ geometry helpers

        private Rect InCanvas(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 c in corners)
            {
                Vector3 l = _canvas.transform.InverseTransformPoint(c);
                min = Vector2.Min(min, l); max = Vector2.Max(max, l);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Union(IEnumerable<Rect> rects)
        {
            Rect u = default; bool first = true;
            foreach (Rect r in rects) { u = first ? r : Rect.MinMaxRect(Mathf.Min(u.xMin, r.xMin), Mathf.Min(u.yMin, r.yMin), Mathf.Max(u.xMax, r.xMax), Mathf.Max(u.yMax, r.yMax)); first = false; }
            return u;
        }

        private static Rect Shrink(Rect r, float k) => new Rect(r.x + r.width * k, r.y + r.height * k, r.width * (1f - 2f * k), r.height * (1f - 2f * k));

        private static float OverlapArea(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        private static Transform Path_(Transform root, string path) => root.Find(path);

        private void CheckOnScreen(string vp, string what, Rect r, Rect screen, float tol = 1.5f)
        {
            bool inside = r.xMin >= screen.xMin - tol && r.xMax <= screen.xMax + tol && r.yMin >= screen.yMin - tol && r.yMax <= screen.yMax + tol;
            Assert.IsTrue(inside, vp + ": " + what + " is off screen (" + r + " vs " + screen + ")");
        }

        private void CheckApart(string vp, string a, Rect ra, string b, Rect rb, float allowedFraction = 0.02f)
        {
            float o = OverlapArea(ra, rb);
            float smaller = Mathf.Min(ra.width * ra.height, rb.width * rb.height);
            Assert.LessOrEqual(o, smaller * allowedFraction, vp + ": " + a + " overlaps " + b + " (" + ra + " / " + rb + ")");
        }

        private void CheckCoverage(string vp, ResponsiveFrame frame, Rect screen)
        {
            float need = ResponsiveFrame.DesignW / ResponsiveFrame.MinHeight;          // widest aspect that still fills the width
            float aspect = screen.width / screen.height;
            float shown = frame.Scale * ResponsiveFrame.DesignW / screen.width;        // fraction of the width the frame covers
            if (aspect <= need + 0.001f) Assert.GreaterOrEqual(shown, 0.995f, vp + ": the game should fill the screen width (covers " + shown + ")");
            else Assert.GreaterOrEqual(shown, 0.5f, vp + ": the game covers only " + shown + " of the width");
        }

        // ------------------------------------------------------------ tests

        [UnityTest]
        [Timeout(900000)]
        public IEnumerator Game_FitsEveryDevice_BiddingAndPlaying()
        {
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null;
            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            var trump = Object.FindFirstObjectByType<TrumpPicker>(FindObjectsInactive.Include);
            var hand = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            float t = 0;
            while (!bid.gameObject.activeSelf && t < 90f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(bid.gameObject.activeSelf, "never got to bid");
            yield return new WaitForSeconds(1.2f);
            UseCameraCanvas("GameCanvas");
            Transform content = _canvas.transform.Find("Content");
            var frame = content.GetComponent<ResponsiveFrame>();
            Assert.IsNotNull(frame, "Content has no ResponsiveFrame");

            // ---- phase 1: the bid panel is up
            foreach (var vp in Viewports())
            {
                yield return Resize(vp.w, vp.h);
                CheckGame(vp.name + "_bid", frame, content, hand, bid.GetComponent<RectTransform>(), true);
                Shot("game_bid_" + vp.name);
            }

            // ---- phase 2: pass, pick trump if asked, then it is my turn to play
            bid.transform.Find("Pass").GetComponent<Button>().onClick.Invoke();
            t = 0;
            while (!hand.AwaitingInput && t < 90f)
            {
                t += Time.deltaTime;
                if (bid.gameObject.activeSelf) bid.transform.Find("Pass").GetComponent<Button>().onClick.Invoke();
                else if (trump.gameObject.activeSelf) trump.transform.Find("Pick_Spades").GetComponent<Button>().onClick.Invoke();
                yield return null;
            }
            Assert.IsTrue(hand.AwaitingInput, "never got to play");
            yield return new WaitForSeconds(0.8f);
            foreach (var vp in Viewports())
            {
                yield return Resize(vp.w, vp.h);
                CheckGame(vp.name + "_play", frame, content, hand, null, false);
                Shot("game_play_" + vp.name);
            }
        }

        private void CheckGame(string vp, ResponsiveFrame frame, Transform content, HandView hand, RectTransform panel, bool bidding)
        {
            Rect screen = ((RectTransform)_canvas.transform).rect;
            CheckCoverage(vp, frame, screen);

            var named = new Dictionary<string, Rect>();
            void Add(string key, string path) { Transform t = Path_(content, path); Assert.IsNotNull(t, "missing " + path); named[key] = InCanvas((RectTransform)t); }
            Add("Seat_Top", "Seats/Seat_Top"); Add("Seat_Left", "Seats/Seat_Left"); Add("Seat_Right", "Seats/Seat_Right"); Add("Seat_Bottom", "Seats/Seat_Bottom");
            Add("MenuButton", "HUD/Header/MenuButton"); Add("SettingsButton", "HUD/Header/SettingsButton"); Add("Title", "HUD/Header/Title");
            Add("Scoreboard", "HUD/Header/Scoreboard"); Add("BidCapsule", "HUD/Header/BidCapsule");
            Add("ChatButton", "ChatUI/ChatButton"); Add("EmojiButton", "ChatUI/EmojiButton"); Add("ContractChip", "HUD/Footer/ContractChip");
            Add("DropZone", "Table/DropZone");
            var cards = new List<Rect>();
            foreach (var c in hand.Cards) cards.Add(InCanvas(c.Rect));
            Assert.Greater(cards.Count, 0, vp + ": no cards in hand");
            Rect handBounds = Union(cards);

            foreach (var kv in named) CheckOnScreen(vp, kv.Key, kv.Value, screen);
            foreach (Rect c in cards) CheckOnScreen(vp, "hand card", c, screen);

            // the hand sits above the bottom seat and chat buttons, the seats stay clear of one another and of the header
            var apart = new[] { "Seat_Top", "Seat_Left", "Seat_Right", "Seat_Bottom", "Scoreboard", "BidCapsule", "MenuButton", "SettingsButton", "ChatButton", "EmojiButton", "ContractChip" };
            for (int i = 0; i < apart.Length; i++)
                for (int j = i + 1; j < apart.Length; j++)
                    CheckApart(vp, apart[i], Shrink(named[apart[i]], 0.06f), apart[j], Shrink(named[apart[j]], 0.06f));
            foreach (string s in new[] { "Seat_Top", "Seat_Left", "Seat_Right", "Seat_Bottom", "ChatButton", "EmojiButton", "ContractChip" })
                CheckApart(vp, "hand", Shrink(handBounds, 0.02f), s, Shrink(named[s], 0.06f));

            // cards played to the table: the four slots stay inside the table and clear of the top seat's nameplate
            Rect table = default;
            foreach (Transform c in content.Find("Table")) if (c.name.StartsWith("Gold bezel")) table = InCanvas((RectTransform)c);
            Assert.Greater(table.width, 0f, vp + ": table not found");
            float cardScale = 0.92f * ResponsiveFrame.TrickScale * frame.Scale;
            foreach (string slot in new[] { "Bottom", "Right", "Top", "Left" })
            {
                Vector2 c = InCanvas((RectTransform)content.Find("Table/TrickArea/Slot_" + slot)).center;
                Rect card = new Rect(c.x - 83f * cardScale, c.y - 116f * cardScale, 166f * cardScale, 232f * cardScale);
                CheckOnScreen(vp, "trick card " + slot, card, Rect.MinMaxRect(table.xMin - 4f, table.yMin + 8f, table.xMax + 4f, table.yMax - 4f), 6f * frame.Scale);
                CheckApart(vp, "trick card " + slot, card, "Seat_Top", Shrink(named["Seat_Top"], 0.06f), 0.04f);
            }

            if (bidding && panel != null)
            {
                Rect p = InCanvas(panel);
                CheckOnScreen(vp, "bid panel", p, screen);
                Assert.LessOrEqual(p.yMin >= handBounds.yMax - 1f ? 0f : OverlapArea(p, handBounds), 1f, vp + ": the bid panel covers the cards (" + p + " vs " + handBounds + ")");
                foreach (string s in new[] { "Seat_Left", "Seat_Right" })
                    Assert.LessOrEqual(OverlapArea(p, named[s]), named[s].width * named[s].height * 0.12f, vp + ": the bid panel covers " + s);
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Menu_FitsEveryDevice()
        {
            Tarneeb.Localization.Loc.SetLanguage(Tarneeb.Localization.Language.English);                               // no first-launch language prompt in the way
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return null;
            UseCameraCanvas("MenuCanvas");
            yield return new WaitForSeconds(3.5f);                                      // the intro animation
            Transform content = _canvas.transform.Find("Content");
            var frame = content.GetComponent<ResponsiveFrame>();
            Assert.IsNotNull(frame, "Content has no ResponsiveFrame");
            foreach (var vp in Viewports())
            {
                yield return Resize(vp.w, vp.h);
                Rect screen = ((RectTransform)_canvas.transform).rect;
                CheckCoverage(vp.name, frame, screen);
                var named = new Dictionary<string, Rect>();
                foreach (string n in new[] { "Crown", "Title", "Tagline", "Cards", "PlayButton", "OnlineButton", "SoundButton", "MusicButton", "SettingsButton" })
                {
                    Transform t = content.Find("Menu/" + n);
                    Assert.IsNotNull(t, "missing " + n);
                    named[n] = InCanvas((RectTransform)t);
                    CheckOnScreen(vp.name, n, named[n], screen);
                }
                string[] names = new string[named.Count]; named.Keys.CopyTo(names, 0);
                for (int i = 0; i < names.Length; i++)
                    for (int j = i + 1; j < names.Length; j++)
                    {
                        // text boxes are taller than their glyphs: only compare shrunken boxes
                        float k = (names[i] == "Title" || names[j] == "Title" || names[i] == "Tagline" || names[j] == "Tagline") ? 0.2f : 0.05f;
                        CheckApart(vp.name, names[i], Shrink(named[names[i]], k), names[j], Shrink(named[names[j]], k));
                    }
                Shot("menu_" + vp.name);
            }
        }
    }
}
