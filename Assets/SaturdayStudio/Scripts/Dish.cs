using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>A plate, bowl, mug or pan that starts dirty and gets clean in the sink.</summary>
    public class Dish : MonoBehaviour
    {
        [SerializeField] private Material _dirty;
        [SerializeField] private Material _clean;
        [SerializeField] private bool _isDirty = true;

        public bool IsDirty => _isDirty;
        /// <summary>VS2: how many more times it has to be washed before it "feels" clean.</summary>
        public int ExtraWashes { get; set; }
        public Rigidbody Body { get; private set; }

        private Transform _slot;
        private DishRack _rack;
        /// <summary>Standing in the drying rack next to the sink.</summary>
        public bool IsRacked => _slot != null;

        public void PutInRack(Transform slot, DishRack rack)
        {
            _slot = slot;
            _rack = rack;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            transform.SetPositionAndRotation(slot.position, slot.rotation);
            ChoreBoard.Changed();
        }

        private void Update()
        {
            if (_slot == null) return;
            bool held = GrabState.IsHeld(Body);
            if (!held && Vector3.Distance(transform.position, _slot.position) < 0.1f) return;
            if (held) return; // wait until it is let go
            _rack.Release(_slot);
            _slot = null;
            Body.isKinematic = false;
            ChoreBoard.Changed();
        }

        public void Setup(Material dirty, Material clean)
        {
            _dirty = dirty;
            _clean = clean;
            ApplyLook();
        }

        private void Awake() => Body = GetComponent<Rigidbody>();

        private void Start()
        {
            ApplyLook();
            ChoreBoard.Changed();
        }

        public void Clean()
        {
            if (!_isDirty) return;
            _isDirty = false;
            ApplyLook();
            ChoreBoard.Changed();
        }

        private void ApplyLook()
        {
            var r = GetComponent<Renderer>();
            var m = _isDirty ? _dirty : _clean;
            if (r != null && m != null) r.sharedMaterial = m;
        }
    }
}
