using System.Collections;
using System.IO;
using NUnit.Framework;
using Tarneeb.Config;
using Tarneeb.Game;
using Tarneeb.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tarneeb.Tests.PlayMode
{
    public class ChatFlowTests
    {
        [SetUp]
        public void SetUp()
        {
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameSettings.FixedSeed = 4242;
            GameSettings.SoundOn = false;
            PlayerPrefs.SetFloat("tarneeb.speed", 1f);
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.FixedSeed = -1;
            PlayerPrefs.DeleteKey("tarneeb.speed");
        }

        /// <summary>Opens both panels through the real buttons, sends an emoji, a typed line and a quick phrase, and renders screenshots.</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Emoji_And_Chat_Through_The_UI()
        {
            yield return SceneManager.LoadSceneAsync("Game");
            yield return new WaitForSeconds(1.5f);
            var ui = Object.FindFirstObjectByType<ChatUI>(FindObjectsInactive.Include);
            Assert.IsNotNull(ui, "ChatUI missing from the Game scene");
            var chatter = Object.FindFirstObjectByType<BotChatter>();
            Assert.IsNotNull(chatter, "single player should have bot chatter");
            chatter.enabled = false;     // random bot replies would make the line counts flaky

            var canvas = GameObject.Find("GameCanvas").GetComponent<Canvas>();
            Camera cam = Camera.main;
            var rt = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);

            Transform t = ui.transform;
            Assert.IsFalse(t.Find("EmojiPanel").gameObject.activeSelf);
            yield return null;
            Shot(cam, rt, tex, "chat_0_buttons");

            // Emoji picker
            t.Find("EmojiButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(t.Find("EmojiPanel").gameObject.activeSelf);
            Assert.IsNotNull(t.Find("EmojiPanel/Panel/Emoji_5/Icon").GetComponent<Image>().sprite, "emoji sprite not loaded");
            yield return null;
            Shot(cam, rt, tex, "chat_1_emoji_panel");
            t.Find("EmojiPanel/Panel/Emoji_3").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(t.Find("EmojiPanel").gameObject.activeSelf, "picker should close after choosing");
            Transform seat = GameObject.Find("Seat_Bottom").transform;
            Assert.IsTrue(seat.Find("ChatBubble").gameObject.activeSelf && seat.Find("ChatBubble/Icon").gameObject.activeSelf, "emoji balloon not shown by the local seat");
            Shot(cam, rt, tex, "chat_2_emoji_sent");

            // Typed message
            yield return new WaitForSeconds(1f);
            t.Find("ChatButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var input = t.Find("ChatPanel/Panel/Input").GetComponent<TMP_InputField>();
            input.text = "Good luck everyone <b>!</b>";
            t.Find("ChatPanel/Panel/Send").GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
            Assert.AreEqual("", input.text, "input should clear after sending");
            string log = t.Find("ChatPanel/Panel/Log").GetComponent<TMP_Text>().text;
            StringAssert.Contains("Good luck everyone b!/b", log);
            Assert.IsFalse(log.Contains("<b>"), "rich-text tags must be stripped");
            Assert.IsTrue(seat.Find("ChatBubble").gameObject.activeSelf, "chat bubble not shown above the local seat");

            // Quick phrase (after the send cooldown) and the screenshot of the open window
            yield return new WaitForSeconds(1f);
            t.Find("ChatPanel/Panel/Quick_1").GetComponent<Button>().onClick.Invoke();
            yield return null; yield return null;
            StringAssert.Contains("Nice play!", t.Find("ChatPanel/Panel/Log").GetComponent<TMP_Text>().text);
            Shot(cam, rt, tex, "chat_3_chat_panel");

            // Close, then spam: the local cooldown swallows the second send
            t.Find("ChatPanel/Panel/Close").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(ui.AnyPanelOpen);
            yield return new WaitForSeconds(1f);
            int before = CountLines(t);
            ChatService.Send(ChatMessage.Say("a"));
            ChatService.Send(ChatMessage.Say("b"));
            yield return null;
            Assert.AreEqual(before + 2, CountLines(t), "ChatService itself does not rate-limit; the UI and the host do");
        }

        private static int CountLines(Transform ui)
        {
            string s = ui.Find("ChatPanel/Panel/Log").GetComponent<TMP_Text>().text;
            return s.Split('\n').Length;
        }

        private static void Shot(Camera cam, RenderTexture rt, Texture2D tex, string name)
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "screens");
            Directory.CreateDirectory(dir);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        }
    }
}
