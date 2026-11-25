using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// An analogue clock on the kitchen wall showing the in-game time (10:00 → 13:00). It ticks quietly;
    /// in VS1 its hands race during a zone-out, in VS2 it simply keeps going while the rituals eat the minutes.
    /// </summary>
    public class WallClock : MonoBehaviour
    {
        private Vector3 Center;
        private const float Radius = 0.19f;

        private Transform _hour, _minute;
        private float _nextTick;

        private void Start()
        {
            Center = Spots.Pos("Clock", new Vector3(-0.2f, 1.95f, 2.49f));
            var root = new GameObject("Wall Clock").transform;
            root.position = Center;
            // local +z points into the room: higher z = closer to the viewer
            root.rotation = Spots.Rot("Clock", Quaternion.Euler(0, 180, 0));

            Part(PrimitiveType.Cylinder, "Rim", root, new Vector3(0, 0, -0.008f), Quaternion.Euler(90, 0, 0),
                new Vector3(Radius * 2.15f, 0.012f, Radius * 2.15f), new Color(0.24f, 0.27f, 0.29f));
            Part(PrimitiveType.Cylinder, "Face", root, new Vector3(0, 0, 0.002f), Quaternion.Euler(90, 0, 0),
                new Vector3(Radius * 2f, 0.013f, Radius * 2f), new Color(0.98f, 0.97f, 0.94f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0) * Radius * 0.82f;
                bool major = i % 3 == 0;
                Part(PrimitiveType.Cube, "Mark", root, p + new Vector3(0, 0, 0.018f), Quaternion.Euler(0, 0, -i * 30f),
                    new Vector3(major ? 0.014f : 0.007f, major ? 0.045f : 0.03f, 0.004f), new Color(0.2f, 0.22f, 0.24f));
            }
            _hour = Hand("Hour Hand", root, 0.1f, 0.016f, 0.022f, new Color(0.15f, 0.16f, 0.18f));
            _minute = Hand("Minute Hand", root, 0.15f, 0.01f, 0.026f, new Color(0.15f, 0.16f, 0.18f));
            Part(PrimitiveType.Cylinder, "Cap", root, new Vector3(0, 0, 0.03f), Quaternion.Euler(90, 0, 0),
                new Vector3(0.025f, 0.005f, 0.025f), new Color(0.75f, 0.35f, 0.25f));
            Set(0);
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 local, Quaternion rot, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = new Material(r.sharedMaterial) { color = color };
            return go;
        }

        /// <summary>A hand pivoting at the centre: the visible bar is offset upwards inside an empty pivot.</summary>
        private static Transform Hand(string name, Transform parent, float length, float width, float z, Color color)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = new Vector3(0, 0, z);
            Part(PrimitiveType.Cube, "Bar", pivot, new Vector3(0, length * 0.4f, 0), Quaternion.identity, new Vector3(width, length, 0.004f), color);
            return pivot;
        }

        private void Set(float minutesSince10)
        {
            float total = 10 * 60 + minutesSince10;
            // the clock faces into the room (rotated 180°), so +angle reads clockwise for someone looking at it
            if (_minute != null) _minute.localRotation = Quaternion.Euler(0, 0, (total % 60f) * 6f);
            if (_hour != null) _hour.localRotation = Quaternion.Euler(0, 0, ((total / 60f) % 12f) * 30f);
        }

        private void Update()
        {
            var ms = MindState.Instance;
            if (ms == null) return;
            Set(ms.Minute);
            if (ms.Running && Time.time >= _nextTick)
            {
                // ticks speed up with the clock (a zone-out makes it race)
                _nextTick = Time.time + 1f / Mathf.Max(1f, ms.ClockSpeed);
                MindAudio.PlayAt(MindAudio.Tick, Center, 0.25f, 3.5f);
            }
        }
    }
}
