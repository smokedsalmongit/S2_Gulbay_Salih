using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// Puts a grabbable back where it started if it is thrown out of the room or falls through the floor.
    /// </summary>
    public class RespawnIfFallen : MonoBehaviour
    {
        [SerializeField] private float _minY = -2f;
        [SerializeField] private float _maxDistance = 25f;

        private Vector3 _startPos;
        private Quaternion _startRot;
        private Rigidbody _rb;

        private bool _hasStart;

        /// <summary>Use the current place as the respawn point (for props moved at runtime).</summary>
        public void ResetStart()
        {
            _startPos = transform.position;
            _startRot = transform.rotation;
            _hasStart = true;
        }

        private void Start()
        {
            if (!_hasStart) ResetStart();
            _rb = GetComponent<Rigidbody>();
            _hasStart = true;
        }

        private void Update()
        {
            if (transform.position.y > _minY && transform.position.magnitude < _maxDistance) return;

            if (_rb != null && !_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            transform.SetPositionAndRotation(_startPos, _startRot);
        }
    }
}
