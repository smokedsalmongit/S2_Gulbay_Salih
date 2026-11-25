using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>
    /// The fourth chore from the storyboard: the stains on the floor. A cleaning rag lies by the sink;
    /// hold (or put) it on a stain for a couple of seconds and the stain fades away.
    /// </summary>
    public class FloorChore : MonoBehaviour
    {
        public static FloorChore Instance { get; private set; }

        private const float ScrubTime = 2.5f;

        public readonly List<Transform> Stains = new List<Transform>();
        private readonly Dictionary<Transform, float> _progress = new Dictionary<Transform, float>();
        public Rigidbody Rag { get; private set; }

        /// <summary>VS2 has no floor stains (the OCD morning is about dishes, bed and laundry).</summary>
        public bool Disabled { get; private set; }
        public int Total => Disabled ? 0 : Stains.Count + _cleaned;
        public int Cleaned => _cleaned;
        private int _cleaned;

        private void Awake() => Instance = this;

        private void OnEnable() => MindState.MindChosen += OnChosen;
        private void OnDisable() => MindState.MindChosen -= OnChosen;

        private void OnChosen(Mind m)
        {
            if (m != Mind.OCD) return;
            Disabled = true;
            foreach (var s in Stains) if (s != null) s.gameObject.SetActive(false);
            Stains.Clear();
            if (Rag != null) Rag.gameObject.SetActive(false);
            ChoreBoard.Changed();
        }

        private void Start()
        {
            foreach (var t in FindObjectsByType<Transform>())
                if (t.name == "Stain" && t.GetComponent<Renderer>() != null) Stains.Add(t);
            MakeRag();
            ChoreBoard.Changed();
        }

        /// <summary>A copy of a piece of laundry becomes the rag, so it is grabbable with the same Building Block setup.</summary>
        private void MakeRag()
        {
            var source = FindAnyObjectByType<Garment>(FindObjectsInactive.Include);
            if (source == null) return;
            var go = Instantiate(source.gameObject, Spots.Pos("Rag", new Vector3(-1.0f, 0.04f, 1.72f)), Quaternion.Euler(0, 15, 0));
            go.SetActive(true);
            go.name = "[Grab] Cleaning Rag";
            DestroyImmediate(go.GetComponent<Garment>());
            go.transform.localScale = new Vector3(0.75f, 0.5f, 0.75f);
            var r = go.GetComponent<Renderer>();
            var m = new Material(r.sharedMaterial) { color = new Color(0.55f, 0.66f, 0.74f) };
            r.sharedMaterial = m;
            Rag = go.GetComponent<Rigidbody>();
            Rag.isKinematic = false;
        }

        private void Update()
        {
            if (Rag == null || Disabled) return;
            var p = Rag.position;
            for (int i = Stains.Count - 1; i >= 0; i--)
            {
                var s = Stains[i];
                var d = s.position - p;
                float reach = Mathf.Max(s.lossyScale.x, s.lossyScale.z) * 0.25f + 0.1f;
                bool on = p.y < 0.45f && new Vector2(d.x, d.z).magnitude < reach;
                _progress.TryGetValue(s, out var t);
                t = on ? t + Time.deltaTime : Mathf.Max(0, t - Time.deltaTime);
                _progress[s] = t;
                if (on) MindHud.Status($"Scrubbing the stain  {Mathf.RoundToInt(t / ScrubTime * 100)}%");
                if (t < ScrubTime) continue;

                Stains.RemoveAt(i);
                _progress.Remove(s);
                _cleaned++;
                StartCoroutine(Fade(s));
                MindAudio.PlayAt(MindAudio.Chime, s.position, 0.5f);
                ChoreBoard.Changed();
            }
        }

        private static IEnumerator Fade(Transform s)
        {
            var start = s.localScale;
            for (float a = 0; a < 1; a += Time.deltaTime * 2)
            {
                s.localScale = Vector3.Lerp(start, new Vector3(0, start.y, 0), a);
                yield return null;
            }
            s.gameObject.SetActive(false);
        }
    }
}
