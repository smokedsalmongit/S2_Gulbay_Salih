using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>Trigger inside the sink basin: hold or drop a dirty dish here and the tap runs until it is clean.</summary>
    [RequireComponent(typeof(Collider))]
    public class SinkZone : MonoBehaviour
    {
        [SerializeField] private GameObject _water;
        [SerializeField] private float _washTime = 1.5f;

        private readonly Dictionary<Dish, float> _progress = new Dictionary<Dish, float>();
        private float _lastWashing = -10;

        private void Awake() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerStay(Collider other)
        {
            var dish = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Dish>() : null;
            if (dish == null || !dish.IsDirty) return;

            _lastWashing = Time.time;
            _progress.TryGetValue(dish, out var t);
            t += Time.fixedDeltaTime;
            _progress[dish] = t;
            if (t < _washTime) return;

            _progress.Remove(dish);
            dish.Clean();
            StartCoroutine(Pop(dish.transform));
        }

        private void Update()
        {
            if (_water != null) _water.SetActive(Time.time - _lastWashing < 0.25f);
        }

        // a small "sparkle" bounce so the change is easy to notice
        private static IEnumerator Pop(Transform t)
        {
            var baseScale = t.localScale;
            for (float a = 0; a < 1; a += Time.deltaTime * 4)
            {
                if (t == null) yield break;
                t.localScale = baseScale * (1 + 0.15f * Mathf.Sin(a * Mathf.PI));
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }
    }
}
