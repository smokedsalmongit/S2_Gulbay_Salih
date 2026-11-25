using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// Named marker objects ("Spot · Tap", "Spot · Stove", …) that tell the game where things are in a scene.
    /// The original procedural room has none, so every caller passes the old position as the fallback.
    /// </summary>
    public static class Spots
    {
        public static Transform Find(string name)
        {
            var go = GameObject.Find("Spot · " + name);
            return go != null ? go.transform : null;
        }

        public static Vector3 Pos(string name, Vector3 fallback)
        {
            var t = Find(name);
            return t != null ? t.position : fallback;
        }

        public static Quaternion Rot(string name, Quaternion fallback)
        {
            var t = Find(name);
            return t != null ? t.rotation : fallback;
        }

        public static float Scale(string name, float fallback)
        {
            var t = Find(name);
            return t != null ? t.localScale.x : fallback;
        }

        public static GameObject Object(string spotName, string fallbackObject)
        {
            var t = Find(spotName);
            if (t != null && t.childCount == 0 && t.GetComponent<SpotTarget>() is SpotTarget st && st.Target != null) return st.Target;
            return GameObject.Find(fallbackObject);
        }
    }

    /// <summary>Optional: the object a spot refers to (for example what glows when the stove is in doubt).</summary>
    public class SpotTarget : MonoBehaviour
    {
        public GameObject Target;
    }
}
