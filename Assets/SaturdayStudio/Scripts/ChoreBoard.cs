using TMPro;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>The note on the wall: an in-game clock from 10:00 to 13:00 and a live checklist of the chores.</summary>
    public class ChoreBoard : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _text;
        [SerializeField] private float _realMinutesFor3Hours = 15f;

        private static ChoreBoard _instance;
        private bool _dirty = true;
        private float _elapsed;
        private string _finishedAt;

        public static void Changed()
        {
            if (_instance != null) _instance._dirty = true;
        }

        private void Awake() => _instance = this;

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_dirty || Time.frameCount % 30 == 0) Redraw();
        }

        private void Redraw()
        {
            _dirty = false;
            var dishes = FindObjectsByType<Dish>();
            var clothes = FindObjectsByType<Garment>(FindObjectsInactive.Include);
            var bed = FindAnyObjectByType<BedMaker>();

            int clean = 0, washed = 0, hung = 0;
            foreach (var d in dishes) if (!d.IsDirty) clean++;
            foreach (var g in clothes)
            {
                if (g.Current != Garment.State.Dirty) washed++;
                if (g.WasHung) hung++;
            }

            bool dishesDone = dishes.Length > 0 && clean == dishes.Length;
            bool bedDone = bed != null && bed.IsMade;
            bool washDone = clothes.Length > 0 && washed == clothes.Length;
            bool hangDone = clothes.Length > 0 && hung == clothes.Length;

            float minutes = Mathf.Min(180, _elapsed / (_realMinutesFor3Hours * 60) * 180);
            string clock = $"{10 + (int)minutes / 60}:{(int)minutes % 60:00}";
            if (dishesDone && bedDone && washDone && hangDone && _finishedAt == null) _finishedAt = clock;

            string Line(bool done, string label, string count) =>
                done ? $"<color=#3E8E74>[x] <s>{label}</s></color>" : $"[ ] {label} <color=#7A7F7C>{count}</color>";

            _text.text =
                $"<b>SATURDAY</b>  <color=#7A7F7C>{clock}</color>\nfriends arrive 13:00\n\n" +
                Line(dishesDone, "wash the dishes", $"{clean}/{dishes.Length}") + "\n" +
                Line(bedDone, "make the bed", "") + "\n" +
                Line(washDone, "wash the laundry", $"{washed}/{clothes.Length}") + "\n" +
                Line(hangDone, "hang it to dry", $"{hung}/{clothes.Length}") +
                (_finishedAt != null ? $"\n\n<color=#3E8E74><b>All done at {_finishedAt}!</b></color>" : "");
        }
    }
}
