using Tarneeb.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Decorative card in the menu: the card back, or the deck face with the given Card.ToByte() index.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class DecoCard : MonoBehaviour
    {
        [Range(0, 51)] public int cardIndex;
        public bool showBack;

        private void Awake() => GetComponent<Image>().sprite = showBack ? CardArt.Back : CardArt.Face(Card.FromByte((byte)cardIndex));
    }
}
