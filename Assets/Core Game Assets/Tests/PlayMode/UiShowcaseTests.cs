using System.Collections;
using System.IO;
using NUnit.Framework;
using Tarneeb.Config;
using Tarneeb.Core.Cards;
using Tarneeb.Core.Rules;
using Tarneeb.Game;
using Tarneeb.Localization;
using Tarneeb.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tarneeb.Tests.PlayMode
{
    /// <summary>Opens every panel of both scenes and renders it to Logs/screens/ui_*.png so the UI can be reviewed without a screen.</summary>
    public class UiShowcaseTests
    {
        private Camera _cam;
        private RenderTexture _rt;
        private Texture2D _tex;

        [SetUp]
        public void SetUp()
        {
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameController.OnlineRequested = false;
            GameSettings.SoundOn = false;
            GameSettings.FixedSeed = 31337;
            PlayerPrefs.SetFloat("tarneeb.speed", 2f);
        }

        [TearDown]
        public void TearDown() { GameSettings.FixedSeed = -1; PlayerPrefs.DeleteKey("tarneeb.speed"); Loc.ResetChoice(); }

        private void Setup(string canvasName)
        {
            var canvas = GameObject.Find(canvasName).GetComponent<Canvas>();
            _cam = Camera.main;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f;      // the batch screen is 640x480; render at 1.5x design size instead
            _rt = new RenderTexture(1275, 2775, 24, RenderTextureFormat.ARGB32);
            _cam.targetTexture = _rt; _cam.orthographic = true; _cam.clearFlags = CameraClearFlags.SolidColor;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _cam; canvas.planeDistance = 10f;
            _tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGB24, false);
        }

        private void Shot(string name)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "screens");
            Directory.CreateDirectory(dir);
            _cam.Render();
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            _tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, "ui_" + name + ".png"), _tex.EncodeToPNG());
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Menu_Panels()
        {
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return new WaitForSeconds(2.2f);                     // intro animation
            Setup("MenuCanvas");
            yield return null; yield return null;                    // let FitScale pick up the new canvas size
            Shot("menu");
            var settings = SettingsPanel.Instance;
            Assert.IsNotNull(settings, "SettingsPanel missing in the menu");
            GameObject.Find("SettingsButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(settings.IsOpen);
            Shot("menu_settings");
            settings.transform.Find("Panel/Main/CreditsButton").GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
            Shot("menu_credits");
            settings.Close();
            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(settings.IsOpen);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Game_Panels()
        {
            yield return SceneManager.LoadSceneAsync("Game");
            yield return new WaitForSeconds(0.3f);
            Object.FindFirstObjectByType<BotChatter>().enabled = false;
            Setup("GameCanvas");
            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            float t = 0;
            while (!bid.gameObject.activeSelf && t < 40f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(bid.gameObject.activeSelf, "bid panel never appeared");
            Shot("bid");

            var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            GameObject.Find("MenuButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(pause.gameObject.activeSelf);
            Shot("pause");
            pause.Close();
            yield return new WaitForSeconds(0.3f);

            GameObject.Find("SettingsButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Shot("game_settings");
            SettingsPanel.Instance.Close();
            yield return new WaitForSeconds(0.3f);

            var picker = Object.FindFirstObjectByType<TrumpPicker>(FindObjectsInactive.Include);
            picker.Show(s => { });
            yield return new WaitForSeconds(0.5f);
            Shot("trump");
            picker.Hide();

            GameObject.Find("EmojiButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            Shot("emoji");
            GameObject.Find("ChatButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.4f);
            ChatService.Deliver(2, ChatMessage.Say("Nice bid, partner!"));
            ChatService.Deliver(1, ChatMessage.Say("Good luck everyone"));
            yield return new WaitForSeconds(0.5f);
            Shot("chat");
            Object.FindFirstObjectByType<ChatUI>(FindObjectsInactive.Include).CloseAll();
            ChatService.Deliver(3, ChatMessage.Emoji(4));
            ChatService.Deliver(2, ChatMessage.Say("Nice bid, partner!"));
            ChatService.Deliver(1, ChatMessage.Say(new string('e', 60)));
            ChatService.Deliver(0, ChatMessage.Say("Thanks! Good luck to everyone at this table"));
            yield return new WaitForSeconds(0.8f);
            Shot("chat_bubbles");

            bid.Hide();
            var sum = Object.FindFirstObjectByType<RoundSummaryView>(FindObjectsInactive.Include);
            var summary = new RoundSummary { Declarer = 0, Bid = 9, Trump = Suit.Spades, DeclaringTeam = 0, DeclaringTricks = 10, DefendingTricks = 3,
                ContractMade = true, DeclaringDelta = 9, DefendingDelta = 0, TeamScoresAfter = new[] { 21, 14 } };
            StartCoroutine2(sum.Show(summary, 0, "Ahmad", false, "Next round", 1.6f));
            yield return new WaitForSeconds(0.9f);
            Shot("summary");
            yield return new WaitForSeconds(1.5f);

            var over = Object.FindFirstObjectByType<GameOverView>(FindObjectsInactive.Include);
            StartCoroutine2(over.Show(true, 41, 27, c => { }, 1.4f));
            yield return new WaitForSeconds(1.2f);
            Shot("gameover_win");
            yield return new WaitForSeconds(1.5f);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Arabic_First_Launch_And_Panels()
        {
            Loc.ResetChoice();                                               // first launch: the language prompt shows, the game is English
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return new WaitForSeconds(2.6f);
            Setup("MenuCanvas");
            yield return null; yield return null;
            var prompt = Object.FindFirstObjectByType<LanguagePrompt>(FindObjectsInactive.Include);
            Assert.IsTrue(prompt.gameObject.activeSelf, "the language prompt must show on the first launch");
            Shot("lang_prompt");
            prompt.transform.Find("Panel/Arabic").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(Language.Arabic, Loc.Current);
            Assert.IsTrue(Loc.HasChosen, "the choice is stored");
            Assert.IsFalse(prompt.gameObject.activeSelf);
            Shot("ar_menu");
            GameObject.Find("SettingsButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.6f);
            Shot("ar_menu_settings");
            SettingsPanel.Instance.Close();
            yield return new WaitForSeconds(0.4f);

            // next launch (scene reload): no prompt any more
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(Object.FindFirstObjectByType<LanguagePrompt>(FindObjectsInactive.Include).gameObject.activeSelf, "the prompt shows only once");

            yield return SceneManager.LoadSceneAsync("Game");
            yield return new WaitForSeconds(0.3f);
            Object.FindFirstObjectByType<BotChatter>().enabled = false;
            Setup("GameCanvas");
            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            float t = 0;
            while (!bid.gameObject.activeSelf && t < 40f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.6f);
            ChatService.Deliver(2, ChatMessage.Say("Nice play!"));
            ChatService.Deliver(1, ChatMessage.Say("Good luck!"));
            ChatService.Deliver(3, ChatMessage.Say("Hello from Khalid"));
            yield return new WaitForSeconds(0.8f);
            Shot("ar_bid");
            GameObject.Find("SettingsButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Shot("ar_game_settings");
            SettingsPanel.Instance.Close();
            yield return new WaitForSeconds(0.3f);
            Loc.SetLanguage(Language.English);                               // switching back at run time re-translates everything
            yield return new WaitForSeconds(0.5f);
            Shot("en_after_switch");
        }

        private static void StartCoroutine2(IEnumerator e) => Object.FindFirstObjectByType<GameController>().StartCoroutine(e);
    }
}
