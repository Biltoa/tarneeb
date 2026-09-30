using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Full-screen black overlay: fades in when a scene starts and fades out before the next one loads.</summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        private Image _image;
        private bool _loading;

        private void Awake()
        {
            Instance = this;
            _image = GetComponent<Image>();
            _image.color = Color.black;
            _image.raycastTarget = true;
        }

        private void Start() => StartCoroutine(Fade(1f, 0f, 0.5f));
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            _image.raycastTarget = true;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _image.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            _image.color = new Color(0f, 0f, 0f, to);
            _image.raycastTarget = to > 0.5f;
        }

        /// <summary>Fades to black, then loads the scene. Without a fader in the scene it loads immediately.</summary>
        public static void Go(string scene)
        {
            if (Instance == null) { SceneManager.LoadScene(scene); return; }
            if (Instance._loading) return;
            Instance._loading = true;
            Instance.StartCoroutine(Instance.GoRoutine(scene));
        }

        private IEnumerator GoRoutine(string scene)
        {
            yield return Fade(0f, 1f, 0.35f);
            SceneManager.LoadScene(scene);
        }
    }
}
