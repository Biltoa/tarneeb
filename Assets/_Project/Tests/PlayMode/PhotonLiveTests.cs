using System.Collections;
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
    /// <summary>
    /// Talks to the real Photon cloud (needs internet and the App ID in the git-ignored PhotonServerSettings asset):
    /// creates a private room, starts the match as master, chats through PhotonTransport and leaves.
    /// </summary>
    [Category("Network")]
    public class PhotonLiveTests
    {
        [SetUp]
        public void SetUp()
        {
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameController.OnlineRequested = false;
            GameSettings.SoundOn = false;
            PlayerPrefs.SetFloat("tarneeb.speed", 2f);
            LobbyProvider.Current = null;
        }

        [TearDown]
        public void TearDown()
        {
            GameController.OnlineRequested = false;
            GameSettings.TurnTimerSeconds = 0;
            PlayerPrefs.DeleteKey("tarneeb.speed");
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator Photon_PrivateRoom_HostMatch_Chat()
        {
            Assume.That(LobbyProvider.Available, "PUN 2 is not installed in this build");
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return null;
            GameObject.Find("OnlineButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Transform p = Object.FindFirstObjectByType<LobbyScreen>(FindObjectsInactive.Include).transform.Find("Panel");
            p.Find("MenuView/Create").GetComponent<Button>().onClick.Invoke();

            float t = 0;
            while (!p.Find("RoomView").gameObject.activeSelf && t < 40f) { t += Time.deltaTime; yield return null; }
            string status = p.Find("Status").GetComponent<TMP_Text>().text;
            Assert.IsTrue(p.Find("RoomView").gameObject.activeSelf, "never joined a Photon room; status: " + status);
            string code = p.Find("RoomView/RoomCode").GetComponent<TMP_Text>().text;
            StringAssert.StartsWith("Room code: ", code);
            Assert.AreEqual(5, code.Substring("Room code: ".Length).Length);
            StringAssert.Contains("1 / 4", p.Find("RoomView/Players").GetComponent<TMP_Text>().text);
            Debug.Log("[PhotonLive] joined " + code);

            p.Find("RoomView/Start").GetComponent<Button>().onClick.Invoke();
            t = 0;
            while (SceneManager.GetActiveScene().name != "Game" && t < 20f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual("Game", SceneManager.GetActiveScene().name, "room property never reached this client");
            yield return new WaitForSeconds(1f);
            Assert.IsNotNull(OnlineSession.Transport, "PhotonTransport missing");

            int seat = -1; string text = null;
            void On(int s, ChatMessage m) { seat = s; if (!m.IsEmoji) text = m.Text; }
            ChatService.Received += On;
            ChatService.Send(ChatMessage.Say("live test"));
            t = 0; while (text == null && t < 10f) { t += Time.deltaTime; yield return null; }
            ChatService.Received -= On;
            Assert.AreEqual("live test", text, "chat did not round-trip through Photon");
            Assert.AreEqual(0, seat);

            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            var hand = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            t = 0; while (!bid.gameObject.activeSelf && !hand.AwaitingInput && t < 30f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(bid.gameObject.activeSelf || hand.AwaitingInput, "host game never asked the local player to act");

            GameObject.Find("MenuButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(0.5f);
            Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include).transform.Find("Panel/Leave").GetComponent<Button>().onClick.Invoke();
            t = 0; while (SceneManager.GetActiveScene().name != "Menu" && t < 10f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(LobbyProvider.Current.InRoom);
        }
    }
}
