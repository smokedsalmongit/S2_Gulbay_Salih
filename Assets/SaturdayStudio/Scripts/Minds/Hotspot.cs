using System;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// A place you "use" with your hands: wash hands under the tap, check the stove, the door, the washer.
    /// VR: hold a hand there for a moment (progress shows in front of you). Desktop: aim at it and press E / click.
    /// </summary>
    public class Hotspot : MonoBehaviour, IActivatable
    {
        private string _label;
        private string _doing;
        private float _dwell;
        private float _radius;
        private Action _onActivate;
        private Func<bool> _canActivate;
        private Transform _follow;
        private Vector3 _followOffset;

        private float _progress;
        private bool _waitForLeave;

        public float Progress01 => _dwell > 0 ? _progress / _dwell : 0;
        public bool InUse { get; private set; }

        public string Label => _label;
        public bool CanActivate => _canActivate == null || _canActivate();

        public void Activate()
        {
            if (!CanActivate) return;
            _onActivate?.Invoke();
        }

        public static Hotspot Create(string name, Vector3 position, float radius, string label, string doing, float dwell,
            Action onActivate, Func<bool> canActivate, Transform follow = null)
        {
            var go = new GameObject("Hotspot · " + name);
            go.transform.position = position;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var h = go.AddComponent<Hotspot>();
            h._label = label;
            h._doing = doing;
            h._dwell = dwell;
            h._radius = radius;
            h._onActivate = onActivate;
            h._canActivate = canActivate;
            h._follow = follow;
            if (follow != null) h._followOffset = position - follow.position;
            go.AddComponent<TaskClickable>().SetTarget(h); // desktop E / click
            return h;
        }

        private void Update()
        {
            if (_follow != null) transform.position = _follow.position + _followOffset;

            bool near = MindInput.HandNear(transform.position, _radius + 0.04f);
            InUse = near && !_waitForLeave && CanActivate;
            if (InUse)
            {
                _progress += Time.deltaTime;
                if (Time.frameCount % 18 == 0) MindInput.Buzz(0.15f, 0.03f, true, true); // felt, not read
                MindHud.Status($"{_doing}  {Mathf.RoundToInt(Progress01 * 100)}%");
                if (_progress >= _dwell)
                {
                    _progress = 0;
                    _waitForLeave = true;
                    MindAudio.PlayAt(MindAudio.Chime, transform.position, 0.35f);
                    Activate();
                }
            }
            else
            {
                _progress = Mathf.Max(0, _progress - Time.deltaTime * 2);
            }
            if (!near) _waitForLeave = false;
        }
    }
}
