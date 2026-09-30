using Tarneeb.Core.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Decorative card in the menu: shows the deck face with the given Card.ToByte() index.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class DecoCard : MonoBehaviour
    {
        [Range(0, 51)] public int cardIndex;

        private void Awake() => GetComponent<Image>().sprite = CardArt.Face(Card.FromByte((byte)cardIndex));
    }
}
