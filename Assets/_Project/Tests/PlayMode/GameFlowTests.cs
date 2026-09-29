using System.Collections;
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
    public class GameFlowTests
    {
        private int _finishedWinner = -2;
        private bool _finished;

        [SetUp]
        public void SetUp()
        {
            _finished = false;
            _finishedWinner = -2;
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameSettings.FixedSeed = -1;
            GameController.GameFinished += OnFinished;
        }

        [TearDown]
        public void TearDown()
        {
            GameController.GameFinished -= OnFinished;
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameSettings.FixedSeed = -1;
            PlayerPrefs.DeleteKey("tarneeb.speed");
        }

        private void OnFinished(int winner, int seed) { _finished = true; _finishedWinner = winner; }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null;
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator FullGame_AllBots_NoPresentation_Finishes()
        {
            GameController.LocalIsBot = true;
            GameController.SkipPresentation = true;
            yield return LoadGame();
            float t = 0;
            while (!_finished && t < 60f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(_finished, "game did not finish");
            Assert.That(_finishedWinner, Is.InRange(0, 1));
        }

        /// <summary>Plays a full game through the real UI widgets (buttons and card clicks), with animations at 4x speed.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FullGame_HumanThroughUI_Finishes()
        {
            PlayerPrefs.SetFloat("tarneeb.speed", 4f);
            GameSettings.FixedSeed = 12345;
            yield return LoadGame();
            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            var trump = Object.FindFirstObjectByType<TrumpPicker>(FindObjectsInactive.Include);
            var hand = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            var summary = Object.FindFirstObjectByType<RoundSummaryView>(FindObjectsInactive.Include);
            Assert.IsNotNull(bid); Assert.IsNotNull(trump); Assert.IsNotNull(hand); Assert.IsNotNull(summary);

            float t = 0;
            int plays = 0;
            while (!_finished && t < 240f)
            {
                t += Time.deltaTime;
                if (bid.gameObject.activeSelf)
                {
                    // Bid 7 if enabled and the hand is decent, otherwise pass.
                    Button b7 = bid.transform.Find("Bid7").GetComponent<Button>();
                    Button pass = bid.transform.Find("Pass").GetComponent<Button>();
                    (b7.interactable && Random.value < 0.4f ? b7 : pass).onClick.Invoke();
                }
                else if (trump.gameObject.activeSelf)
                {
                    trump.transform.Find("Pick_Spades").GetComponent<Button>().onClick.Invoke();
                }
                else if (hand.AwaitingInput)
                {
                    foreach (var cv in hand.Cards)
                    {
                        if (cv.IsDimmed) continue;
                        cv.OnPointerClick(null);   // select (lift)
                        yield return null;
                        cv.OnPointerClick(null);   // play
                        plays++;
                        break;
                    }
                }
                else if (summary.gameObject.activeSelf && summary.transform.Find("Panel/Continue").gameObject.activeInHierarchy)
                {
                    var cg = summary.GetComponent<CanvasGroup>();
                    if (cg.alpha > 0.9f) summary.transform.Find("Panel/Continue").GetComponent<Button>().onClick.Invoke();
                }
                yield return null;
            }
            Assert.IsTrue(_finished, "UI-driven game did not finish (plays=" + plays + ")");
            Assert.Greater(plays, 20);
        }

        /// <summary>Renders the table to PNGs so layout can be reviewed without a screen.</summary>
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Screenshots_BotVsBots_RendersTable()
        {
            GameController.LocalIsBot = true;
            GameSettings.FixedSeed = 777;
            PlayerPrefs.SetFloat("tarneeb.speed", 3f);
            yield return LoadGame();

            var canvas = SetupCameraCanvas("GameCanvas", out RenderTexture rt, out Camera cam);
            string dir = ShotDir();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            var summary = Object.FindFirstObjectByType<RoundSummaryView>(FindObjectsInactive.Include);
            var over = Object.FindFirstObjectByType<GameOverView>(FindObjectsInactive.Include);
            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            float[] times = { 0.4f, 1.0f, 2.0f, 3.5f, 5f, 8f, 11f };
            int shot = 0; bool gotSummary = false, gotBid = false;
            float start = Time.time;
            while (Time.time - start < 80f && !(_finished))
            {
                bool due = shot < times.Length && Time.time - start >= times[shot];
                bool summaryNow = summary.gameObject.activeSelf && summary.GetComponent<CanvasGroup>().alpha > 0.95f && !gotSummary;
                bool overNow = over.gameObject.activeSelf && over.GetComponent<CanvasGroup>().alpha > 0.95f;
                if (due || summaryNow || overNow)
                {
                    Save(cam, rt, tex, dir, due ? "t" + shot : (summaryNow ? "round_summary" : "game_over"));
                    if (due) shot++;
                    if (summaryNow) gotSummary = true;
                    if (overNow) break;
                }
                yield return null;
            }
            Assert.IsTrue(gotSummary, "no round summary shown");
        }

        [UnityTest]
        public IEnumerator Screenshots_Menu()
        {
            PlayerPrefs.DeleteKey("tarneeb.speed");
            yield return SceneManager.LoadSceneAsync("Menu");
            yield return null;
            SetupCameraCanvas("MenuCanvas", out RenderTexture rt, out Camera cam);
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            yield return null;
            yield return null;
            Save(cam, rt, tex, ShotDir(), "menu");
        }

        private static Canvas SetupCameraCanvas(string canvasName, out RenderTexture rt, out Camera cam)
        {
            var canvas = GameObject.Find(canvasName).GetComponent<Canvas>();
            cam = Camera.main;
            rt = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            return canvas;
        }

        private static string ShotDir()
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "screens");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void Save(Camera cam, RenderTexture rt, Texture2D tex, string dir, string name)
        {
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        }
    }
}
