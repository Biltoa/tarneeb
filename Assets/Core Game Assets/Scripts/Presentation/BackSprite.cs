using UnityEngine;
using UnityEngine.UI;

namespace Tarneeb.Presentation
{
    /// <summary>Assigns the runtime card-back sprite to an Image (the deal pile is built in the editor, the sprite loads from Resources).</summary>
    public sealed class BackSprite : MonoBehaviour
    {
        private void Awake() { var im = GetComponent<Image>(); if (im != null) im.sprite = CardArt.Back; }
    }
}
