using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// Simple skin-coloured hands that follow the controllers (VR) or sit in the lower right of the view (desktop).
    /// In VS2 they show dirt after touching dirty things, and get clean again under the tap.
    /// </summary>
    public class VirtualHands : MonoBehaviour
    {
        public static VirtualHands Instance { get; private set; }

        private static readonly Color Skin = new Color(0.86f, 0.66f, 0.54f);
        private static readonly Color Grime = new Color(0.36f, 0.27f, 0.17f);

        private readonly List<Renderer> _skin = new List<Renderer>();
        private readonly List<GameObject> _spots = new List<GameObject>();
        private Material _skinMat;
        private float _dirt, _shownDirt;

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
            _skinMat = new Material(Resources.Load<Material>("MindGlow") ?? new Material(Shader.Find("Universal Render Pipeline/Lit")));
            _skinMat.DisableKeyword("_EMISSION");
            _skinMat.SetColor("_EmissionColor", Color.black);
            _skinMat.color = Skin;
            _skinMat.SetFloat("_Smoothness", 0.35f);

            if (MindInput.Desktop)
            {
                var cam = MindInput.Head;
                if (cam != null)
                {
                    var h = BuildHand("Desktop Hand", cam, false);
                    h.localPosition = new Vector3(0.2f, -0.2f, 0.38f);
                    h.localRotation = Quaternion.Euler(15, -20, 0);
                }
            }
            else
            {
                HideRigVisuals();
                Attach(MindInput.LeftHand, true);
                Attach(MindInput.RightHand, false);
            }
        }

        private void Attach(Transform anchor, bool left)
        {
            if (anchor == null) return;
            var h = BuildHand(left ? "Left Hand" : "Right Hand", anchor, left);
            h.localPosition = new Vector3(left ? -0.01f : 0.01f, -0.02f, -0.04f);
            // palm turned inwards, as when holding a controller
            h.localRotation = Quaternion.Euler(0, 0, left ? 80 : -80);
        }

        /// <summary>The rig's own controller / hand models would overlap ours, so they are hidden.</summary>
        private static void HideRigVisuals()
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                for (var t = r.transform; t != null; t = t.parent)
                {
                    var n = t.name;
                    if (n.Contains("ControllerVisual") || n.Contains("HandVisual") || n.Contains("OVRControllerPrefab"))
                    {
                        r.enabled = false;
                        break;
                    }
                }
            }
        }

        private Transform BuildHand(string name, Transform parent, bool left)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            float side = left ? -1 : 1;

            // a soft palm (flattened sphere + a box for the knuckle line), four fingers and a thumb
            Part(PrimitiveType.Sphere, "Palm", root, new Vector3(0, 0, 0.005f), Quaternion.identity, new Vector3(0.085f, 0.03f, 0.1f));
            Part(PrimitiveType.Capsule, "Knuckles", root, new Vector3(0, -0.002f, 0.042f), Quaternion.Euler(0, 0, 90), new Vector3(0.024f, 0.04f, 0.022f));
            for (int i = 0; i < 4; i++)
            {
                float x = (-0.03f + i * 0.02f) * side;
                float len = i == 0 ? 0.062f : i == 3 ? 0.05f : 0.07f; // index … little finger
                Part(PrimitiveType.Capsule, "Finger", root, new Vector3(x, -0.006f, 0.05f + len * 0.42f),
                    Quaternion.Euler(98, 0, 0), new Vector3(0.0175f, len * 0.5f, 0.0175f));
            }
            Part(PrimitiveType.Capsule, "Thumb", root, new Vector3(0.048f * -side, -0.008f, 0.02f),
                Quaternion.Euler(80, -38 * side, 0), new Vector3(0.021f, 0.03f, 0.021f));

            // dirt spots (hidden until the hands feel dirty)
            var rng = new System.Random(left ? 3 : 7);
            for (int i = 0; i < 10; i++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * 0.07f;
                float z = ((float)rng.NextDouble() - 0.3f) * 0.12f;
                float y = (i % 2 == 0 ? 1 : -1) * 0.014f; // back of the hand and palm
                var spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(spot.GetComponent<Collider>());
                spot.name = "Dirt";
                spot.transform.SetParent(root, false);
                spot.transform.localPosition = new Vector3(x, y, z);
                float s = 0.012f + (float)rng.NextDouble() * 0.014f;
                spot.transform.localScale = new Vector3(s, 0.004f, s * 1.3f);
                var r = spot.GetComponent<Renderer>();
                r.sharedMaterial = new Material(_skinMat) { color = Grime };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                spot.SetActive(false);
                _spots.Add(spot);
            }
            return root;
        }

        private void Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _skinMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _skin.Add(r);
        }

        private void Update()
        {
            if (_skinMat == null) return;
            _shownDirt = Mathf.MoveTowards(_shownDirt, _dirt, Time.deltaTime * 1.5f);
            _skinMat.color = Color.Lerp(Skin, Color.Lerp(Skin, Grime, 0.45f), _shownDirt);
            for (int i = 0; i < _spots.Count; i++)
            {
                bool on = _shownDirt > 0.05f && (i % 10) < _shownDirt * 10f; // more spots the dirtier, on each hand
                if (_spots[i].activeSelf != on) _spots[i].SetActive(on);
            }
        }
    }
}
