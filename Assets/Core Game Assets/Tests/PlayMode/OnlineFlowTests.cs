using System.Collections;
using System.IO;
using NUnit.Framework;
using Tarneeb.Config;
using Tarneeb.Game;
using Tarneeb.Net;
using Tarneeb.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tarneeb.Tests.PlayMode
{
    /// <summary>The whole online path except the network: Menu window, host match, chat relay, leaving. Uses LoopbackLobby.</summary>
    public class OnlineFlowTests
    {
        private System.Func<ILobby> _savedFactory;

        [SetUp]
        public void SetUp()
        {
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameController.OnlineRequested = false;
            GameSettings.SoundOn = false;
            GameSettings.FixedSeed = 99;
            PlayerPrefs.SetFloat("tarneeb.speed", 2f);
            _savedFactory = LobbyProvider.Factory;
            LobbyProvider.Current = null;
            LobbyProvider.Factory = () => new LoopbackLobby();
        }

        [TearDown]
        public void TearDown()
        {
            LobbyProvider.Factory = _savedFactory;
            LobbyProvider.Current = null;
            OnlineSession.Transport = null;
            GameController.OnlineRequested = false;
            GameSettings.FixedSeed = -1;
            GameSettings.TurnTimerSeconds = 0;
            PlayerPrefs.DeleteKey("tarneeb.speed");
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

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator Menu_Lobby_Host_Chat_Leave()
        {
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return null;

            var online = GameObject.Find("OnlineButton").GetComponent<Button>();
            Assert.IsTrue(online.interactable, "Play Online should be enabled when a lobby backend is registered");
            var panel = Object.FindFirstObjectByType<LobbyScreen>(FindObjectsInactive.Include);
            Assert.IsNotNull(panel, "OnlinePanel missing from the Menu scene");
            Assert.IsFalse(panel.gameObject.activeSelf);

            var canvas = GameObject.Find("MenuCanvas").GetComponent<Canvas>();
            Camera cam = Camera.main;
            var rt = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);

            online.onClick.Invoke();
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf);
            yield return null;
            Shot(cam, rt, tex, "lobby_0_menu");
            Transform p = panel.transform.Find("Panel");
            Assert.IsTrue(p.Find("MenuView").gameObject.activeSelf);
            Assert.IsFalse(p.Find("RoomView").gameObject.activeSelf);

            // A bad code shows an error instead of joining
            p.Find("MenuView/Join").GetComponent<Button>().onClick.Invoke();
            StringAssert.Contains("5-letter", p.Find("Status").GetComponent<TMP_Text>().text);

            p.Find("MenuView/Quick").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(p.Find("RoomView").gameObject.activeSelf, "should be in the waiting room");
            StringAssert.Contains("1 / 4", p.Find("RoomView/Players").GetComponent<TMP_Text>().text);
            Assert.IsTrue(p.Find("RoomView/Start").gameObject.activeSelf, "the master can start");
            yield return null;
            Shot(cam, rt, tex, "lobby_1_room");
            cam.targetTexture = null;

            p.Find("RoomView/Start").GetComponent<Button>().onClick.Invoke();
            float t = 0;
            while (SceneManager.GetActiveScene().name != "Game" && t < 10f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual("Game", SceneManager.GetActiveScene().name);
            yield return new WaitForSeconds(1.0f);

            Assert.IsTrue(GameController.OnlineRequested);
            Assert.IsNotNull(ChatService.Outgoing, "NetChat should own the chat channel online");
            Assert.IsNull(Object.FindFirstObjectByType<BotChatter>(), "no fake bot chatter in an online game");
            Assert.AreEqual("AHMAD", GameObject.Find("Seat_Bottom").transform.Find("NameLabel").GetComponent<TMP_Text>().text);
            Assert.AreEqual("Ahmad", OnlineSession.NamesFor(1)[1]);      // the local seat is always Ahmad at the bottom
            Assert.AreEqual("Omar", OnlineSession.NamesFor(1)[3]);        // two seats further round is the partner

            // Chat goes host -> relay -> back to this peer with seat 0
            int seat = -1; string text = null; int emoji = -1;
            void On(int s, ChatMessage m) { seat = s; if (m.IsEmoji) emoji = m.EmojiId; else text = m.Text; }
            ChatService.Received += On;
            ChatService.Send(ChatMessage.Say("hello <b>online</b>"));
            t = 0; while (text == null && t < 3f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual("hello bonline/b", text);
            Assert.AreEqual(0, seat);
            yield return new WaitForSeconds(1f);                // beyond the host's rate limit
            ChatService.Send(ChatMessage.Emoji(7));
            t = 0; while (emoji < 0 && t < 3f) { t += Time.deltaTime; yield return null; }
            ChatService.Received -= On;
            Assert.AreEqual(7, emoji);
            Assert.IsTrue(GameObject.Find("Seat_Bottom").transform.Find("ChatBubble/Icon").gameObject.activeInHierarchy);

            // The table is really a game: the human gets to bid
            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            var hand = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            t = 0; while (!bid.gameObject.activeSelf && !hand.AwaitingInput && t < 20f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(bid.gameObject.activeSelf || hand.AwaitingInput, "online game never asked the local player to act");

            // Leaving the table drops the room
            GameObject.Find("MenuButton").GetComponent<Button>().onClick.Invoke();          // hamburger opens the pause menu
            yield return new WaitForSeconds(0.5f);
            var pause = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            Assert.IsTrue(pause.gameObject.activeSelf, "pause menu should open");
            pause.transform.Find("Panel/Leave").GetComponent<Button>().onClick.Invoke();
            t = 0; while (SceneManager.GetActiveScene().name != "Menu" && t < 10f) { t += Time.deltaTime; yield return null; }
            yield return null;
            Assert.IsFalse(GameController.OnlineRequested, "OnlineRequested should reset when leaving the game");
            Assert.IsFalse(LobbyProvider.Current.InRoom, "the lobby should have left the room");
            Assert.IsNull(ChatService.Outgoing);
        }
    }
}
