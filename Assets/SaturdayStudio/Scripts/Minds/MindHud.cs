using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaturdayStudio
{
    /// <summary>
    /// The wristwatch (VR: bring the left wrist up to your eyes · desktop: hold T).
    /// There is no floating text in front of the eyes: the inner voice is heard as whispers, the chores and
    /// controls are on the TV, and the time is on the wall clock and the watch.
    /// <see cref="Say"/> and <see cref="Status"/> are kept as no-ops so the behaviours stay simple to read.
    /// </summary>
    public class MindHud : MonoBehaviour
    {
        public static MindHud Instance { get; private set; }

        private TextMeshPro _watch;

        private void Awake()
        {
            Instance = this;
            _watch = new GameObject("Wristwatch").AddComponent<TextMeshPro>();
            _watch.transform.SetParent(transform, false);
            _watch.rectTransform.sizeDelta = new Vector2(0.4f, 0.14f);
            _watch.alignment = TextAlignmentOptions.Center;
            _watch.fontStyle = FontStyles.Bold;
            _watch.outlineWidth = 0.22f;
            _watch.outlineColor = new Color32(0, 0, 0, 200);
            _watch.fontMaterial.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, (float)CompareFunction.Always);
            _watch.text = "";
        }

        /// <summary>Inner-voice line. Not shown as text (no pop-ups); kept for readability of the behaviour code.</summary>
        public static void Say(string line, float seconds = 3.5f) { }

        /// <summary>Hint line. Not shown as text (no pop-ups).</summary>
        public static void Status(string line) { }

        private void LateUpdate()
        {
            var head = MindInput.Head;
            var ms = MindState.Instance;
            bool show = false;
            if (head != null && ms != null && ms.Running)
            {
                var left = MindInput.LeftHand;
                if (left != null && Vector3.Distance(left.position, head.position) < 0.42f)
                {
                    show = true;
                    _watch.transform.position = left.position + (head.position - left.position).normalized * 0.06f + Vector3.up * 0.04f;
                    _watch.fontSize = 0.35f;
                }
                else if (MindInput.WatchHeld())
                {
                    show = true;
                    _watch.transform.position = head.position + head.forward * 0.9f - head.up * 0.12f - head.right * 0.25f;
                    _watch.fontSize = 0.5f;
                }
                if (show) _watch.transform.rotation = Quaternion.LookRotation(_watch.transform.position - head.position, Vector3.up);
            }
            _watch.text = show ? ms.Now : "";
        }
    }
}
