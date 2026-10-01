using System.Collections;
using System.IO;
using NUnit.Framework;
using Tarneeb.Config;
using Tarneeb.Game;
using Tarneeb.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tarneeb.Tests.PlayMode
{
    /// <summary>Press-and-drag of a hand card: released after 25% of the hand-to-centre distance it is played, released short of that it returns to the hand.</summary>
    public class DragCardTests
    {
        [SetUp]
        public void SetUp()
        {
            GameController.LocalIsBot = false;
            GameController.SkipPresentation = false;
            GameController.OnlineRequested = false;
            GameSettings.SoundOn = false;
            GameSettings.FixedSeed = 4242;
            PlayerPrefs.SetFloat("tarneeb.speed", 4f);
        }

        [TearDown]
        public void TearDown() { GameSettings.FixedSeed = -1; PlayerPrefs.DeleteKey("tarneeb.speed"); }

        private Camera _cam;
        private RenderTexture _rt;
        private Texture2D _tex;

        private Vector2 Screen(Transform t) => RectTransformUtility.WorldToScreenPoint(_cam, t.position);

        /// <summary>Same off-screen camera setup as UiShowcaseTests: the batch screen is tiny, so render the canvas into a big texture.</summary>
        private void UseCameraCanvas()
        {
            var canvas = GameObject.Find("GameCanvas").GetComponent<Canvas>();
            _cam = Camera.main;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f;
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
            _tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0); _tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, "drag_" + name + ".png"), _tex.EncodeToPNG());
        }

        private IEnumerator Drag(CardView cv, Vector2 from, Vector2 to, string shot = null)
        {
            var es = EventSystem.current;
            var e = new PointerEventData(es) { position = from, pressPosition = from, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(cv.gameObject, e, ExecuteEvents.beginDragHandler);
            e.position = Vector2.Lerp(from, to, 0.5f);
            ExecuteEvents.Execute(cv.gameObject, e, ExecuteEvents.dragHandler);
            e.position = to;
            ExecuteEvents.Execute(cv.gameObject, e, ExecuteEvents.dragHandler);
            if (shot != null) { yield return new WaitForSeconds(0.5f); Shot(shot); }
            ExecuteEvents.Execute(cv.gameObject, e, ExecuteEvents.endDragHandler);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Drag_Out_Returns_Or_Plays()
        {
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null;
            var bid = Object.FindFirstObjectByType<BidPanel>(FindObjectsInactive.Include);
            var trump = Object.FindFirstObjectByType<TrumpPicker>(FindObjectsInactive.Include);
            var hand = Object.FindFirstObjectByType<HandView>(FindObjectsInactive.Include);
            Assert.IsNotNull(hand.DropZone, "HandView has no drop zone");
            float t = 0;
            while (!hand.AwaitingInput && t < 90f)
            {
                t += Time.deltaTime;
                if (bid.gameObject.activeSelf) bid.transform.Find("Pass").GetComponent<Button>().onClick.Invoke();
                else if (trump.gameObject.activeSelf) trump.transform.Find("Pick_Spades").GetComponent<Button>().onClick.Invoke();
                yield return null;
            }
            Assert.IsTrue(hand.AwaitingInput, "never got to play a card");
            yield return new WaitForSeconds(0.8f);
            UseCameraCanvas();
            yield return null; yield return null;

            CardView card = null;
            foreach (var cv in hand.Cards) if (!cv.IsDimmed) { card = cv; break; }
            Assert.IsNotNull(card);
            int before = hand.Count;

            // one click: picked (lifted, gold border, shown in front)
            card.OnPointerClick(null);
            yield return new WaitForSeconds(0.4f);
            Shot("1_selected");

            Vector2 start = Screen(card.transform);
            Vector2 centre = Screen(hand.DropZone);
            Vector2 handPos = Screen(hand.transform);

            // released at 15% of the way: back into the hand, still my turn
            yield return Drag(card, start, Vector2.Lerp(handPos, centre, 0.15f), "2_dragging_short");
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(before, hand.Count, "a card released under 25% must stay in the hand");
            Assert.IsTrue(hand.AwaitingInput, "still my turn");
            Shot("3_returned");

            // dragged to the centre, then back to 10% while still held: returns without a release
            var es = EventSystem.current;
            var e = new PointerEventData(es) { position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.beginDragHandler);
            e.position = centre; ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.dragHandler);
            e.position = Vector2.Lerp(handPos, centre, 0.10f); ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.dragHandler);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(before, hand.Count, "card must go back to the hand when dragged back under 25% while held");
            e.position = centre; ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.dragHandler);   // further drags are ignored
            ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.endDragHandler);                      // and the late release does not play it
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(before, hand.Count, "a card already returned must not be played by the late release");
            Assert.IsTrue(hand.AwaitingInput, "still my turn");

            // released just over 25%: played
            yield return Drag(card, Screen(card.transform), Vector2.Lerp(handPos, centre, 0.30f));
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(before - 1, hand.Count, "a card released over 25% must be played");
            Shot("5_played");
        }
    }
}
