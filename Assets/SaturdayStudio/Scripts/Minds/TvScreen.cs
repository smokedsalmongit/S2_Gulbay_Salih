using TMPro;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// The television shows the to-do list with a progress bar per chore, plus the controls for the chosen mind.
    /// Before the start it invites you to use the menu; at 13:00 it shows where the three hours went.
    /// </summary>
    public class TvScreen : MonoBehaviour
    {
        public static TvScreen Instance { get; private set; }

        private const float W = 1.2f, H = 0.66f;

        private Transform _root;
        private TextMeshPro _title, _footer, _summary;
        private Row[] _rows;
        private Material _baseMat;

        private class Row
        {
            public TextMeshPro Label;
            public Transform Fill;
            public Renderer FillRenderer;
        }

        private void Awake() => Instance = this;

        private void Start()
        {
            var tv = GameObject.Find("TV");
            _baseMat = Resources.Load<Material>("MindGlow");
            _root = new GameObject("TV Screen").transform;
            if (tv != null)
            {
                var b = tv.GetComponent<Renderer>().bounds;
                _root.position = new Vector3(b.center.x, b.center.y, b.max.z + 0.002f);
            }
            else _root.position = new Vector3(1.2f, 1.25f, -2.448f);
            // the TV is on the front wall and the room is on its +z side: local +z points at the viewer,
            // so the viewer's right is local -x
            _root.rotation = Quaternion.identity;

            Quad("Glass", new Vector3(0, 0, 0), new Vector2(W + 0.04f, H + 0.04f), new Color(0.05f, 0.08f, 0.11f), 0.6f);
            _title = Text("Title", new Vector3(0, 0.25f, 0.004f), new Vector2(W - 0.08f, 0.12f), 0.75f, TextAlignmentOptions.Center);
            _footer = Text("Controls", new Vector3(0, -0.265f, 0.004f), new Vector2(W - 0.06f, 0.11f), 0.38f, TextAlignmentOptions.Center);
            _footer.enableAutoSizing = true;
            _footer.fontSizeMin = 0.2f;
            _footer.fontSizeMax = 0.38f;
            _footer.color = new Color(0.62f, 0.89f, 0.78f);
            _summary = Text("Summary", new Vector3(0, 0, 0.004f), new Vector2(W - 0.1f, H - 0.06f), 0.4f, TextAlignmentOptions.Center);
            _summary.enableAutoSizing = true;
            _summary.fontSizeMin = 0.12f;
            _summary.fontSizeMax = 0.4f;

            string[] names = { "Dishes", "Bed", "Laundry", "Floor" };
            _rows = new Row[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                float y = 0.13f - i * 0.105f;
                var label = Text(names[i], new Vector3(0.27f, y, 0.004f), new Vector2(0.6f, 0.09f), 0.5f, TextAlignmentOptions.Left);
                label.enableAutoSizing = true;
                label.fontSizeMin = 0.25f;
                label.fontSizeMax = 0.5f;
                Quad("Bar " + names[i], new Vector3(-0.3f, y, 0.002f), new Vector2(0.5f, 0.05f), new Color(0.2f, 0.25f, 0.3f), 0.4f);
                var pivot = new GameObject("Fill " + names[i]).transform;
                pivot.SetParent(_root, false);
                pivot.localPosition = new Vector3(-0.05f, y, 0.003f); // left edge of the bar (as the viewer sees it)
                var fill = Quad("Fill", Vector3.zero, Vector2.one, new Color(0.45f, 0.82f, 0.68f), 1f);
                fill.SetParent(pivot, false);
                fill.localPosition = new Vector3(-0.5f, 0, 0); // grows towards the viewer's right
                pivot.localScale = new Vector3(0, 0.05f, 1);
                _rows[i] = new Row { Label = label, Fill = pivot, FillRenderer = fill.GetComponent<Renderer>() };
            }
            Refresh();
        }

        private Transform Quad(string name, Vector3 local, Vector2 size, Color color, float glow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0, 180, 0); // quads are visible from their -z side
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var r = go.GetComponent<Renderer>();
            var m = new Material(_baseMat != null ? _baseMat : r.sharedMaterial) { color = color };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * glow);
            r.sharedMaterial = m;
            return go.transform;
        }

        private TextMeshPro Text(string name, Vector3 local, Vector2 size, float fontSize, TextAlignmentOptions align)
        {
            var t = new GameObject(name).AddComponent<TextMeshPro>();
            t.transform.SetParent(_root, false);
            t.transform.localPosition = local;
            t.transform.localRotation = Quaternion.Euler(0, 180, 0); // TMP reads from its -z side
            t.rectTransform.sizeDelta = size;
            t.fontSize = fontSize;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.color = new Color(0.95f, 0.96f, 0.95f);
            return t;
        }

        private float _next;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            if (_rows == null) return;
            var ms = MindState.Instance;
            var mind = MindState.Current;
            bool running = ms != null && ms.Running;
            bool ended = ms != null && ms.Ended;

            _summary.gameObject.SetActive(ended);
            foreach (var r in _rows) r.Label.gameObject.SetActive(running);
            SetRowsVisible(running);

            if (ended)
            {
                _title.text = "";
                _footer.text = "";
                _summary.text = MindState.Summary();
                return;
            }
            if (!running)
            {
                _title.text = "<b>SATURDAY STUDIO</b>";
                _footer.text = "Friends arrive at 13:00.\nChoose a mind and a level on the menu, then press Start.";
                return;
            }

            var p = ChoreBoard.Progress;
            _title.text = $"<b>TO DO</b>  <size=70%><color=#B8C2C8>{MindState.Describe(mind, MindState.CurrentLevel)}</color></size>";
            SetRow(0, $"Dishes  {p.DishesClean}/{p.DishesTotal} clean · {p.DishesRacked} drying",
                p.DishesTotal > 0 ? (p.DishesClean + p.DishesRacked) / (2f * p.DishesTotal) : 0);
            SetRow(1, p.BedMade ? "Bed  made" : "Bed  unmade", p.BedMade ? 1 : 0);
            float laundry = p.ClothesTotal > 0 ? (p.Washed + p.Hung) / (2f * p.ClothesTotal) : 0;
            SetRow(2, $"Laundry  {p.Washed}/{p.ClothesTotal} washed · {p.Hung} hung", laundry);
            bool floor = FloorChore.Instance == null || !FloorChore.Instance.Disabled;
            ShowRow(3, floor);
            if (floor) SetRow(3, $"Floor  {p.StainsClean}/{p.StainsTotal}", p.StainsTotal > 0 ? p.StainsClean / (float)p.StainsTotal : 1);

            bool desktop = MindInput.Desktop;
            _footer.text = mind == Mind.ADHD
                ? (desktop ? "Zoned out? press R ×3  ·  hold T: watch" : "Zoned out? press A ×3  ·  raise your left wrist: watch")
                : (desktop ? "E: wash hands at the tap · check stove / door / washer  ·  G: straighten  ·  hold F: resist"
                           : "Hands under the tap: wash  ·  hand on stove / door / washer: check  ·  hold B: resist");
        }

        private void SetRowsVisible(bool show)
        {
            foreach (Transform child in _root)
                if (child.name.StartsWith("Bar ") || child.name.StartsWith("Fill ")) child.gameObject.SetActive(show);
        }

        private void ShowRow(int i, bool show)
        {
            _rows[i].Label.gameObject.SetActive(show);
            _rows[i].Fill.gameObject.SetActive(show);
            string n = _rows[i].Fill.name.Replace("Fill ", "");
            var bar = _root.Find("Bar " + n);
            if (bar != null) bar.gameObject.SetActive(show);
        }

        private void SetRow(int i, string label, float fraction)
        {
            var r = _rows[i];
            fraction = Mathf.Clamp01(fraction);
            r.Label.text = fraction >= 1 ? $"<color=#7FD1A9>{label}  · done</color>" : label;
            r.Fill.localScale = new Vector3(0.5f * fraction, 0.05f, 1);
        }
    }
}
