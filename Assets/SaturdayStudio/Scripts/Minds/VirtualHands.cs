using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// The hands you see. VR: Meta's own hand models (the Interaction SDK hands, driven by controllers or hand
    /// tracking) are recoloured to a visible skin tone. Desktop: a simple hand in the lower right of the view.
    /// In VS2 they turn grimy after touching dirty things — brown tint plus dirt on the knuckles — and get clean under the tap.
    /// </summary>
    public class VirtualHands : MonoBehaviour
    {
        public static VirtualHands Instance { get; private set; }

        private static readonly Color Skin = new Color(0.86f, 0.66f, 0.54f);
        private static readonly Color SkinShade = new Color(0.7f, 0.5f, 0.4f);
        private static readonly Color Grime = new Color(0.33f, 0.24f, 0.15f);

        private static readonly int ColorTop = Shader.PropertyToID("_ColorTop");
        private static readonly int ColorBottom = Shader.PropertyToID("_ColorBottom");
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");

        private readonly List<Renderer> _metaHands = new List<Renderer>();
        private readonly List<GameObject> _spots = new List<GameObject>();
        private Material _skinMat, _dirtMat;
        private MaterialPropertyBlock _block;
        private float _dirt, _shownDirt;
        private float _nextScan;

        /// <summary>0 = clean, 1 = very dirty.</summary>
        public float Dirt
        {
            get => _dirt;
            set => _dirt = Mathf.Clamp01(value);
        }

        private void Awake() => Instance = this;

        private IEnumerator Start()
        {
            // wait until we know whether this is VR or desktop mode
            yield return new WaitForSeconds(1.3f);
            var baseMat = Resources.Load<Material>("MindGlow");
            _skinMat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _skinMat.DisableKeyword("_EMISSION");
            _skinMat.SetColor("_EmissionColor", Color.black);
            _skinMat.color = Skin;
            _skinMat.SetFloat("_Smoothness", 0.35f);
            _dirtMat = new Material(_skinMat) { color = Grime };
            _block = new MaterialPropertyBlock();

            if (MindInput.Desktop)
            {
                var cam = MindInput.Head;
                if (cam != null)
                {
                    var h = BuildDesktopHand(cam);
                    h.localPosition = new Vector3(0.2f, -0.2f, 0.38f);
                    h.localRotation = Quaternion.Euler(15, -20, 0);
                }
            }
        }

        // ---------- VR: Meta's hands ----------

        /// <summary>Finds the Interaction SDK hand meshes and puts dirt blobs on their knuckle and finger joints.</summary>
        private void ScanMetaHands()
        {
            _metaHands.Clear();
            foreach (var r in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include))
            {
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || !m.shader.name.Contains("OculusHand")) continue;
                _metaHands.Add(r);
            }
            if (_spots.Count > 0) return;
            var bones = new[] { "index1", "index2", "middle1", "middle2", "ring1", "ring2", "pinky1", "thumb2", "thumb3", "index3" };
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                string n = t.name.ToLower();
                if (!(n.StartsWith("b_l_") || n.StartsWith("b_r_"))) continue;
                foreach (var b in bones)
                {
                    if (!n.EndsWith(b)) continue;
                    var spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(spot.GetComponent<Collider>());
                    spot.name = "Dirt";
                    spot.transform.SetParent(t, false);
                    spot.transform.localPosition = Vector3.zero;
                    spot.transform.localScale = Vector3.one * 0.019f; // a little thicker than the finger, so it shows as a smear
                    var sr = spot.GetComponent<Renderer>();
                    sr.sharedMaterial = _dirtMat;
                    sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    spot.SetActive(false);
                    _spots.Add(spot);
                }
            }
        }

        private void TintMetaHands(float dirt)
        {
            foreach (var r in _metaHands)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(ColorTop, Color.Lerp(Skin, Grime, dirt * 0.7f));
                _block.SetColor(ColorBottom, Color.Lerp(SkinShade, Grime, dirt * 0.8f));
                _block.SetFloat(Opacity, 1f);
                r.SetPropertyBlock(_block);
            }
        }

        // ---------- desktop hand ----------

        private Transform BuildDesktopHand(Transform parent)
        {
            var root = new GameObject("Desktop Hand").transform;
            root.SetParent(parent, false);
            Part(PrimitiveType.Sphere, root, new Vector3(0, 0, 0.005f), Quaternion.identity, new Vector3(0.085f, 0.03f, 0.1f));
            Part(PrimitiveType.Capsule, root, new Vector3(0, -0.002f, 0.042f), Quaternion.Euler(0, 0, 90), new Vector3(0.024f, 0.04f, 0.022f));
            for (int i = 0; i < 4; i++)
            {
                float len = i == 0 ? 0.062f : i == 3 ? 0.05f : 0.07f;
                Part(PrimitiveType.Capsule, root, new Vector3(-0.03f + i * 0.02f, -0.006f, 0.05f + len * 0.42f),
                    Quaternion.Euler(98, 0, 0), new Vector3(0.0175f, len * 0.5f, 0.0175f));
            }
            Part(PrimitiveType.Capsule, root, new Vector3(-0.048f, -0.008f, 0.02f), Quaternion.Euler(80, -38, 0), new Vector3(0.021f, 0.03f, 0.021f));

            var rng = new System.Random(7);
            for (int i = 0; i < 10; i++)
            {
                var spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(spot.GetComponent<Collider>());
                spot.name = "Dirt";
                spot.transform.SetParent(root, false);
                spot.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.07f, 0.014f, ((float)rng.NextDouble() - 0.3f) * 0.12f);
                float s = 0.012f + (float)rng.NextDouble() * 0.014f;
                spot.transform.localScale = new Vector3(s, 0.004f, s * 1.3f);
                spot.GetComponent<Renderer>().sharedMaterial = _dirtMat;
                spot.SetActive(false);
                _spots.Add(spot);
            }
            return root;
        }

        private void Part(PrimitiveType type, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _skinMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void Update()
        {
            if (_skinMat == null) return;
            if (!MindInput.Desktop && Time.time > _nextScan)
            {
                _nextScan = Time.time + 3f; // hands can switch between controllers and hand tracking
                ScanMetaHands();
            }
            _shownDirt = Mathf.MoveTowards(_shownDirt, _dirt, Time.deltaTime * 1.5f);
            _skinMat.color = Color.Lerp(Skin, Color.Lerp(Skin, Grime, 0.45f), _shownDirt);
            TintMetaHands(_shownDirt);
            for (int i = 0; i < _spots.Count; i++)
            {
                bool on = _shownDirt > 0.05f && (i % 10) < _shownDirt * 10f;
                if (_spots[i] != null && _spots[i].activeSelf != on) _spots[i].SetActive(on);
            }
        }
    }
}
