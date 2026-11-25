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
        private TextMeshPro _title, _about, _info;
        private bool _busy;

        /// <summary>The introduction happens in its own quiet room, away from the apartment.</summary>
        public static readonly Vector3 IntroCenter = new Vector3(-40f, 0f, 0f);

        private void Start()
        {
            _mind = MindState.NextMind;
            _level = MindState.NextLevel;
            BuildIntroRoom();
            Build(IntroCenter + new Vector3(0, 1.35f, 1.0f), Quaternion.Euler(0, 180, 0));
            MindState.MindChosen += OnChosen;
            MindState.DayEnded += OnDayEnded;
            if (MindState.NextMind != Mind.None)
            {
                // "play again": straight into the apartment, MindState starts the chosen mind by itself
                PlayerRig.ToApartment();
            }
            else PlayerRig.TeleportTo(IntroCenter + new Vector3(0, 0, -0.5f), 0f);
            Refresh();
        }

        private void BuildIntroRoom()
        {
            var root = new GameObject("Introduction Room").transform;
            var dark = new Material(Resources.Load<Material>("MindGlow")) { color = new Color(0.12f, 0.14f, 0.17f) };
            dark.DisableKeyword("_EMISSION");
            dark.SetColor("_EmissionColor", Color.black);
            var floor = new Material(dark) { color = new Color(0.2f, 0.22f, 0.25f) };
            void Slab(Vector3 local, Vector3 size, Material m, bool collider)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (!collider) Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.transform.position = IntroCenter + local;
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m;
            }
            Slab(new Vector3(0, -0.05f, 0), new Vector3(7, 0.1f, 7), floor, true);
            Slab(new Vector3(0, 3.5f, 0), new Vector3(7, 0.1f, 7), dark, false);
            Slab(new Vector3(0, 1.75f, 3.5f), new Vector3(7, 3.5f, 0.1f), dark, true);
            Slab(new Vector3(0, 1.75f, -3.5f), new Vector3(7, 3.5f, 0.1f), dark, true);
            Slab(new Vector3(3.5f, 1.75f, 0), new Vector3(0.1f, 3.5f, 7), dark, true);
            Slab(new Vector3(-3.5f, 1.75f, 0), new Vector3(0.1f, 3.5f, 7), dark, true);
            var l = new GameObject("Intro Light").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = IntroCenter + new Vector3(0, 3f, 0.3f);
            l.type = LightType.Point;
            l.range = 9f;
            l.intensity = 1.4f;
            l.color = new Color(0.95f, 0.92f, 1f);

            // explanation board, to the right of the menu and turned towards the player
            _info = new GameObject("Introduction Text").AddComponent<TextMeshPro>();
            _info.transform.SetParent(root, false);
            _info.transform.position = IntroCenter + new Vector3(1.05f, 1.45f, 0.75f);
            _info.transform.rotation = Quaternion.Euler(0, 40, 0);
            _info.rectTransform.sizeDelta = new Vector2(0.95f, 0.95f);
            _info.fontSize = 0.3f;
            _info.enableAutoSizing = true;
            _info.fontSizeMin = 0.15f;
            _info.fontSizeMax = 0.3f;
            _info.alignment = TextAlignmentOptions.TopLeft;
            _info.textWrappingMode = TextWrappingModes.Normal;
            _info.color = new Color(0.9f, 0.92f, 0.93f);
        }

        private string InfoText()
        {
            if (_ended) return MindState.Summary();
            bool desktop = MindInput.Desktop;
            string intro = "<b>A Saturday morning, lived through another mind</b>\n" +
                           "<size=80%>It is 10:00. Friends arrive at 13:00. The apartment needs tidying: dishes, bed, laundry" +
                           (_mind == Mind.OCD ? "" : ", floor") + ". The to-do list is on the TV, the time on the kitchen clock.</size>\n\n";
            string move = desktop
                ? "<size=75%><b>Controls</b>  WASD walk · mouse look · E / click: pick up, drop, use</size>\n"
                : "<size=75%><b>Controls</b>  left stick: walk · right stick: turn · grip: grab (also from a distance) · trigger: use</size>\n";
            if (_mind == Mind.ADHD)
                return intro + "<color=#B9A6FF><b>VS1 · ADHD: attention drifts away</b></color>\n<size=80%>" +
                       "Boring tasks are hard to start. Now and then your mind drifts off: the room turns grey and blurry and time speeds up. " +
                       (desktop ? "Press R or Space a few times" : "Press any button a few times") + " to come back (or wait). " +
                       "Other chores and your phone pull you away. " +
                       (desktop ? "Hold T to look at your watch." : "Raise your left wrist to look at your watch.") + "</size>\n\n" + move;
            if (_mind == Mind.OCD)
                return intro + "<color=#7FD1C1><b>VS2 · OCD: attention gets stuck</b></color>\n<size=80%>" +
                       "Touching dirty things makes your hands feel dirty; wash them under the kitchen tap. Doubts like \"is the stove off?\" pull you back to check. " +
                       "Crooked things have to be straightened. " +
                       (desktop ? "Hold F to resist an urge, press F twice to calm down." : "Hold B to resist an urge, press B twice to calm down.") + "</size>\n\n" + move;
            return intro + "<size=80%>Choose whose Saturday you want to live, and how strong it is, then press Start.</size>\n\n" + move;
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
            if (_info != null) _info.text = InfoText();
            _about.text = _mind == Mind.None ? "<color=#8A949A>Pick a mind first</color>"
                : _ended ? $"<color=#8A949A>Play again as {MindState.Describe(_mind, _level)}</color>"
                : $"<color=#8A949A>{MindState.Describe(_mind, _level)} · friends arrive at 13:00</color>";
        }

        private void OnStart()
        {
            if (_mind == Mind.None || _busy) return;
            if (_ended) { StartCoroutine(FadeThen(() => MindState.LiveAgain(_mind, _level), false)); return; }
            StartCoroutine(FadeThen(() =>
            {
                PlayerRig.ToApartment();
                MindState.Instance.Choose(_mind, _level);
            }, true));
        }

        /// <summary>Fade to black, do something (move the player), fade back in.</summary>
        private System.Collections.IEnumerator FadeThen(Action action, bool fadeIn)
        {
            _busy = true;
            var post = MindPost.Instance;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.6f)
            {
                if (post != null) post.Fade = t;
                yield return null;
            }
            if (post != null) post.Fade = 1;
            action();
            yield return new WaitForSeconds(0.3f);
            if (fadeIn)
                for (float t = 1; t > 0; t -= Time.deltaTime / 0.8f)
                {
                    if (post != null) post.Fade = t;
                    yield return null;
                }
            if (post != null) post.Fade = 0;
            _busy = false;
        }

        private void OnChosen(Mind m)
        {
            MindAudio.Play2D(MindAudio.Chime, 0.6f);
        }

        private void OnDayEnded()
        {
            _ended = true;
            MindAudio.Play2D(MindAudio.Doorbell, 0.9f);
            _start.SetLabel("Play again");
            Refresh();
            StartCoroutine(BackToIntro());
        }

        private System.Collections.IEnumerator BackToIntro()
        {
            yield return new WaitForSeconds(4f); // hear the doorbell, glance at the TV
            yield return FadeThen(() => PlayerRig.TeleportTo(IntroCenter + new Vector3(0, 0, -0.5f), 0f), true);
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
