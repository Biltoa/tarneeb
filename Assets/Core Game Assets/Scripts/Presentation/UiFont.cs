using TMPro;
using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>Fonts from the Figma design (Cinzel for labels, Noto Serif for numbers), wired by the scene builder.</summary>
    public sealed class UiFont : MonoBehaviour
    {
        public static TMP_FontAsset Title { get; private set; }
        public static TMP_FontAsset Number { get; private set; }
        public TMP_FontAsset title, number;

        private void Awake() { Title = title; Number = number; }
    }
}
