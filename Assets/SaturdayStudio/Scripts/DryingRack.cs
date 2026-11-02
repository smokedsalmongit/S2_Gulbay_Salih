using System.Collections.Generic;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>Trigger around the drying rack: let go of a wet piece of clothing near it and it hangs itself on a free bar.</summary>
    [RequireComponent(typeof(Collider))]
    public class DryingRack : MonoBehaviour
    {
        [SerializeField] private Transform[] _slots;

        private readonly HashSet<Transform> _used = new HashSet<Transform>();

        public void SetSlots(Transform[] slots) => _slots = slots;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            // let the zone reach the floor, so a piece that falls next to the rack still gets hung
            if (GetComponent<Collider>() is BoxCollider box)
            {
                float bottom = transform.position.y + box.center.y - box.size.y / 2;
                if (bottom > 0)
                {
                    box.size += Vector3.up * bottom;
                    box.center -= Vector3.up * (bottom / 2);
                }
            }
        }

        public bool Accepts(Garment g) => g != null && !g.IsHung && g.Current != Garment.State.Dirty && HasFreeSlot;

        public bool HasFreeSlot => _used.Count < _slots.Length;

        public bool IsNear(Vector3 p, float margin = 0.7f)
        {
            var c = GetComponent<Collider>();
            return Vector3.Distance(c.ClosestPoint(p), p) <= margin;
        }

        private void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            var g = rb != null ? rb.GetComponent<Garment>() : null;
            if (g == null || GrabState.IsHeld(rb)) return;
            TryHang(g);
        }

        public bool TryHang(Garment g)
        {
            if (!Accepts(g)) return false;
            foreach (var s in _slots)
            {
                if (_used.Contains(s)) continue;
                _used.Add(s);
                g.Hang(s, this);
                return true;
            }
            return false;
        }

        public void Release(Transform slot) => _used.Remove(slot);
    }
}
