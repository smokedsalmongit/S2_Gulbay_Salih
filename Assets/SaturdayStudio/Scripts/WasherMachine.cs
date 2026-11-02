using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// Trigger in front of the washer door. Drop dirty clothes in; a few seconds after the last one the
    /// machine starts by itself, spins, and puts the clothes back out, washed and wet, on top of the machine.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WasherMachine : MonoBehaviour
    {
        private enum Mode { Idle, Washing, Done }

        [SerializeField] private Transform _drum;
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _output;
        [SerializeField] private TextMeshPro _display;
        [SerializeField] private Renderer _drumRenderer;
        [SerializeField] private Material _drumIdle;
        [SerializeField] private Material _drumWashing;
        [SerializeField] private float _startDelay = 3f;
        [SerializeField] private float _washDuration = 12f;

        private readonly List<Garment> _load = new List<Garment>();
        private Mode _mode = Mode.Idle;
        private float _timer;
        private Vector3 _bodyPos;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (_body != null) _bodyPos = _body.localPosition;
        }

        private void Start() => Refresh();

        private void OnTriggerStay(Collider other)
        {
            if (_mode == Mode.Washing) return;
            var rb = other.attachedRigidbody;
            var g = rb != null ? rb.GetComponent<Garment>() : null;
            if (g == null || GrabState.IsHeld(rb)) return;
            TryLoad(g);
        }

        /// <summary>Can this piece go in right now (dirty, machine not running)?</summary>
        public bool Accepts(Garment g) => g != null && _mode != Mode.Washing && g.Current == Garment.State.Dirty && !_load.Contains(g);

        /// <summary>Is a point close enough to the washer door to count as "putting it in"?</summary>
        public bool IsNear(Vector3 p, float margin = 0.6f)
        {
            var c = GetComponent<Collider>();
            return Vector3.Distance(c.ClosestPoint(p), p) <= margin;
        }

        public bool TryLoad(Garment g)
        {
            if (!Accepts(g)) return false;
            _load.Add(g);
            g.gameObject.SetActive(false);
            _mode = Mode.Idle;
            _timer = _startDelay;
            Refresh();
            return true;
        }

        private void Update()
        {
            if (_mode == Mode.Idle && _load.Count > 0)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0)
                {
                    _mode = Mode.Washing;
                    _timer = _washDuration;
                    if (_drumRenderer != null) _drumRenderer.sharedMaterial = _drumWashing;
                }
                Refresh();
            }
            else if (_mode == Mode.Washing)
            {
                _timer -= Time.deltaTime;
                if (_drum != null) _drum.Rotate(0, 720 * Time.deltaTime, 0, Space.Self);
                if (_body != null) _body.localPosition = _bodyPos + Random.insideUnitSphere * 0.004f;
                if (_timer <= 0) Finish();
                Refresh();
            }
        }

        private void Finish()
        {
            _mode = Mode.Done;
            if (_body != null) _body.localPosition = _bodyPos;
            if (_drumRenderer != null) _drumRenderer.sharedMaterial = _drumIdle;
            // spread the washed pieces over the top of the machine (2 x 2, then a second layer)
            for (int i = 0; i < _load.Count; i++)
            {
                var g = _load[i];
                int cell = i % 4, layer = i / 4;
                var offset = new Vector3((cell % 2 - 0.5f) * 0.28f, 0.01f + layer * 0.07f, (cell / 2 - 0.5f) * 0.26f);
                g.transform.SetPositionAndRotation(_output.position + offset, _output.rotation);
                g.gameObject.SetActive(true);
                g.Body.isKinematic = false;
                g.Body.WakeUp();
                g.Body.linearVelocity = Vector3.zero;
                g.Body.angularVelocity = Vector3.zero;
                g.SetState(Garment.State.Wet);
            }
            _load.Clear();
            Refresh();
        }

        private void Refresh()
        {
            if (_display == null) return;
            _display.text = _mode switch
            {
                Mode.Washing => $"WASHING  {Mathf.CeilToInt(_timer)}s",
                Mode.Done => "DONE! Wet clothes are on top",
                _ when _load.Count > 0 => $"{_load.Count} in · starts in {Mathf.CeilToInt(_timer)}s",
                _ => "Drop dirty clothes in here",
            };
        }
    }
}
