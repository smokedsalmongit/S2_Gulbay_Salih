using System;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// The start menu, a panel standing in the room: choose the mind (VS1 ADHD / VS2 OCD) and how strong it is
    /// (mild / moderate / severe), then Start. VR: point the controller ray and press the trigger (or pinch),
    /// or touch a button with your hand. Desktop: aim and press E / click. It comes back at 13:00 to play again.
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        private static readonly Color Idle = new Color(0.23f, 0.27f, 0.3f);
        private static readonly Color AdhdColor = new Color(0.62f, 0.54f, 0.95f);
        private static readonly Color OcdColor = new Color(0.36f, 0.72f, 0.65f);
        private static readonly Color LevelColor = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color StartColor = new Color(0.36f, 0.62f, 0.45f);

        private Transform _root;
        private Mind _mind = Mind.None;
        private Level _level = Level.Moderate;
        private bool _ended;
        private MenuButton _adhd, _ocd, _start;
        private MenuButton[] _levels;
        private TextMeshPro _title, _about;

        private void Start()
        {
            _mind = MindState.NextMind;
            _level = MindState.NextLevel;
            Build(new Vector3(0.2f, 1.35f, -0.65f), Quaternion.Euler(0, 180, 0));
            MindState.MindChosen += OnChosen;
            MindState.DayEnded += OnDayEnded;
            if (MindState.NextMind != Mind.None) _root.gameObject.SetActive(false); // "play again" starts by itself
            Refresh();
        }

        private void OnDestroy()
        {
            MindState.MindChosen -= OnChosen;
            MindState.DayEnded -= OnDayEnded;
        }

        // the panel faces the viewer with its local +z, so the viewer's right is local -x
        private static Vector3 At(float right, float up, float towardViewer = 0) => new Vector3(-right, up, towardViewer);

        private void Build(Vector3 position, Quaternion rotation)
        {
            _root = new GameObject("Start Menu").transform;
            _root.SetPositionAndRotation(position, rotation);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(board.GetComponent<Collider>());
            board.name = "Board";
            board.transform.SetParent(_root, false);
            board.transform.localPosition = At(0, 0, -0.01f);
            board.transform.localScale = new Vector3(1.0f, 0.68f, 0.01f);
            var bm = board.GetComponent<Renderer>();
            bm.sharedMaterial = new Material(bm.sharedMaterial) { color = new Color(0.1f, 0.12f, 0.14f) };

            _title = Text("Title", At(0, 0.25f, 0.002f), new Vector2(0.95f, 0.1f), 0.55f);
            _title.text = "<b>SATURDAY STUDIO</b>";
            Text("Mind Label", At(0, 0.155f, 0.002f), new Vector2(0.9f, 0.06f), 0.26f).text = "<color=#B8C2C8>Whose Saturday do you live?</color>";
            _adhd = Button("VS1 · ADHD\n<size=65%>attention drifts away</size>", At(-0.22f, 0.07f), new Vector2(0.4f, 0.11f), () => { _mind = Mind.ADHD; Refresh(); });
            _ocd = Button("VS2 · OCD\n<size=65%>attention gets stuck</size>", At(0.22f, 0.07f), new Vector2(0.4f, 0.11f), () => { _mind = Mind.OCD; Refresh(); });

            Text("Level Label", At(0, -0.03f, 0.002f), new Vector2(0.9f, 0.06f), 0.26f).text = "<color=#B8C2C8>How strong?</color>";
            _levels = new[]
            {
                Button("Mild", At(-0.3f, -0.1f), new Vector2(0.26f, 0.075f), () => { _level = Level.Mild; Refresh(); }),
                Button("Moderate", At(0, -0.1f), new Vector2(0.26f, 0.075f), () => { _level = Level.Moderate; Refresh(); }),
                Button("Severe", At(0.3f, -0.1f), new Vector2(0.26f, 0.075f), () => { _level = Level.Severe; Refresh(); }),
            };

            _start = Button("Start", At(0, -0.235f), new Vector2(0.42f, 0.09f), OnStart);
            _about = Text("About", At(0, -0.31f, 0.002f), new Vector2(0.95f, 0.05f), 0.2f);
        }

        private TextMeshPro Text(string name, Vector3 local, Vector2 size, float fontSize)
        {
            var t = new GameObject(name).AddComponent<TextMeshPro>();
            t.transform.SetParent(_root, false);
            t.transform.localPosition = local;
            t.transform.localRotation = Quaternion.Euler(0, 180, 0); // TMP reads from its -z side
            t.rectTransform.sizeDelta = size;
            t.fontSize = fontSize;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.color = new Color(0.95f, 0.96f, 0.95f);
            return t;
        }

        private MenuButton Button(string label, Vector3 local, Vector2 size, Action onPress)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Button · " + label.Split('\n')[0];
            go.transform.SetParent(_root, false);
            go.transform.localPosition = local;
            go.transform.localScale = new Vector3(size.x, size.y, 0.02f);
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true; // pointable, but nothing bumps into it
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = new Material(r.sharedMaterial) { color = Idle };

            // VR: ray + trigger / pinch (same Interaction SDK setup as the bed)
            var surface = go.AddComponent<ColliderSurface>();
            surface.InjectCollider(box);
            go.AddComponent<RayInteractable>().InjectSurface(surface);

            var b = go.AddComponent<MenuButton>();
            b.Setup(label.Split('\n')[0], onPress, r);
            go.AddComponent<TaskClickable>().SetTarget(b); // desktop E / click, and the VR ray select

            var t = Text("Label", local + new Vector3(0, 0, 0.012f), size - new Vector2(0.02f, 0.01f), size.y > 0.1f ? 0.36f : 0.3f);
            t.text = label;
            b.LabelText = t;
            return b;
        }

        private void Refresh()
        {
            _adhd.SetColor(_mind == Mind.ADHD ? AdhdColor : Idle);
            _ocd.SetColor(_mind == Mind.OCD ? OcdColor : Idle);
            for (int i = 0; i < _levels.Length; i++) _levels[i].SetColor((int)_level == i ? LevelColor : Idle);
            _start.SetColor(_mind != Mind.None ? StartColor : Idle);
            _start.Enabled = _mind != Mind.None;
            _about.text = _mind == Mind.None ? "<color=#8A949A>Pick a mind first</color>"
                : _ended ? $"<color=#8A949A>Play again as {MindState.Describe(_mind, _level)}</color>"
                : $"<color=#8A949A>{MindState.Describe(_mind, _level)} · friends arrive at 13:00</color>";
        }

        private void OnStart()
        {
            if (_mind == Mind.None) return;
            if (_ended) MindState.LiveAgain(_mind, _level);
            else MindState.Instance.Choose(_mind, _level);
        }

        private void OnChosen(Mind m)
        {
            _root.gameObject.SetActive(false);
            MindAudio.Play2D(MindAudio.Chime, 0.6f);
        }

        private void OnDayEnded()
        {
            _ended = true;
            MindAudio.Play2D(MindAudio.Doorbell, 0.9f);
            // bring the menu back in front of the player
            var head = MindInput.Head;
            if (head != null)
            {
                var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                var pos = head.position + fwd * 0.8f;
                pos.y = Mathf.Clamp(head.position.y - 0.25f, 0.9f, 1.5f);
                _root.SetPositionAndRotation(pos, Quaternion.LookRotation(-fwd)); // local +z towards the player
            }
            _start.SetLabel("Play again");
            _root.gameObject.SetActive(true);
            Refresh();
        }
    }

    /// <summary>One menu button: ray select / desktop E (via <see cref="TaskClickable"/>) or a hand touch in VR.</summary>
    public class MenuButton : MonoBehaviour, IActivatable
    {
        private string _label;
        private Action _onPress;
        private Renderer _renderer;
        private Color _color;
        private float _touch;
        private bool _waitForLeave;
        private float _cooldownUntil;

        public bool Enabled { get; set; } = true;
        public string Label => _label;
        public bool CanActivate => Enabled && gameObject.activeInHierarchy;

        public void Setup(string label, Action onPress, Renderer r)
        {
            _label = label;
            _onPress = onPress;
            _renderer = r;
        }

        public void SetColor(Color c)
        {
            _color = c;
            if (_renderer != null) _renderer.sharedMaterial.color = c;
        }

        public TextMeshPro LabelText { get; set; }

        public void SetLabel(string text)
        {
            _label = text;
            if (LabelText != null) LabelText.text = text;
        }

        public void Activate()
        {
            if (!CanActivate || Time.time < _cooldownUntil) return;
            _cooldownUntil = Time.time + 0.3f;
            MindAudio.PlayAt(MindAudio.Tick, transform.position, 0.9f);
            MindInput.Buzz(0.3f, 0.05f, true, true);
            _onPress?.Invoke();
        }

        private void Update()
        {
            // VR hand touch: hold a fingertip / hand on the button for a moment
            bool near = MindInput.HandNear(transform.position, 0.08f);
            if (near && !_waitForLeave)
            {
                _touch += Time.deltaTime;
                if (_renderer != null) _renderer.sharedMaterial.color = Color.Lerp(_color, Color.white, 0.35f);
                if (_touch > 0.35f)
                {
                    _touch = 0;
                    _waitForLeave = true;
                    Activate();
                }
            }
            else
            {
                _touch = 0;
                if (_renderer != null) _renderer.sharedMaterial.color = _color;
            }
            if (!near) _waitForLeave = false;
        }
    }
}
