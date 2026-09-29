using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>Scales a fixed-size design frame (default 1080x1920) uniformly to fit its parent canvas (letterbox).</summary>
    [ExecuteAlways]
    public sealed class FitScale : MonoBehaviour
    {
        public Vector2 designSize = new Vector2(1080f, 1920f);

        private void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            Rect pr = parent.rect;
            if (pr.width <= 0f || pr.height <= 0f) return;
            float s = Mathf.Min(pr.width / designSize.x, pr.height / designSize.y);
            rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
