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

        private void Awake() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            var g = rb != null ? rb.GetComponent<Garment>() : null;
            if (g == null || g.IsHung || g.Current == Garment.State.Dirty || GrabState.IsHeld(rb)) return;

            foreach (var s in _slots)
            {
                if (_used.Contains(s)) continue;
                _used.Add(s);
                g.Hang(s, this);
                return;
            }
        }

        public void Release(Transform slot) => _used.Remove(slot);
    }
}
