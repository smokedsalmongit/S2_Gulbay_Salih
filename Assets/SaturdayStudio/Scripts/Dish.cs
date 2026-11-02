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
        public Rigidbody Body { get; private set; }

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
