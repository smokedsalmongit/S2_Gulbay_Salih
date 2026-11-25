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

        /// <summary>Set by VS2: does this dish need another wash before it counts as clean?</summary>
        public static System.Func<Dish, bool> ExtraWashNeeded;
        public static event System.Action<Dish> ExtraWashDone;
        /// <summary>True while a dish is being washed (VS1 finds this boring).</summary>
        public static bool WashingNow => Time.time - _lastWashingStatic < 0.3f;
        private static float _lastWashingStatic = -10;
        private static float _forceWaterUntil = -10;

        /// <summary>Run the tap for a while (washing hands).</summary>
        public static void ShowWater(float seconds) => _forceWaterUntil = Time.time + seconds;

        private readonly Dictionary<Dish, float> _progress = new Dictionary<Dish, float>();
        private float _lastWashing = -10;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            _tap = null; // look the tap up again in this scene
        }

        private void OnTriggerStay(Collider other)
        {
            var dish = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<Dish>() : null;
            if (dish == null || !dish.IsDirty) return;

            _lastWashing = Time.time;
            _lastWashingStatic = Time.time;
            _progress.TryGetValue(dish, out var t);
            t += Time.fixedDeltaTime;
            _progress[dish] = t;
            if (t < _washTime) return;

            _progress.Remove(dish);
            if (ExtraWashNeeded != null && ExtraWashNeeded(dish))
            {
                dish.ExtraWashes--;
                ExtraWashDone?.Invoke(dish);
                StartCoroutine(Pop(dish.transform));
                return;
            }
            dish.Clean();
            StartCoroutine(Pop(dish.transform));
        }

        private void Update()
        {
            if (_water != null) _water.SetActive(Time.time - _lastWashing < 0.25f || Time.time < _forceWaterUntil
                || HandsUnderTap());
        }

        // VR: water also runs while a hand is under the tap
        private static Vector3? _tap;
        public static Vector3 TapPosition => _tap ??= Spots.Pos("Tap", new Vector3(-1.0f, 1.0f, 2.25f));
        private static bool HandsUnderTap() => MindInput.HandNear(TapPosition, 0.16f);

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
