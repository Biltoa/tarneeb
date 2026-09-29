using System;
using Tarneeb.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Four suit buttons Pick_Clubs/Diamonds/Hearts/Spades, each with an Icon child.</summary>
    public sealed class TrumpPicker : MonoBehaviour
    {
        private Action<Suit> _onPick;
        private bool _bound;

        private void Awake() => Bind();

        private void Bind()
        {
            if (_bound) return;
            _bound = true;
            foreach (Suit s in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades })
            {
                Suit suit = s;
                Button b = UiBind.Get<Button>(transform, "Pick_" + s);
                b.onClick.AddListener(() => Pick(suit));
                Image icon = UiBind.Get<Image>(transform, "Pick_" + s + "/Icon");
                if (CardArt.Instance != null) icon.sprite = CardArt.Instance.SuitSprite(s);
                icon.color = CardArt.SuitColor(s);
            }
            gameObject.SetActive(false);
        }

        public void Show(Action<Suit> onPick)
        {
            Bind();
            _onPick = onPick;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            Bind();
            _onPick = null;
            gameObject.SetActive(false);
        }

        private void Pick(Suit s)
        {
            Action<Suit> cb = _onPick;
            Hide();
            cb?.Invoke(s);
        }
    }
}
