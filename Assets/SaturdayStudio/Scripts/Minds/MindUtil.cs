using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SaturdayStudio
{
    /// <summary>Turns grabbing off for one object (slow start) or for everything (zone-out), in VR and desktop mode.</summary>
    public static class GrabLock
    {
        private static readonly HashSet<Rigidbody> Locked = new HashSet<Rigidbody>();
        public static bool AllLocked { get; private set; }

        public static void Reset()
        {
            Locked.Clear();
            AllLocked = false;
        }

        public static bool CanGrab(Rigidbody rb) => rb != null && !AllLocked && !Locked.Contains(rb);
        public static bool IsLocked(Rigidbody rb) => rb != null && Locked.Contains(rb);

        public static void Lock(Rigidbody rb, bool locked)
        {
            if (rb == null) return;
            if (locked) Locked.Add(rb); else Locked.Remove(rb);
            SetInteractables(rb.gameObject, !locked && !AllLocked);
        }

        public static void LockAll(bool locked)
        {
            AllLocked = locked;
            foreach (var g in Object.FindObjectsByType<Grabbable>())
            {
                var rb = g.GetComponent<Rigidbody>();
                SetInteractables(g.gameObject, !locked && !Locked.Contains(rb));
            }
            if (locked) DesktopFallbackPlayer.ForceDrop();
        }

        private static void SetInteractables(GameObject go, bool on)
        {
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (b is GrabInteractable || b is HandGrabInteractable || b is DistanceHandGrabInteractable || b is DistanceGrabInteractable)
                    b.enabled = on;
        }
    }

    /// <summary>The physical interface of the slides: A / B buttons, wrist, haptics — with desktop keys as stand-ins.</summary>
    public static class MindInput
    {
        private static Transform _left, _right;

        public static bool Desktop => DesktopFallbackPlayer.Active;

        public static Transform Head => Camera.main != null ? Camera.main.transform : null;

        public static Transform LeftHand => Desktop ? null : Find(ref _left, "LeftHandAnchor");
        public static Transform RightHand => Desktop ? null : Find(ref _right, "RightHandAnchor");

        private static Transform Find(ref Transform cache, string name)
        {
            if (cache == null || !cache.gameObject.activeInHierarchy)
            {
                var go = GameObject.Find(name);
                cache = go != null ? go.transform : null;
            }
            return cache;
        }

        /// <summary>Is either VR hand within <paramref name="radius"/> of a point?</summary>
        public static bool HandNear(Vector3 p, float radius)
        {
            if (Desktop) return false;
            var l = LeftHand;
            var r = RightHand;
            return (l != null && (l.position - p).sqrMagnitude < radius * radius) ||
                   (r != null && (r.position - p).sqrMagnitude < radius * radius);
        }

        private static bool KeyDown(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
        private static bool KeyHeld(Key k) => Keyboard.current != null && Keyboard.current[k].isPressed;

        private static bool Ovr(System.Func<bool> f)
        {
            try { return f(); }
            catch { return false; }
        }

        /// <summary>VS1 refocus: A button (desktop R).</summary>
        public static bool RefocusDown() => KeyDown(Key.R) || Ovr(() => OVRInput.GetDown(OVRInput.Button.One));

        /// <summary>Any button on either controller (A, B, X, Y, triggers, thumbsticks) or R / Space / click on desktop.</summary>
        public static bool AnyButtonDown()
        {
            if (KeyDown(Key.R) || KeyDown(Key.Space) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)) return true;
            return Ovr(() => OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Two) ||
                             OVRInput.GetDown(OVRInput.Button.Three) || OVRInput.GetDown(OVRInput.Button.Four) ||
                             OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger) ||
                             OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick) || OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick));
        }

        /// <summary>VS2 resist: hold B (desktop F).</summary>
        public static bool ResistHeld() => KeyHeld(Key.F) || Ovr(() => OVRInput.Get(OVRInput.Button.Two));

        private static float _lastCalmTap = -10f;

        /// <summary>VS2 calm down: press B twice quickly (desktop: F twice).</summary>
        public static bool CalmDoubleTap()
        {
            bool tap = KeyDown(Key.F) || Ovr(() => OVRInput.GetDown(OVRInput.Button.Two));
            if (!tap) return false;
            if (Time.time - _lastCalmTap < 0.4f)
            {
                _lastCalmTap = -10f;
                return true;
            }
            _lastCalmTap = Time.time;
            return false;
        }

        /// <summary>Desktop straighten key (VR uses a hand held next to the object).</summary>
        public static bool StraightenDown() => KeyDown(Key.G);

        /// <summary>Desktop wristwatch (VR: bring the left wrist up to your eyes).</summary>
        public static bool WatchHeld() => KeyHeld(Key.T);

        public static void Buzz(float amplitude, float seconds, bool left, bool right)
        {
            if (Desktop || MindState.Instance == null) return;
            MindState.Instance.StartCoroutine(BuzzRoutine(amplitude, seconds, left, right));
        }

        private static IEnumerator BuzzRoutine(float amplitude, float seconds, bool left, bool right)
        {
            try
            {
                if (left) OVRInput.SetControllerVibration(1f, amplitude, OVRInput.Controller.LTouch);
                if (right) OVRInput.SetControllerVibration(1f, amplitude, OVRInput.Controller.RTouch);
            }
            catch { yield break; }
            yield return new WaitForSeconds(seconds);
            try
            {
                if (left) OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
                if (right) OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            }
            catch { }
        }

        /// <summary>Head position on the floor plane.</summary>
        public static Vector3 FlatHead()
        {
            var h = Head;
            if (h == null) return Vector3.zero;
            var p = h.position;
            p.y = 0;
            return p;
        }

        /// <summary>Is the player looking at a point (within an angle) and close enough?</summary>
        public static bool LookingAt(Vector3 p, float maxAngle, float maxDistance)
        {
            var h = Head;
            if (h == null) return false;
            var d = p - h.position;
            return d.magnitude < maxDistance && Vector3.Angle(h.forward, d) < maxAngle;
        }
    }

    /// <summary>All sounds are synthesised in code, so the project needs no audio files.</summary>
    public static class MindAudio
    {
        private const int Rate = 44100;
        private static AudioClip _buzz, _beep, _heart, _bell, _whisper, _tick, _chime, _water, _breath;

        public static AudioClip PhoneBuzz => Get(ref _buzz, () => Make("buzz", 1.0f, t =>
            (t % 0.4f < 0.25f ? 1f : 0f) * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 150 * t)) * 0.25f));

        public static AudioClip WasherBeep => Get(ref _beep, () => Make("beep", 1.1f, t =>
        {
            float local = t % 0.37f;
            return local < 0.18f ? Mathf.Sin(2 * Mathf.PI * 1250 * t) * 0.45f * Mathf.Min(1, (0.18f - local) * 40) : 0;
        }));

        public static AudioClip Heartbeat => Get(ref _heart, () => Make("heart", 0.45f, t =>
            Thump(t) + 0.7f * Thump(t - 0.2f)));

        public static AudioClip Doorbell => Get(ref _bell, () => Make("bell", 1.6f, t =>
            t < 0.55f ? Mathf.Sin(2 * Mathf.PI * 659 * t) * Mathf.Exp(-t * 3) * 0.5f
                      : Mathf.Sin(2 * Mathf.PI * 523 * t) * Mathf.Exp(-(t - 0.55f) * 2.5f) * 0.5f));

        public static AudioClip Whisper => Get(ref _whisper, () => MakeNoise("whisper", 1.3f));

        public static AudioClip Tick => Get(ref _tick, () => Make("tick", 0.03f, t => (Random01(t) * 2 - 1) * (1 - t / 0.03f) * 0.6f));

        public static AudioClip Chime => Get(ref _chime, () => Make("chime", 0.6f, t =>
            (Mathf.Sin(2 * Mathf.PI * 880 * t) + (t > 0.12f ? Mathf.Sin(2 * Mathf.PI * 1320 * t) : 0)) * Mathf.Exp(-t * 5) * 0.25f));

        public static AudioClip Breath => Get(ref _breath, () => MakeNoise("breath", 8f, 0.1f, false, true));

        public static AudioClip Water => Get(ref _water, () => MakeNoise("water", 1.0f, 0.12f, false));

        private static AudioClip Get(ref AudioClip cache, System.Func<AudioClip> make)
        {
            if (cache == null) cache = make(); // also re-creates clips destroyed when Play mode ended
            return cache;
        }

        private static float Thump(float t) => t < 0 ? 0 : Mathf.Sin(2 * Mathf.PI * 52 * t) * Mathf.Exp(-t * 22) * 0.9f;

        private static float Random01(float t) => Mathf.Repeat(Mathf.Sin(t * 12.9898f * 1000f) * 43758.5453f, 1f);

        private static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int i = 0; i < data.Length; i++) data[i] = f(i / (float)Rate);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Breathy noise shaped like a few whispered syllables (or steady, for running water).</summary>
        private static AudioClip MakeNoise(string name, float seconds, float volume = 0.22f, bool syllables = true, bool breath = false)
        {
            var rng = new System.Random(name.GetHashCode());
            var data = new float[Mathf.CeilToInt(seconds * Rate)];
            float lp = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)rng.NextDouble() * 2 - 1;
                lp += (n - lp) * (syllables ? 0.35f : 0.6f); // soften the hiss a little
                float env = breath ? Mathf.Sin(Mathf.PI * t / seconds * 2f) * Mathf.Sin(Mathf.PI * t / seconds * 2f) // in … out
                    : syllables ? Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 4.5f)) * Mathf.Sin(Mathf.PI * t / seconds) : 1;
                data[i] = lp * env * volume;
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float maxDistance = 6f)
        {
            var go = new GameObject("Sound " + clip.name);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = volume;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.4f;
            s.maxDistance = maxDistance;
            s.Play();
            Object.Destroy(go, clip.length + 0.2f);
            return s;
        }

        public static void Play2D(AudioClip clip, float volume = 1f)
        {
            var h = MindInput.Head;
            if (h == null) return;
            var go = new GameObject("Sound " + clip.name);
            go.transform.SetParent(h, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = volume;
            s.spatialBlend = 0f;
            s.Play();
            Object.Destroy(go, clip.length + 0.2f);
        }
    }

    /// <summary>Makes an object pulse with a coloured glow (task pull, checking targets, crooked objects).</summary>
    public class Glow : MonoBehaviour
    {
        private Renderer[] _renderers;
        private readonly List<Material> _instances = new List<Material>();
        private Color _color;
        private float _until;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        public static Glow On(GameObject go, Color color, float seconds)
        {
            if (go == null) return null;
            var g = go.GetComponent<Glow>();
            if (g == null) g = go.AddComponent<Glow>();
            g._color = color;
            g._until = seconds < 0 ? float.MaxValue : Time.time + seconds;
            g.enabled = true;
            return g;
        }

        public static void Off(GameObject go)
        {
            var g = go != null ? go.GetComponent<Glow>() : null;
            if (g != null) g._until = 0;
        }

        public bool Active => Time.time < _until;

        private void Update()
        {
            if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
            bool on = Time.time < _until;
            float pulse = on ? 0.35f + 0.35f * Mathf.Sin(Time.time * 5f) : 0f;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                foreach (var m in r.materials)
                {
                    if (!_instances.Contains(m)) { _instances.Add(m); m.EnableKeyword("_EMISSION"); }
                    m.SetColor(Emission, _color * pulse);
                }
            }
            if (!on) enabled = false;
        }

        private void OnDisable()
        {
            foreach (var m in _instances) if (m != null) m.SetColor(Emission, Color.black);
            // renderer.materials made per-object copies; the swap back to shared state materials (dish / clothes) still works
            _renderers = null;
            _instances.Clear();
        }
    }
}
