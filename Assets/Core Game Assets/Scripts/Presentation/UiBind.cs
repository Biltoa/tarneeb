using UnityEngine;

namespace Tarneeb.Presentation
{
    /// <summary>Finds child components by relative path so views work without hand-wired serialized fields.</summary>
    internal static class UiBind
    {
        public static T Get<T>(Transform root, string path) where T : Component
        {
            Transform t = string.IsNullOrEmpty(path) ? root : root.Find(path);
            if (t == null)
            {
                Debug.LogError("[UiBind] '" + root.name + "' is missing child '" + path + "'", root);
                return null;
            }
            T c = t.GetComponent<T>();
            if (c == null) Debug.LogError("[UiBind] '" + root.name + "/" + path + "' has no " + typeof(T).Name, t);
            return c;
        }

        public static GameObject Go(Transform root, string path)
        {
            Transform t = root.Find(path);
            if (t == null)
            {
                Debug.LogError("[UiBind] '" + root.name + "' is missing child '" + path + "'", root);
                return null;
            }
            return t.gameObject;
        }
    }
}
