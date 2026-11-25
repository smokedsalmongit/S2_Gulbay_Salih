using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// VS2 · OCD — "completion does not reliably produce relief".
    /// Contamination (dirty things leave the hands feeling dirty; it spreads to what you touch next),
    /// the relief loop (washing hands calms only briefly, the next touch triggers faster),
    /// checking (stove / front door / washer door; certainty fades after ~20 s),
    /// just right (crooked things pull at you; fixing sometimes "isn't quite right" again),
    /// resisting (hold B / F: anxiety peaks, then falls, and the next trigger is weaker),
    /// and dishes that "still don't feel clean". Anxiety drives the heartbeat and narrows the vision.
    /// </summary>
    public class OcdBehaviours : MonoBehaviour
    {
        private MindState _ms;
        private bool _active;

        private float _sensitivity = 1f;
        private bool _handsDirty;
        private float _calmUntil;

        private readonly List<Rigidbody> _grabbables = new List<Rigidbody>();
        private readonly HashSet<Rigidbody> _heldLastFrame = new HashSet<Rigidbody>();
        private readonly HashSet<Rigidbody> _smudged = new HashSet<Rigidbody>();

        private class Check
        {
            public string Name;
            public Vector3 Position;
            public GameObject GlowTarget;
            public Hotspot Hotspot;
            public string[] Doubts;
            public string Relief;
            public float CertainUntil;
            public bool Doubting;
            public bool Armed;      // a reason to doubt exists (you were near it, or used it)
            public float NextWhisper;
            public int Escalation;
        }

        private readonly List<Check> _checks = new List<Check>();
        private Check _stove, _door, _washerDoor;

        private class Crooked
        {
            public Rigidbody Body;
            public float AlignedYaw;
            public int Relapses;
            public bool Fixed;
        }

        private readonly List<Crooked> _crooked = new List<Crooked>();

        private bool _calming;
        private float _calmReadyAt;
        private float _resistTime;
        private bool _resisting;
        private float _nextBeat;
        private Hotspot _tap;

        private void Start()
        {
            _ms = MindState.Instance;
            MindState.MindChosen += OnChosen;
            WasherMachine.Started += OnWasherStarted;
            SinkZone.ExtraWashNeeded = d => _active && d.ExtraWashes > 0;
        }

        private void OnDestroy()
        {
            MindState.MindChosen -= OnChosen;
            WasherMachine.Started -= OnWasherStarted;
            SinkZone.ExtraWashDone -= OnExtraWash;
            SinkZone.ExtraWashNeeded = null;
        }

        private void OnChosen(Mind m)
        {
            if (m != Mind.OCD) return;
            _active = true;
            _ms.Anxiety = 15f;
            _sensitivity = MindState.Intensity; // mild 0.5 · moderate 1 · severe 1.6

            foreach (var g in FindObjectsByType<Grabbable>())
            {
                var rb = g.GetComponent<Rigidbody>();
                if (rb != null) _grabbables.Add(rb);
            }

            // dishes never feel clean the first time
            int extra = MindState.CurrentLevel == Level.Mild ? 0 : MindState.CurrentLevel == Level.Severe ? 2 : 1;
            foreach (var d in FindObjectsByType<Dish>()) d.ExtraWashes = extra;
            SinkZone.ExtraWashDone += OnExtraWash;

            // wash hands under the tap
            _tap = Hotspot.Create("Wash Hands", SinkZone.TapPosition, 0.16f, "Wash your hands", "Washing your hands…", 2.5f,
                WashHands, () => _active && _ms.Running);

            _stove = AddCheck("Stove", Spots.Pos("Stove", new Vector3(-1.9f, 0.86f, 1.9f)), Spots.Object("Stove", "Stove Top"),
                new[] { "Is the stove off?", "What if it's still on?", "Check it. Just once more.", "You didn't really look, did you?" },
                "It's off. …I think it's off.");
            _door = AddCheck("Front Door", Spots.Pos("Front Door", new Vector3(-1.75f, 1.0f, -2.4f)), Spots.Object("Front Door", "Door"),
                new[] { "Did I lock the front door?", "Anyone could walk in.", "Go and check the door." },
                "Locked. Definitely locked. …Definitely?");
            _washerDoor = AddCheck("Washer Door", Spots.Pos("Washer Door", new Vector3(-0.25f, 0.4f, 1.85f)), Spots.Object("Washer Door", "Washer Door Ring"),
                new[] { "Is the washer door really shut?", "What if it floods the kitchen?" },
                "Shut. It's shut.");

            // crooked things that pull at you
            foreach (var name in new[] { "[Grab] Pillow", "[Grab] Phone", "[Grab] Vase", "[Grab] Bottle" })
            {
                foreach (var rb in _grabbables)
                {
                    if (rb.name != name || _crooked.Exists(c => c.Body == rb)) continue;
                    float yaw = rb.rotation.eulerAngles.y;
                    float aligned = Mathf.Round(yaw / 90f) * 90f;
                    _crooked.Add(new Crooked { Body = rb, AlignedYaw = aligned });
                    rb.rotation = Quaternion.Euler(rb.rotation.eulerAngles.x, aligned + (Random.value < 0.5f ? -1 : 1) * Random.Range(14f, 22f), rb.rotation.eulerAngles.z);
                    if (name != "[Grab] Pillow") break; // both pillows, one of each other thing
                }
            }
            _calmUntil = Time.time + 8f;
        }

        private Check AddCheck(string name, Vector3 pos, GameObject glow, string[] doubts, string relief)
        {
            var c = new Check { Name = name, Position = pos, GlowTarget = glow, Doubts = doubts, Relief = relief };
            c.Hotspot = Hotspot.Create("Check " + name, pos, 0.22f, "Check the " + name.ToLower(), $"Checking the {name.ToLower()}…", 1.5f,
                () => DoCheck(c), () => _active && _ms.Running);
            _checks.Add(c);
            return c;
        }

        private void Update()
        {
            if (!_active || _ms == null || !_ms.Running)
            {
                if (MindPost.Instance != null && _active) MindPost.Instance.Narrow = 0;
                return;
            }

            float rise = 0;
            rise += UpdateContamination();
            rise += UpdateChecks();
            rise += UpdateCrooked();

            // the dirt fades from the hands while they are under the tap
            if (_tap != null && _tap.InUse && VirtualHands.Instance != null)
                VirtualHands.Instance.Dirt = Mathf.Min(VirtualHands.Instance.Dirt, 1f - _tap.Progress01);

            // calm down: B twice quickly (desktop F twice) starts a slow breath that lowers the anxiety
            if (MindInput.CalmDoubleTap() && !_calming && Time.time > _calmReadyAt) StartCoroutine(Calm());
            if (_calming) rise = 0;

            // resisting: hold B / F — anxiety peaks, then falls
            if (!_calming && MindInput.ResistHeld() && _ms.Anxiety > 12f)
            {
                _resisting = true;
                _resistTime += Time.deltaTime;
                float change = _resistTime < 3f ? 9f : -14f;
                _ms.Anxiety += change * Time.deltaTime;
                MindHud.Status(_resistTime < 3f ? "Resisting… it gets worse before it gets better" : "Resisting… let it be there. It's passing.");
                rise = 0;
            }
            else if (_resisting)
            {
                if (_resistTime > 4f)
                {
                    _ms.Log.Resists++;
                    _sensitivity = Mathf.Max(0.4f, _sensitivity * 0.8f);
                    MindHud.Say("It passed. It always passes.", 3f);
                    MindAudio.Play2D(MindAudio.Chime, 0.4f);
                    _calmUntil = Time.time + 6f;
                }
                _resisting = false;
                _resistTime = 0;
            }

            if (Time.time < _calmUntil) rise *= 0.2f;
            _ms.Anxiety += rise * _sensitivity * Time.deltaTime;
            _ms.Anxiety -= (rise > 0 || _resisting ? 0 : 1.2f) * Time.deltaTime;
            _ms.Anxiety = Mathf.Clamp(_ms.Anxiety, 5f, 100f);

            if (MindPost.Instance != null)
            {
                MindPost.Instance.Narrow = Mathf.InverseLerp(25f, 100f, _ms.Anxiety); // calm = clear view
                MindPost.Instance.Dirt = _handsDirty ? 1 : 0;
            }
            Heartbeat();

            if (_handsDirty && !_tap.InUse)
                MindHud.Status(MindInput.Desktop ? "Your hands feel dirty · tap: E to wash · hold F to resist" : "Your hands feel dirty · tap: wash · hold B to resist");
        }

        // ---------- contamination + relief loop ----------

        private float UpdateContamination()
        {
            foreach (var rb in _grabbables)
            {
                if (rb == null) continue;
                bool held = GrabState.IsHeld(rb);
                bool wasHeld = _heldLastFrame.Contains(rb);
                if (held && !wasHeld) OnTouched(rb);
                if (held) _heldLastFrame.Add(rb); else _heldLastFrame.Remove(rb);
            }
            return _handsDirty ? 3.5f : 0f;
        }

        private void OnTouched(Rigidbody rb)
        {
            var dish = rb.GetComponent<Dish>();
            var garment = rb.GetComponent<Garment>();
            bool dirtyThing = (dish != null && dish.IsDirty) || (garment != null && garment.Current == Garment.State.Dirty) || _smudged.Contains(rb);
            if (dirtyThing)
            {
                if (VirtualHands.Instance != null) VirtualHands.Instance.Dirt += _smudged.Contains(rb) ? 0.3f : 0.45f;
                if (!_handsDirty)
                {
                    _handsDirty = true;
                    _ms.Log.Contaminations++;
                    _ms.Anxiety += 12f * _sensitivity;
                    MindHud.Say(_smudged.Contains(rb) ? "I touched that with dirty hands before. Now it's on me again." : "It's on my hands now. I can feel it.", 3f);
                    MindInput.Buzz(0.3f, 0.15f, true, true);
                }
                return;
            }
            if (_handsDirty && !_smudged.Contains(rb))
            {
                Smudge(rb);
                _ms.Anxiety += 6f * _sensitivity;
                MindHud.Say("Now that's dirty too.", 2f);
            }
        }

        /// <summary>A small brown fingerprint on the object, so you can see where the "dirt" went.</summary>
        private void Smudge(Rigidbody rb)
        {
            _smudged.Add(rb);
            var r = rb.GetComponent<Renderer>();
            if (r == null) return;
            var b = r.bounds;
            var mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(mark.GetComponent<Collider>());
            mark.name = "Smudge";
            mark.transform.SetParent(rb.transform, true);
            mark.transform.position = b.center + Vector3.up * (b.extents.y * 0.95f);
            mark.transform.localScale = Vector3.one * 0.035f;
            mark.transform.localScale = new Vector3(0.035f / rb.transform.lossyScale.x, 0.008f / rb.transform.lossyScale.y, 0.03f / rb.transform.lossyScale.z);
            var m = new Material(r.sharedMaterial) { color = new Color(0.36f, 0.27f, 0.17f) };
            mark.GetComponent<Renderer>().sharedMaterial = m;
        }

        private void WashHands()
        {
            SinkZone.ShowWater(0.2f);
            MindAudio.PlayAt(MindAudio.Water, _tap.transform.position, 0.7f);
            _handsDirty = false;
            if (VirtualHands.Instance != null) VirtualHands.Instance.Dirt = 0;
            _ms.Log.HandWashes++;
            _ms.Log.WashMinutes += 5f;
            _ms.AddMinutes(5f);
            _ms.Anxiety = Mathf.Max(5f, _ms.Anxiety - 40f);
            _calmUntil = Time.time + 6f;
            // relief, but the next trigger comes faster
            _sensitivity = Mathf.Min(2.2f, _sensitivity * 1.2f);
            MindHud.Say(_ms.Log.HandWashes == 1 ? "Better. …For now." : "Clean. Was it long enough, though?", 3f);
        }

        private void OnExtraWash(Dish d)
        {
            _ms.Log.ExtraDishWashes++;
            _ms.AddMinutes(2f);
            _ms.Anxiety += 5f * _sensitivity;
            MindHud.Say("It still doesn't feel clean. Again.", 2.5f);
        }

        // ---------- checking ----------

        private void OnWasherStarted(WasherMachine w)
        {
            if (_washerDoor != null) _washerDoor.Armed = true;
        }

        private float UpdateChecks()
        {
            float rise = 0;
            var head = MindInput.FlatHead();
            foreach (var c in _checks)
            {
                float dist = Vector3.Distance(head, new Vector3(c.Position.x, 0, c.Position.z));
                if (c == _stove && dist < 2.0f) c.Armed = true;           // you were in the kitchen
                if (c == _door && _ms.Minute > 25f) c.Armed = true;       // a while into the morning

                if (!c.Doubting && c.Armed && Time.time > c.CertainUntil && dist > 2.8f && Time.time > _calmUntil)
                {
                    c.Doubting = true;
                    c.Escalation = 0;
                    c.NextWhisper = Time.time;
                }
                if (!c.Doubting) continue;

                rise += 4f;
                if (c.GlowTarget != null) Glow.On(c.GlowTarget, new Color(1f, 0.55f, 0.35f), 0.4f);
                if (Time.time >= c.NextWhisper)
                {
                    MindAudio.PlayAt(MindAudio.Whisper, c.Position, 1f, 8f);
                    MindHud.Say(c.Doubts[Mathf.Min(c.Escalation, c.Doubts.Length - 1)], 3f);
                    c.Escalation++;
                    c.NextWhisper = Time.time + 12f / MindState.Intensity;
                }
            }
            return rise;
        }

        private void DoCheck(Check c)
        {
            MindAudio.PlayAt(MindAudio.Tick, c.Position, 1f);
            c.Doubting = false;
            c.CertainUntil = Time.time + 20f / MindState.Intensity;   // certainty fades (mild 40 s · moderate 20 s · severe 12 s)
            _ms.Log.Checks++;
            _ms.Log.CheckMinutes += 3f;
            _ms.AddMinutes(3f);
            _ms.Anxiety = Mathf.Max(5f, _ms.Anxiety - 22f);
            if (c.GlowTarget != null) Glow.Off(c.GlowTarget);
            MindHud.Say(c.Relief, 3f);
        }

        // ---------- just right ----------

        private float UpdateCrooked()
        {
            float rise = 0;
            foreach (var c in _crooked)
            {
                if (c.Body == null || c.Fixed) continue;
                bool inView = MindInput.LookingAt(c.Body.position, 35f, 2.6f);
                if (inView)
                {
                    rise += 1.8f;
                    Glow.On(c.Body.gameObject, new Color(1f, 0.9f, 0.3f), 0.3f);
                }

                bool desktopAim = DesktopFallbackPlayer.AimedBody == c.Body;
                if (desktopAim) MindHud.Status("It's crooked. It has to be straight.  G: straighten it");
                bool vrHand = MindInput.HandNear(c.Body.position, 0.2f) && !GrabState.IsHeld(c.Body);
                if (!vrHand) _handTime[c] = 0;
                if ((desktopAim && MindInput.StraightenDown()) || (vrHand && HoldHand(c)))
                    StartCoroutine(Straighten(c));
            }
            return rise;
        }

        private readonly Dictionary<Crooked, float> _handTime = new Dictionary<Crooked, float>();

        private bool HoldHand(Crooked c)
        {
            _handTime.TryGetValue(c, out var t);
            t += Time.deltaTime;
            _handTime[c] = t;
            MindHud.Status($"Straightening…  {Mathf.RoundToInt(Mathf.Clamp01(t / 1.2f) * 100)}%");
            if (t < 1.2f) return false;
            _handTime[c] = -2f;
            return true;
        }

        private IEnumerator Straighten(Crooked c)
        {
            c.Fixed = true;
            var rb = c.Body;
            bool kin = rb.isKinematic;
            rb.isKinematic = true;
            var e = rb.rotation.eulerAngles;
            var from = rb.rotation;
            var to = Quaternion.Euler(0, c.AlignedYaw, 0);
            for (float t = 0; t < 1; t += Time.deltaTime * 2.5f)
            {
                rb.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            rb.transform.rotation = to;
            rb.isKinematic = kin;
            Glow.Off(rb.gameObject);
            _ms.Log.Aligns++;
            _ms.Log.AlignMinutes += 2f;
            _ms.AddMinutes(2f);
            _ms.Anxiety = Mathf.Max(5f, _ms.Anxiety - 12f);
            MindAudio.PlayAt(MindAudio.Tick, rb.position, 0.8f);

            // fixing restarts the loop: sometimes it's "not quite right"
            if (c.Relapses < 2 && Random.value < 0.4f * MindState.Intensity)
            {
                MindHud.Say("There. …No. Not quite right.", 2.5f);
                yield return new WaitForSeconds(2.5f);
                if (rb == null) yield break;
                c.Relapses++;
                rb.transform.rotation = Quaternion.Euler(0, c.AlignedYaw + Random.Range(6f, 10f) * (Random.value < 0.5f ? -1 : 1), 0);
                c.Fixed = false;
            }
            else
            {
                MindHud.Say("Just right.", 2f);
            }
        }

        // ---------- calm down (double B): two slow breaths ----------

        private IEnumerator Calm()
        {
            _calming = true;
            _ms.Log.Calms++;
            float start = _ms.Anxiety;
            float target = Mathf.Max(5f, start * 0.35f);
            const float breath = 8f; // 4 s in, 4 s out
            for (int b = 0; b < 2; b++)
            {
                MindAudio.Play2D(MindAudio.Breath, 0.5f);
                for (float t = 0; t < breath; t += Time.deltaTime)
                {
                    float total = (b * breath + t) / (2 * breath);
                    _ms.Anxiety = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, total));
                    // a soft pulse that follows the breathing rhythm
                    if (Time.frameCount % 30 == 0) MindInput.Buzz(0.08f + 0.12f * Mathf.Sin(t / breath * Mathf.PI), 0.05f, true, true);
                    yield return null;
                }
            }
            MindAudio.Play2D(MindAudio.Chime, 0.35f);
            _calmUntil = Time.time + 10f;
            _calmReadyAt = Time.time + 20f;
            _calming = false;
        }

        // ---------- heartbeat: speed = anxiety ----------

        private void Heartbeat()
        {
            if (Time.time < _nextBeat) return;
            float a = _ms.Anxiety / 100f;
            _nextBeat = Time.time + Mathf.Lerp(1.15f, 0.42f, a);
            if (a < 0.18f) return;
            MindAudio.Play2D(MindAudio.Heartbeat, Mathf.Lerp(0.15f, 0.85f, a));
            MindInput.Buzz(Mathf.Lerp(0.15f, 0.7f, a), 0.07f, true, true);
        }
    }
}
