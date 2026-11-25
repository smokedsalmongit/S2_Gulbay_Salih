using TMPro;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>The note on the wall: the in-game clock (10:00 → 13:00) and a live checklist of the four chores.</summary>
    public class ChoreBoard : MonoBehaviour
    {
        public struct ChoreProgress
        {
            public int DishesClean, DishesRacked, DishesTotal;
            public bool BedMade;
            public int Washed, Hung, ClothesTotal;
            public int StainsClean, StainsTotal;

            /// <summary>Washed and put in the drying rack next to the sink.</summary>
            public bool DishesDone => DishesTotal > 0 && DishesRacked == DishesTotal;
            public bool LaundryDone => ClothesTotal > 0 && Hung == ClothesTotal;
            public bool FloorDone => StainsTotal == 0 || StainsClean == StainsTotal;
            public int ChoresDone => (DishesDone ? 1 : 0) + (BedMade ? 1 : 0) + (LaundryDone ? 1 : 0) + (FloorDone ? 1 : 0);
            public int ChoresTotal => 4;
            public bool AllDone => ChoresDone == ChoresTotal;
            /// <summary>Every small step counts (used for VS1's "small win" interest boost).</summary>
            public int Points => DishesClean + DishesRacked + (BedMade ? 3 : 0) + Washed + Hung + StainsClean;
        }

        [SerializeField] private TextMeshPro _text;
        [SerializeField] private float _realMinutesFor3Hours = 15f;

        private static ChoreBoard _instance;
        private bool _dirty = true;
        private float _elapsed;
        private string _finishedAt;
        private ChoreProgress _progress;

        public static ChoreProgress Progress => _instance != null ? _instance._progress : default;

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

        private ChoreProgress Compute()
        {
            var p = new ChoreProgress();
            var dishes = FindObjectsByType<Dish>();
            p.DishesTotal = dishes.Length;
            foreach (var d in dishes)
            {
                if (!d.IsDirty) p.DishesClean++;
                if (!d.IsDirty && d.IsRacked) p.DishesRacked++;
            }
            var clothes = FindObjectsByType<Garment>(FindObjectsInactive.Include);
            p.ClothesTotal = clothes.Length;
            foreach (var g in clothes)
            {
                if (g.Current != Garment.State.Dirty) p.Washed++;
                if (g.WasHung && g.Current != Garment.State.Dirty) p.Hung++;
            }
            var bed = FindAnyObjectByType<BedMaker>();
            p.BedMade = bed != null && bed.IsMade;
            var floor = FloorChore.Instance;
            if (floor != null)
            {
                p.StainsClean = floor.Cleaned;
                p.StainsTotal = floor.Total;
            }
            return p;
        }

        private void Redraw()
        {
            _dirty = false;
            var p = _progress = Compute();
            var ms = MindState.Instance;

            string clock;
            if (ms != null)
                clock = ms.Now;
            else
            {
                float minutes = Mathf.Min(180, _elapsed / (_realMinutesFor3Hours * 60) * 180);
                clock = MindState.ClockText(minutes);
            }

            if (p.AllDone && _finishedAt == null && (ms == null || ms.Running))
            {
                _finishedAt = clock;
                if (ms != null) ms.EndDay();
            }

            // the note is just a reminder now: the to-do list and progress are on the TV, the time on the wall clock
            string status = _finishedAt != null ? $"\n\n<color=#3E8E74><b>All done at {_finishedAt}!</b></color>"
                : ms != null && ms.Ended ? "\n\n<color=#B5553C><b>13:00 · out of time</b></color>" : "";
            _text.text = $"<b>SATURDAY</b>\nfriends arrive at 13:00!\n\n<color=#7A7F7C>to-do list: on the TV</color>{status}";
        }
    }
}
