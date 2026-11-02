using System.Collections;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>Select the bed (VR: point the ray and press the trigger or pinch; desktop: E / click) and the blanket smooths itself out.</summary>
    public class BedMaker : MonoBehaviour, IActivatable
    {
        [SerializeField] private Transform _blanket;
        [SerializeField] private Transform _fold;
        [SerializeField] private Vector3 _madePosition;
        [SerializeField] private Vector3 _madeEuler;
        [SerializeField] private Vector3 _madeScale = Vector3.one;

        public bool IsMade { get; private set; }
        public string Label => IsMade ? "Bed (made)" : "Bed · make it";
        public bool CanActivate => !IsMade;

        public void Setup(Transform blanket, Transform fold, Vector3 madePos, Vector3 madeEuler, Vector3 madeScale)
        {
            _blanket = blanket;
            _fold = fold;
            _madePosition = madePos;
            _madeEuler = madeEuler;
            _madeScale = madeScale;
        }

        public void Activate()
        {
            if (IsMade) return;
            IsMade = true;
            StartCoroutine(Smooth());
            ChoreBoard.Changed();
        }

        private IEnumerator Smooth()
        {
            var p0 = _blanket.position;
            var r0 = _blanket.rotation;
            var s0 = _blanket.localScale;
            var r1 = Quaternion.Euler(_madeEuler);
            var f0 = _fold != null ? _fold.localScale : Vector3.zero;
            for (float t = 0; t < 1; t += Time.deltaTime / 1.2f)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                _blanket.SetPositionAndRotation(Vector3.Lerp(p0, _madePosition, e), Quaternion.Slerp(r0, r1, e));
                _blanket.localScale = Vector3.Lerp(s0, _madeScale, e);
                if (_fold != null) _fold.localScale = Vector3.Lerp(f0, Vector3.zero, e);
                yield return null;
            }
            _blanket.SetPositionAndRotation(_madePosition, r1);
            _blanket.localScale = _madeScale;
            if (_fold != null) _fold.gameObject.SetActive(false);
        }
    }
}
