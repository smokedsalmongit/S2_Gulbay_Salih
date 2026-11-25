using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>A piece of laundry: dirty → washed (wet) in the washing machine → hung on the rack → dry.</summary>
    public class Garment : MonoBehaviour
    {
        public enum State { Dirty, Wet, Dry }

        [SerializeField] private Material _dirty;
        [SerializeField] private Material _wet;
        [SerializeField] private Material _dry;
        [SerializeField] private State _state = State.Dirty;
        [SerializeField] private float _dryingTime = 10f;

        private Transform _slot;
        private DryingRack _rack;
        private float _hungFor;

        public State Current => _state;
        /// <summary>Colour of the clean piece (used to texture it).</summary>
        public Color DryColor => _dry != null ? _dry.color : Color.white;
        public bool IsHung => _slot != null;
        public bool WasHung { get; private set; }
        public Rigidbody Body { get; private set; }

        public void Setup(Material dirty, Material wet, Material dry)
        {
            _dirty = dirty;
            _wet = wet;
            _dry = dry;
            ApplyLook();
        }

        private void Awake() => Body = GetComponent<Rigidbody>();

        private void Start()
        {
            ApplyLook();
            ChoreBoard.Changed();
        }

        public void SetState(State s)
        {
            _state = s;
            ApplyLook();
            ChoreBoard.Changed();
        }

        public void Hang(Transform slot, DryingRack rack)
        {
            _slot = slot;
            _rack = rack;
            _hungFor = 0;
            WasHung = true;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            transform.SetPositionAndRotation(slot.position, slot.rotation);
            ChoreBoard.Changed();
        }

        private void Update()
        {
            if (_slot == null) return;

            // taken off the rack again
            if (GrabState.IsHeld(Body) || Vector3.Distance(transform.position, _slot.position) > 0.15f)
            {
                if (GrabState.IsHeld(Body)) return; // wait until it is let go
                _rack.Release(_slot);
                _slot = null;
                Body.isKinematic = false;
                ChoreBoard.Changed();
                return;
            }

            if (_state == State.Wet)
            {
                _hungFor += Time.deltaTime;
                if (_hungFor >= _dryingTime) SetState(State.Dry);
            }
        }

        private void ApplyLook()
        {
            var r = GetComponent<Renderer>();
            var m = _state == State.Dirty ? _dirty : _state == State.Wet ? _wet : _dry;
            if (r != null && m != null) r.sharedMaterial = m;
        }
    }
}
