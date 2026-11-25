using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// VS1 · ADHD — "tasks don't fail because the person refuses; they fail through drift".
    /// Slow start, zone-outs (blur + grey, clock ×4, grabbing off until you press A / R three times),
    /// task pull (another chore glows and calls), phone pull (buzz → look → 15–30 min gone),
    /// and out of sight (the washer beep is only spatial; laundry forgotten for 50 min must be rewashed).
    /// </summary>
    public class AdhdBehaviours : MonoBehaviour
    {
        // mild 2 · moderate 3 · severe 5 presses to snap back
        private static int RefocusPresses => MindState.CurrentLevel == Level.Mild ? 2 : MindState.CurrentLevel == Level.Severe ? 5 : 3;
        private static float K => MindState.Intensity;

        private MindState _ms;
        private bool _active;

        // slow start
        private readonly Dictionary<Rigidbody, float> _effortNeeded = new Dictionary<Rigidbody, float>();
        private readonly Dictionary<Rigidbody, float> _effort = new Dictionary<Rigidbody, float>();
        private bool _firstStart = true;

        // zone-out
        private bool _zoned;
        private int _presses;
        private float _nextZoneCheck, _zoneCooldownUntil, _zoneStartMinute;

        // task pull
        private float _nextPull;
        private GameObject _pullTarget;
        private float _pullUntil;
        private bool _pullFollowed;

        // phone pull
        private Rigidbody _phone;
        private float _nextBuzz, _buzzUntil, _lookTime;
        private bool _scrolling;

        // laundry out of sight
        private readonly List<(List<Garment> batch, float doneMinute)> _washed = new List<(List<Garment>, float)>();

        private int _lastProgress;

        private static readonly string[] ZoneLines =
        {
            "…", "Wait. What was I doing?", "I should really… hm.", "Where did the last ten minutes go?",
            "Just a second. My head is somewhere else.", "Everything feels far away.",
        };

        private static readonly string[] StartLines =
        {
            "Ugh. So boring. Maybe in a minute.", "I'll do it. I just… can't start.", "Come on. Just pick it up.",
        };

        private void Start()
        {
            _ms = MindState.Instance;
            MindState.MindChosen += OnChosen;
            WasherMachine.Finished += OnWasherFinished;
        }

        private void OnDestroy()
        {
            MindState.MindChosen -= OnChosen;
            WasherMachine.Finished -= OnWasherFinished;
        }

        private void OnChosen(Mind m)
        {
            if (m != Mind.ADHD) return;
            _active = true;
            _ms.Interest = 65;
            _nextZoneCheck = Time.time + 25f / K;
            _nextPull = Time.time + 40f / K;
            _nextBuzz = Time.time + 55f / K;
            foreach (var d in FindObjectsByType<Dish>())
            {
                if (d.Body == null) continue;
                _effortNeeded[d.Body] = (_firstStart ? 4f : Random.Range(1.5f, 2.5f)) * K;
                _firstStart = false;
                GrabLock.Lock(d.Body, true);
            }
            foreach (var g in FindObjectsByType<Grabbable>())
                if (g.name.Contains("Phone")) _phone = g.GetComponent<Rigidbody>();
            _lastProgress = ChoreBoard.Progress.Points;
        }

        private void Update()
        {
            if (!_active || _ms == null || !_ms.Running) return;

            UpdateInterest();
            UpdateSlowStart();
            UpdateZoneOut();
            if (!_zoned)
            {
                UpdateTaskPull();
                UpdatePhone();
            }
            UpdateForgottenLaundry();
        }

        // ---------- interest ----------

        private void UpdateInterest()
        {
            bool boring = SinkZone.WashingNow || HoldingDirtyDish();
            _ms.Interest -= Time.deltaTime * (boring ? 1.6f : 0.5f) * K;
            int points = ChoreBoard.Progress.Points;
            if (points > _lastProgress) _ms.Interest += 10 * (points - _lastProgress); // a small win
            _lastProgress = points;
            _ms.Interest = Mathf.Clamp(_ms.Interest, 0, 100);
        }

        private static bool HoldingDirtyDish()
        {
            foreach (var d in FindObjectsByType<Dish>())
                if (d.IsDirty && GrabState.IsHeld(d.Body)) return true;
            return false;
        }

        // ---------- slow start: the first grab of a boring task lags ----------

        private void UpdateSlowStart()
        {
            if (_effortNeeded.Count == 0) return;
            Rigidbody done = null;
            foreach (var kv in _effortNeeded)
            {
                var rb = kv.Key;
                if (rb == null) continue;
                bool trying = DesktopFallbackPlayer.AimedBody == rb || MindInput.HandNear(rb.position, 0.3f);
                if (!trying) continue;
                _effort.TryGetValue(rb, out var e);
                if (e == 0) MindHud.Say(StartLines[Random.Range(0, StartLines.Length)], 2.5f);
                e += Time.deltaTime;
                _effort[rb] = e;
                MindHud.Status(MindInput.Desktop ? "Getting started… keep looking at it" : "Getting started… keep your hand there");
                if (e >= kv.Value) { done = rb; break; }
            }
            if (done != null)
            {
                _effortNeeded.Remove(done);
                GrabLock.Lock(done, false);
                MindAudio.PlayAt(MindAudio.Tick, done.position, 0.8f);
                MindHud.Say("Okay. Fine. Doing it.", 1.8f);
            }
        }

        // ---------- zone-out ----------

        private void UpdateZoneOut()
        {
            var post = MindPost.Instance;
            if (_zoned)
            {
                MindHud.Status(MindInput.Desktop
                    ? $"Zoned out. Press R to snap back  ({_presses}/{RefocusPresses})"
                    : $"Zoned out. Press A to snap back  ({_presses}/{RefocusPresses})");
                if (MindInput.RefocusDown())
                {
                    _presses++;
                    MindInput.Buzz(0.4f, 0.08f, false, true);
                    MindAudio.Play2D(MindAudio.Tick, 0.8f);
                    if (_presses >= RefocusPresses) EndZoneOut();
                }
                return;
            }
            if (post != null) post.ZoneOut = 0;

            if (Time.time < _nextZoneCheck || Time.time < _zoneCooldownUntil || _scrolling) return;
            _nextZoneCheck = Time.time + 1f;
            float chance = (0.012f + (1 - _ms.Interest / 100f) * 0.07f) * K;
            if (Random.value < chance) StartZoneOut();
        }

        private void StartZoneOut()
        {
            _zoned = true;
            _presses = 0;
            _zoneStartMinute = _ms.Minute;
            _ms.ClockSpeed = 4f;
            _ms.Log.ZoneOuts++;
            GrabLock.LockAll(true);
            if (MindPost.Instance != null) MindPost.Instance.ZoneOut = 1;
            MindHud.Say(ZoneLines[Random.Range(0, ZoneLines.Length)], 4f);
        }

        private void EndZoneOut()
        {
            _zoned = false;
            _ms.ClockSpeed = 1f;
            _ms.Log.ZoneOutMinutes += _ms.Minute - _zoneStartMinute;
            _ms.Interest = Mathf.Min(100, _ms.Interest + 30);
            _zoneCooldownUntil = Time.time + 25f / K;
            GrabLock.LockAll(false);
            if (MindPost.Instance != null) MindPost.Instance.ZoneOut = 0;
            MindHud.Say($"Oh. It's {_ms.Now} already?", 3f);
        }

        // ---------- task pull: another chore glows and calls you away ----------

        private void UpdateTaskPull()
        {
            if (_pullTarget != null)
            {
                if (!_pullFollowed && Vector3.Distance(MindInput.FlatHead(), Flat(_pullTarget.transform.position)) < 1.4f)
                {
                    _pullFollowed = true;
                    _ms.Log.TaskPulls++;
                }
                if (Time.time > _pullUntil) { Glow.Off(_pullTarget); _pullTarget = null; }
                return;
            }
            if (Time.time < _nextPull) return;
            _nextPull = Time.time + Random.Range(35f, 55f) / K;

            var options = new List<(GameObject go, string line)>();
            var p = ChoreBoard.Progress;
            var bed = FindAnyObjectByType<BedMaker>();
            if (bed != null && !bed.IsMade) options.Add((bed.gameObject, "Oh, the bed. I should make the bed first."));
            var washer = GameObject.Find("Washing Machine");
            if (washer != null && p.Washed < p.ClothesTotal) options.Add((washer, "The laundry! Let me just start the laundry."));
            var rack = GameObject.Find("Drying Rack");
            if (rack != null && p.Washed > p.Hung) options.Add((rack, "The wet clothes… I should hang them."));
            var floor = FloorChore.Instance;
            if (floor != null && floor.Stains.Count > 0) options.Add((floor.Stains[0].gameObject, "That stain is so annoying. I'll just clean it now."));
            foreach (var d in FindObjectsByType<Dish>())
                if (d.IsDirty && !GrabState.IsHeld(d.Body)) { options.Add((d.gameObject, "More dishes over there. Ugh.")); break; }
            if (options.Count == 0) return;

            var pick = options[Random.Range(0, options.Count)];
            _pullTarget = pick.go;
            _pullUntil = Time.time + 9f;
            _pullFollowed = false;
            Glow.On(_pullTarget, new Color(1f, 0.85f, 0.4f), 9f);
            MindAudio.PlayAt(MindAudio.Whisper, _pullTarget.transform.position, 0.8f);
            MindHud.Say(pick.line, 3.5f);
            _ms.Interest = Mathf.Max(0, _ms.Interest - 8);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // ---------- phone pull ----------

        private void UpdatePhone()
        {
            if (_phone == null || _scrolling) return;
            if (Time.time >= _nextBuzz && Time.time > _buzzUntil)
            {
                _buzzUntil = Time.time + 12f;
                _nextBuzz = Time.time + Random.Range(70f, 110f) / K;
                _lookTime = 0;
                StartCoroutine(BuzzPattern());
            }
            if (Time.time > _buzzUntil) return;

            Glow.On(_phone.gameObject, new Color(0.7f, 0.85f, 1f), 0.3f);
            bool looking = MindInput.LookingAt(_phone.position, 14f, 4f);
            _lookTime = looking ? _lookTime + Time.deltaTime : 0;
            if (_lookTime > 1.5f || GrabState.IsHeld(_phone)) StartCoroutine(Scroll());
        }

        private IEnumerator BuzzPattern()
        {
            for (int i = 0; i < 3 && Time.time < _buzzUntil && !_scrolling; i++)
            {
                MindAudio.PlayAt(MindAudio.PhoneBuzz, _phone.position, 0.9f, 7f);
                MindInput.Buzz(0.6f, 0.5f, true, false); // the phone "in your pocket": left controller
                yield return new WaitForSeconds(3.5f);
            }
        }

        private IEnumerator Scroll()
        {
            _scrolling = true;
            _buzzUntil = 0;
            DesktopFallbackPlayer.ForceDrop();
            _ms.Log.PhonePickups++;
            MindHud.Say("Just one quick look…", 2.5f);
            bool wasKinematic = _phone.isKinematic;
            _phone.isKinematic = true;
            GrabLock.LockAll(true);

            float minutes = Random.Range(15f, 30f) * Mathf.Lerp(0.7f, 1f, K / 1.6f);
            float real = 5f;
            float startMinute = _ms.Minute;
            for (float t = 0; t < real; t += Time.deltaTime)
            {
                var head = MindInput.Head;
                if (head != null)
                {
                    var target = head.position + head.forward * 0.35f - head.up * 0.08f;
                    _phone.transform.SetPositionAndRotation(target, Quaternion.LookRotation(head.up, -head.forward));
                }
                Glow.On(_phone.gameObject, new Color(0.7f, 0.85f, 1f), 0.3f);
                _ms.AddMinutes(minutes / real * Time.deltaTime);
                MindHud.Status($"scrolling…  {_ms.Now}");
                yield return null;
            }
            _ms.Log.PhoneMinutes += _ms.Minute - startMinute;
            _ms.Interest = Mathf.Min(100, _ms.Interest + 25);

            var h = MindInput.Head;
            if (h != null) _phone.transform.position = h.position + h.forward * 0.4f - Vector3.up * 0.3f;
            _phone.isKinematic = wasKinematic;
            GrabLock.LockAll(false);
            _scrolling = false;
            MindHud.Say($"Wait… {_ms.Now}? Where did that go?", 3.5f);
        }

        // ---------- out of sight: forgotten laundry ----------

        private void OnWasherFinished(WasherMachine washer, List<Garment> batch)
        {
            if (_active) _washed.Add((new List<Garment>(batch), _ms.Minute));
        }

        private void UpdateForgottenLaundry()
        {
            for (int i = _washed.Count - 1; i >= 0; i--)
            {
                var (batch, doneMinute) = _washed[i];
                if (_ms.Minute - doneMinute < 50f) continue;
                _washed.RemoveAt(i);
                int musty = 0;
                foreach (var g in batch)
                {
                    if (g == null || g.IsHung || g.WasHung || g.Current != Garment.State.Wet) continue;
                    g.SetState(Garment.State.Dirty);
                    musty++;
                }
                if (musty == 0) continue;
                _ms.Log.Rewashes++;
                MindHud.Say("Oh no. The laundry. It's been sitting there for ages… it smells. It needs washing again.", 5f);
            }
        }
    }
}
