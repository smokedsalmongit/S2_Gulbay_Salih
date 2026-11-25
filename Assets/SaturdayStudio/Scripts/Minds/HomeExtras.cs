using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaturdayStudio
{
    /// <summary>
    /// Finishes the apartment at runtime: a ceiling with a lamp, a city skyline behind the window
    /// (the studio is a few floors up), and a dish-drying rack on the counter next to the sink.
    /// </summary>
    public class HomeExtras : MonoBehaviour
    {
        private Material _baseMat;

        private void Start()
        {
            _baseMat = Resources.Load<Material>("MindGlow");
            BuildCeiling();
            BuildCity();
            DishRack.Build(this);
        }

        public Material Mat(Color color, float smoothness = 0.2f, Texture tex = null, float emission = 0f)
        {
            var m = new Material(_baseMat != null ? _baseMat : new Material(Shader.Find("Universal Render Pipeline/Lit")));
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            if (tex != null) m.mainTexture = tex;
            if (emission > 0)
            {
                m.EnableKeyword("_EMISSION");
                if (tex != null) m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * emission);
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            return m;
        }

        public GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider = false, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ---------- ceiling ----------

        private void BuildCeiling()
        {
            var root = new GameObject("Ceiling").transform;
            // does not cast shadows, so the sunlight through the window still reaches the room
            Box("Ceiling", root, new Vector3(0, 2.75f, 0), new Vector3(6.3f, 0.1f, 5.3f), Mat(new Color(0.95f, 0.94f, 0.91f), 0.05f), false, false);
            var shade = Box("Ceiling Lamp", root, new Vector3(0.2f, 2.66f, 0.1f), new Vector3(0.42f, 0.06f, 0.42f),
                Mat(new Color(1f, 0.95f, 0.85f), 0.3f, null, 1.2f), false, false);
            var lightGo = new GameObject("Ceiling Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.position = new Vector3(0.2f, 2.5f, 0.1f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 7f;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.93f, 0.82f);
            l.shadows = LightShadows.None;
        }

        // ---------- city view ----------

        private void BuildCity()
        {
            var root = new GameObject("City View").transform;
            var rng = new System.Random(42);
            float R() => (float)rng.NextDouble();

            // sky behind everything
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(sky.GetComponent<Collider>());
            sky.name = "Sky";
            sky.transform.SetParent(root, false);
            sky.transform.position = new Vector3(95f, 20f, -5f);
            sky.transform.rotation = Quaternion.Euler(0, 90, 0); // visible from the room (looking +x)
            sky.transform.localScale = new Vector3(260f, 110f, 1f);
            var skyR = sky.GetComponent<Renderer>();
            skyR.sharedMaterial = Mat(Color.white, 0f, SkyTexture(), 1f);
            skyR.shadowCastingMode = ShadowCastingMode.Off;

            // facades: a few colour variants with a window grid texture
            var facades = new List<Material>();
            Color[] tints = { new Color(0.78f, 0.74f, 0.68f), new Color(0.62f, 0.66f, 0.7f), new Color(0.82f, 0.7f, 0.6f), new Color(0.55f, 0.58f, 0.6f), new Color(0.88f, 0.86f, 0.8f) };
            foreach (var t in tints) facades.Add(Mat(t, 0.25f, WindowTexture(rng), 0f));

            // the studio is on the 5th floor: building bases are below the window
            const float ground = -14f;
            for (int i = 0; i < 70; i++)
            {
                float x = 9f + R() * 70f;
                float z = -55f + R() * 100f;
                // keep a little street gap in front of the window
                if (x < 14f && Mathf.Abs(z + 0.9f) < 5f) continue;
                float w = 6f + R() * 10f, d = 6f + R() * 10f;
                float h = 10f + R() * R() * 55f;
                var mat = facades[rng.Next(facades.Count)];
                var b = Box("Building", root, new Vector3(x, ground + h / 2, z), new Vector3(w, h, d), mat, false, false);
                var mr = b.GetComponent<Renderer>();
                var inst = new Material(mat) { mainTextureScale = new Vector2(Mathf.Max(1, Mathf.Round(w / 3.5f)), Mathf.Max(1, Mathf.Round(h / 3.2f))) };
                mr.sharedMaterial = inst;
                if (R() < 0.35f) // roof box / water tank
                    Box("Roof", root, new Vector3(x + (R() - 0.5f) * w * 0.4f, ground + h + 1.2f, z), new Vector3(2.5f, 2.4f, 2.5f), Mat(new Color(0.5f, 0.5f, 0.52f), 0.2f), false, false);
            }
            // a street far below with some trees
            Box("Street", root, new Vector3(40f, ground, -5f), new Vector3(80f, 0.2f, 120f), Mat(new Color(0.35f, 0.36f, 0.37f), 0.1f), false, false);
        }

        private static Texture2D SkyTexture()
        {
            var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var top = new Color(0.45f, 0.66f, 0.9f);
            var horizon = new Color(0.88f, 0.92f, 0.95f);
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;
                var c = Color.Lerp(horizon, top, Mathf.SmoothStep(0.25f, 1f, t));
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        /// <summary>One floor of windows; the material tiles it over the facade.</summary>
        private static Texture2D WindowTexture(System.Random rng)
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var glass = Color.Lerp(new Color(0.3f, 0.38f, 0.45f), new Color(0.55f, 0.65f, 0.72f), (float)rng.NextDouble());
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool window = x > 6 && x < 26 && y > 8 && y < 26;
                bool frame = window && (x == 16);
                var c = window && !frame ? glass * (0.85f + 0.15f * y / s) : Color.white;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }
    }

    /// <summary>A wire rack next to the sink: put a clean dish there and it stands up to dry.</summary>
    public class DishRack : MonoBehaviour
    {
        private readonly List<Transform> _plateSlots = new List<Transform>();
        private readonly List<Transform> _mugSlots = new List<Transform>();
        private readonly HashSet<Transform> _used = new HashSet<Transform>();

        private static readonly Vector3 Center = new Vector3(-1.43f, 0.94f, 2.2f); // countertop, left of the sink

        public static void Build(HomeExtras home)
        {
            // the plate and bowl that stood here move to the table
            MoveProp("[Grab] Plate", new Vector3(-1.5f, 0.945f, 2.15f), new Vector3(-0.72f, 0.79f, -0.3f));
            MoveProp("[Grab] Bowl", new Vector3(-1.6f, 0.945f, 2.32f), new Vector3(-1.65f, 0.79f, -0.45f));

            var root = new GameObject("Dish Drying Rack").transform;
            var metal = home.Mat(new Color(0.78f, 0.8f, 0.82f), 0.7f);
            home.Box("Rack Tray", root, Center + new Vector3(0, 0.008f, 0), new Vector3(0.34f, 0.016f, 0.5f), home.Mat(new Color(0.85f, 0.87f, 0.88f), 0.5f), true);
            // side rails and the wire dividers between the plate slots
            foreach (float dx in new[] { -0.16f, 0.16f })
                home.Box("Rack Rail", root, Center + new Vector3(dx, 0.06f, 0), new Vector3(0.008f, 0.008f, 0.48f), metal);
            var rack = root.gameObject.AddComponent<DishRack>();
            for (int i = 0; i < 7; i++)
            {
                float z = -0.18f + i * 0.048f;
                var slot = new GameObject("Plate Slot").transform;
                slot.SetParent(root, false);
                slot.SetPositionAndRotation(Center + new Vector3(-0.03f, 0.145f, z), Quaternion.Euler(90, 0, 0));
                rack._plateSlots.Add(slot);
                foreach (float dx in new[] { -0.13f, 0.07f })
                    home.Box("Wire", root, Center + new Vector3(dx, 0.05f, z - 0.024f), new Vector3(0.006f, 0.08f, 0.006f), metal);
            }
            foreach (float dz in new[] { 0.17f, 0.22f })
            {
                var slot = new GameObject("Mug Slot").transform;
                slot.SetParent(root, false);
                slot.SetPositionAndRotation(Center + new Vector3(0.11f, 0.11f, dz - 0.03f), Quaternion.Euler(180, 0, 0)); // upside down to drip
                rack._mugSlots.Add(slot);
            }

            var zone = root.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = Center + new Vector3(0, 0.17f, 0);
            zone.size = new Vector3(0.42f, 0.34f, 0.56f);
        }

        private static void MoveProp(string name, Vector3 near, Vector3 to)
        {
            foreach (var r in FindObjectsByType<RespawnIfFallen>())
            {
                if (r.name != name || Vector3.Distance(r.transform.position, near) > 0.15f) continue;
                var rb = r.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = to; rb.linearVelocity = Vector3.zero; }
                r.transform.position = to;
                r.ResetStart();
                return;
            }
        }

        private void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            var dish = rb != null ? rb.GetComponent<Dish>() : null;
            if (dish == null || GrabState.IsHeld(rb)) return;
            TryRack(dish);
        }

        public bool TryRack(Dish dish)
        {
            if (dish.IsDirty || dish.IsRacked) return false;
            var slots = dish.name.Contains("Mug") ? _mugSlots : _plateSlots;
            foreach (var s in slots)
            {
                if (_used.Contains(s)) continue;
                _used.Add(s);
                dish.PutInRack(s, this);
                MindAudio.PlayAt(MindAudio.Tick, s.position, 0.6f);
                return true;
            }
            return false;
        }

        public void Release(Transform slot) => _used.Remove(slot);
    }
}
