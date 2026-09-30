#if UNITY_EDITOR
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
    /// <summary>Builds the main menu from the same Figma pieces as the table (background, medallion, settings control) plus new layout.</summary>
    public static class MenuSceneAssembler
    {
        private const float W = 850f, H = 1850f;

        private static RectTransform FindNode(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return (RectTransform)t;
            Debug.LogError("[MenuAssembler] missing node " + name);
            return null;
        }

        public static string Assemble()
        {
            UiKit.LoadFonts();
            var canvas = Object.FindFirstObjectByType<Canvas>();
            canvas.name = "MenuCanvas";
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var content = (RectTransform)canvas.transform.GetChild(0);
            content.name = "Content";
            GameSceneAssembler.StripFigmaComponents(content);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(W, H);
            var back = UiKit.Fill("Background", canvas.transform); back.SetSiblingIndex(0);
            var bi = back.gameObject.AddComponent<Image>(); bi.color = new Color32(0, 24, 26, 255); bi.raycastTarget = true;
            content.gameObject.AddComponent<FitScale>().designSize = new Vector2(W, H);

            // keep: the background art, the settings control and the table medallion
            RectTransform bg = FindNode(content, "01 / Background"); bg.name = "Background";
            RectTransform settingsCtl = FindNode(content, "Control / Settings");
            RectTransform medallion = FindNode(content, "Central medallion / centered");

            var vig = UiKit.Fill("Vignette", content);
            var vi = vig.gameObject.AddComponent<Image>(); vi.sprite = UiKit.Res("vignette"); vi.color = new Color(1, 1, 1, 0.7f); vi.raycastTarget = false;
            var menu = UiKit.Rt("Menu", content, W / 2f, H / 2f, W, H);
            menu.gameObject.AddComponent<MenuController>();

            // soft glow and the medallion ornament behind the buttons
            var glow = UiKit.Img("Glow", menu, UiKit.Res("glow_gold"), 425f, 1000f, 1100f, 1100f, new Color(1f, 0.85f, 0.45f, 0.13f));
            medallion.SetParent(menu, true); medallion.name = "Medallion";
            medallion.anchorMin = medallion.anchorMax = new Vector2(0f, 1f); medallion.anchoredPosition = new Vector2(425f, -1150f);
            medallion.localScale = Vector3.one * 3.4f;
            foreach (Graphic g in medallion.GetComponentsInChildren<Graphic>(true)) { g.raycastTarget = false; g.color = new Color(1, 1, 1, 0.5f); }
            glow.transform.SetSiblingIndex(0);

            // crown, title, tagline
            var crown = UiKit.Img("Crown", menu, UiKit.Res("icon_crown"), 425f, 290f, 190f, 176f);
            var crownVip = UiKit.Text("Vip", crown.transform, "VIP", 95f, 118f, 150f, 60f, 44f, new Color32(72, 44, 4, 255)); crownVip.fontStyle = FontStyles.Bold;
            var title = UiKit.Text("Title", menu, "TARNEEB", 425f, 470f, 800f, 160f, 122f, UiKit.Gold);
            title.fontStyle = FontStyles.Bold; title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(new Color32(255, 248, 216, 255), new Color32(255, 248, 216, 255), new Color32(231, 188, 93, 255), new Color32(231, 188, 93, 255));
            UiKit.Group(title.gameObject);
            var tagline = UiKit.Text("Tagline", menu, "THE CLASSIC CARD GAME", 425f, 580f, 700f, 50f, 30f, UiKit.Cream);
            tagline.characterSpacing = 8f;
            UiKit.Group(tagline.gameObject);

            // floating card fan
            var cards = UiKit.Rt("Cards", menu, 425f, 820f, 560f, 260f);
            UiKit.Group(cards.gameObject);
            int[] faces = { 9, 23, 51, 37, 47 };             // J clubs, Q diamonds, A spades, K hearts, 10 spades
            for (int i = 0; i < 5; i++)
            {
                float t = i - 2f;
                var c = UiKit.Img("Card" + i, cards, null, 280f + t * 104f, 130f + Mathf.Abs(t) * Mathf.Abs(t) * 12f, 168f, 235f);
                c.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -t * 9f);
                c.gameObject.AddComponent<DecoCard>().cardIndex = faces[i];
                var sh = UiKit.Img("Shadow", c.transform, UiKit.Res("card_shadow_9s50"), 84f, 130f, 210f, 280f, new Color(0, 0, 0, 0.5f), false, true);
                sh.transform.SetAsFirstSibling();
            }

            // buttons
            var play = UiKit.Btn("PlayButton", menu, "button_gold_9s48", 425f, 1160f, 560f, 128f, "PLAY VS BOTS", 46f, UiKit.Ink);
            var online = UiKit.Btn("OnlineButton", menu, "button_emerald_9s48", 425f, 1320f, 560f, 108f, "PLAY ONLINE", 38f, UiKit.Cream);
            settingsCtl.SetParent(menu, true); settingsCtl.name = "SettingsButton";
            foreach (string n in new[] { "Settings icon" }) { var ic = FindNode(content, n); if (ic != null) Object.DestroyImmediate(ic.gameObject); }
            var face = settingsCtl.Find("Emerald button face").GetComponent<Graphic>();
            var sb = settingsCtl.gameObject.AddComponent<Button>(); face.raycastTarget = true; sb.targetGraphic = face; sb.transition = Selectable.Transition.None;
            settingsCtl.gameObject.AddComponent<ButtonFx>();
            var gear = UiKit.Rt("Icon", settingsCtl, 42f, 39f, 46f, 46f).gameObject.AddComponent<Image>(); gear.sprite = UiKit.Res("icon_gear"); gear.raycastTarget = false;
            settingsCtl.SetAsLastSibling();

            // quick sound / music switches
            UiKit.IconBtn("SoundButton", menu, 355f, 1520f, 100f, "icon_sound_on", 52f);
            UiKit.IconBtn("MusicButton", menu, 495f, 1520f, 100f, "icon_music_on", 52f);
            UiKit.Text("Footer", menu, "Portfolio prototype", 425f, 1705f, 500f, 30f, 20f, new Color(1f, 0.94f, 0.75f, 0.6f), true);
            UiKit.Text("Footer2", menu, "Cards: Vector Playing Cards by Chris Aguilar, LGPL 3.0", 425f, 1738f, 520f, 26f, 16f, new Color(1f, 0.94f, 0.75f, 0.45f), true);

            // the remaining design content is not part of the menu
            foreach (Transform t in content.Cast<Transform>().ToArray())
                if (t.name != "Background" && t.name != "Menu" && t.name != "Vignette") Object.DestroyImmediate(t.gameObject);

            var fx = UiKit.Rt("FxLayer", content, W / 2f, H / 2f, W, H); fx.gameObject.AddComponent<UiFx>();
            BuildOnlinePanel(content);
            GameSceneAssembler.BuildSettingsPanel(content, 425f, 925f);
            foreach (Graphic g in content.GetComponentsInChildren<Graphic>(true))
                if (g.GetComponentInParent<Selectable>(true) == null && g.name != "Dim" && !g.name.StartsWith("Panel")) g.raycastTarget = false;
            foreach (Button b in content.GetComponentsInChildren<Button>(true)) if (b.GetComponent<ButtonFx>() == null) b.gameObject.AddComponent<ButtonFx>();

            var fader = UiKit.Fill("Fader", canvas.transform);
            fader.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            fader.gameObject.AddComponent<ScreenFader>();
            fader.SetAsLastSibling();

            foreach (string n in new[] { "UiFont", "EventSystem" }) { var old = GameObject.Find(n); if (old != null) Object.DestroyImmediate(old); }
            var fonts = new GameObject("UiFont").AddComponent<UiFont>(); fonts.title = UiKit.Cinzel; fonts.number = UiKit.Noto;
            EditorSceneHelpers.EnsureAudio();
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var cam = Camera.main; if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0, 24, 26, 255); }
            return "menu assembled";
        }

        private static void BuildOnlinePanel(RectTransform content)
        {
            var op = UiKit.Fill("OnlinePanel", content);
            UiKit.Group(op.gameObject);
            var dim = UiKit.Fill("Dim", op); var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.03f, 0.04f, 0.8f);
            Image p = UiKit.Img("Panel", op, UiKit.Res("panel_dark_9s56"), 425f, 925f, 700f, 900f, Color.white, true, true);
            UiKit.Text("Title", p.transform, "PLAY ONLINE", 350f, 72f, 600f, 70f, 46f, UiKit.Gold).fontStyle = FontStyles.Bold;
            var mv = UiKit.Rt("MenuView", p.transform, 350f, 470f, 700f, 700f);
            UiKit.Btn("Quick", mv, "button_gold_9s48", 350f, 110f, 520f, 116f, "QUICK MATCH", 38f, UiKit.Ink);
            UiKit.Btn("Create", mv, "button_emerald_9s48", 350f, 260f, 520f, 100f, "CREATE PRIVATE ROOM", 28f, UiKit.Cream);
            UiKit.Input("CodeInput", mv, 240f, 410f, 320f, 92f, "ROOM CODE", 5, 36f);
            UiKit.Btn("Join", mv, "button_gold_9s48", 520f, 410f, 190f, 92f, "JOIN", 30f, UiKit.Ink);
            UiKit.Btn("Back", mv, "button_disabled_9s48", 350f, 590f, 300f, 80f, "BACK", 28f, UiKit.Cream);
            var rv = UiKit.Rt("RoomView", p.transform, 350f, 470f, 700f, 700f);
            UiKit.Text("RoomCode", rv, "", 350f, 110f, 620f, 90f, 60f, Color.white, true).fontStyle = FontStyles.Bold;
            UiKit.Text("Players", rv, "", 350f, 210f, 620f, 60f, 36f, UiKit.Gold);
            UiKit.Btn("Start", rv, "button_gold_9s48", 350f, 360f, 480f, 116f, "START GAME", 38f, UiKit.Ink);
            UiKit.Btn("Leave", rv, "button_red_9s48", 350f, 540f, 300f, 90f, "LEAVE", 30f, Color.white);
            UiKit.Text("Status", p.transform, "", 350f, 815f, 620f, 110f, 24f, UiKit.Cream, true, TextAlignmentOptions.Center, true);
            op.gameObject.AddComponent<LobbyScreen>();
        }
    }
}
#endif
