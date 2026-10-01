using System;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Buttons Bid7..Bid13 plus Pass. Only bids above the current high bid are enabled.</summary>
    public sealed class BidPanel : MonoBehaviour
    {
        private const int First = 7, Last = 13;
        private Button[] _bidButtons;
        private Button _pass;
        private Action<int> _onBid;
        private Action _onPass;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            _bidButtons = new Button[Last - First + 1];
            for (int n = First; n <= Last; n++)
            {
                int amount = n;
                Button b = UiBind.Get<Button>(transform, "Bid" + n);
                _bidButtons[n - First] = b;
                b.onClick.AddListener(() => Choose(amount));
            }
            _pass = UiBind.Get<Button>(transform, "Pass");
            _pass.onClick.AddListener(ChoosePass);
            gameObject.SetActive(false);
        }

        public void Show(int minimumBid, Action<int> onBid, Action onPass)
        {
            Bind();
            _onBid = onBid;
            _onPass = onPass;
            for (int n = First; n <= Last; n++) _bidButtons[n - First].interactable = n >= minimumBid;
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(Intro());
        }

        private System.Collections.IEnumerator Intro()
        {
            AudioManager.Play(Sfx.Whoosh, 0.5f);
            transform.localScale = Vector3.one * 0.7f;
            var group = GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 0f;
            for (float t = 0f; t < 0.28f; t += Time.deltaTime)
            {
                float k = Tween.Evaluate(Ease.OutBack, t / 0.28f);
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, k);
                if (group != null) group.alpha = Mathf.Clamp01(t / 0.18f);
                yield return null;
            }
            transform.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;
        }

        public void Hide()
        {
            Bind();
            _onBid = null;
            _onPass = null;
            gameObject.SetActive(false);
        }

        private void Choose(int amount)
        {
            Action<int> cb = _onBid;
            Hide();
            cb?.Invoke(amount);
        }

        private void ChoosePass()
        {
            Action cb = _onPass;
            Hide();
            cb?.Invoke();
        }
    }
}
