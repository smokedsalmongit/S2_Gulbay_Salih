using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SaturdayStudio
{
    public enum Mind { None, ADHD, OCD }

    /// <summary>How strongly the mind shapes the morning.</summary>
    public enum Level { Mild, Moderate, Severe }

    /// <summary>Where the three hours went in one playthrough (shown at 13:00).</summary>
    public class Ledger
    {
        public int ZoneOuts, PhonePickups, TaskPulls, Rewashes;
        public float ZoneOutMinutes, PhoneMinutes;
        public int HandWashes, Checks, Aligns, Resists, ExtraDishWashes, Contaminations, Calms;
        public float WashMinutes, CheckMinutes, AlignMinutes;
        public int ChoresDone, ChoresTotal;
        public float EndMinute;
        public Level Level;
    }

    /// <summary>
    /// The heart of the two virtual subjectivenesses: which mind is active, the in-game clock (10:00 → 13:00),
    /// and the inner state (ADHD interest, OCD anxiety) that the behaviours read and write.
    /// Created automatically at runtime in the Saturday Studio scene (see <see cref="MindBootstrap"/>).
    /// </summary>
    public class MindState : MonoBehaviour
    {
        public const float DayMinutes = 180f;

        public static MindState Instance { get; private set; }
        public static Mind Current => Instance != null ? Instance._mind : Mind.None;
        public static bool IsADHD => Current == Mind.ADHD;
        public static bool IsOCD => Current == Mind.OCD;
        public static Level CurrentLevel => Instance != null ? Instance._level : Level.Moderate;
        /// <summary>Multiplier the behaviours use: mild 0.5 · moderate 1 · severe 1.6.</summary>
        public static float Intensity => CurrentLevel == Level.Mild ? 0.5f : CurrentLevel == Level.Severe ? 1.6f : 1f;
        public static string Describe(Mind m, Level l) =>
            (m == Mind.ADHD ? "VS1 · ADHD" : m == Mind.OCD ? "VS2 · OCD" : "") + $" · {l.ToString().ToLower()}";

        /// <summary>Results of finished playthroughs this session, kept across scene reloads so both timelines can be compared.</summary>
        public static readonly Dictionary<Mind, Ledger> Results = new Dictionary<Mind, Ledger>();
        /// <summary>Mind to start automatically after a reload ("live it again as ...").</summary>
        public static Mind NextMind = Mind.None;
        public static Level NextLevel = Level.Moderate;

        public static event System.Action<Mind> MindChosen;
        public static event System.Action DayEnded;

        [SerializeField] private float _realMinutesForDay = 12f;

        private Mind _mind;
        private Level _level = Level.Moderate;
        public float Minute { get; private set; }
        public float ClockSpeed { get; set; } = 1f;
        public bool Ended { get; private set; }
        public bool Running => _mind != Mind.None && !Ended;
        public Ledger Log { get; private set; } = new Ledger();

        /// <summary>VS1: interest 0–100. Boredom drains it; low interest makes zone-outs likelier.</summary>
        public float Interest = 70f;
        /// <summary>VS2: anxiety 0–100. Drives the heartbeat, the narrowing vision and the whispers.</summary>
        public float Anxiety = 10f;

        public static void ResetStatics()
        {
            Results.Clear();
            NextMind = Mind.None;
            NextLevel = Level.Moderate;
            MindChosen = null;
            DayEnded = null;
            Instance = null;
        }

        public static string ClockText(float minute)
        {
            int m = Mathf.Clamp((int)minute, 0, (int)DayMinutes);
            return $"{10 + m / 60}:{m % 60:00}";
        }

        public string Now => ClockText(Minute);

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private System.Collections.IEnumerator Start()
        {
            if (NextMind == Mind.None) yield break;
            var m = NextMind;
            NextMind = Mind.None;
            yield return new WaitForSeconds(1.2f);
            Choose(m, NextLevel);
        }

        public void Choose(Mind m, Level level)
        {
            if (_mind != Mind.None || m == Mind.None) return;
            _mind = m;
            _level = level;
            Minute = 0;
            Log = new Ledger();
            MindChosen?.Invoke(m);
            ChoreBoard.Changed();
        }

        /// <summary>Time that a behaviour "eats" (phone scrolling, a ritual): jumps the clock forward.</summary>
        public void AddMinutes(float minutes)
        {
            if (!Running) return;
            Minute = Mathf.Min(DayMinutes, Minute + minutes);
        }

        private void Update()
        {
            if (!Running) return;
            Minute += Time.deltaTime / (_realMinutesForDay * 60f) * DayMinutes * ClockSpeed;
            if (Minute >= DayMinutes) EndDay();
        }

        public void EndDay()
        {
            if (!Running) return;
            Minute = Mathf.Min(Minute, DayMinutes);
            Ended = true;
            ClockSpeed = 0;
            var p = ChoreBoard.Progress;
            Log.ChoresDone = p.ChoresDone;
            Log.ChoresTotal = p.ChoresTotal;
            Log.EndMinute = Minute;
            Log.Level = _level;
            Results[_mind] = Log;
            GrabLock.LockAll(false);
            DayEnded?.Invoke();
            ChoreBoard.Changed();
        }

        public static void LiveAgain(Mind m, Level level)
        {
            NextMind = m;
            NextLevel = level;
            GrabLock.Reset();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static string Summary()
        {
            var sb = new StringBuilder();
            sb.Append("<b>13:00 · the doorbell rings</b>\n<size=75%>Where did the three hours go?</size>\n\n");
            if (Results.TryGetValue(Mind.ADHD, out var a))
            {
                sb.Append($"<color=#B9A6FF><b>VS1 · ADHD</b> ({a.Level.ToString().ToLower()})  attention drifts away</color>\n<size=75%>");
                sb.Append($"chores done {a.ChoresDone}/{a.ChoresTotal} · finished at {ClockText(a.EndMinute)}\n");
                sb.Append($"zoned out {a.ZoneOuts}× ({a.ZoneOutMinutes:0} min gone)\n");
                sb.Append($"phone {a.PhonePickups}× ({a.PhoneMinutes:0} min)\n");
                sb.Append($"pulled to another chore {a.TaskPulls}×\n");
                sb.Append($"forgotten laundry rewashed {a.Rewashes}×</size>\n\n");
            }
            if (Results.TryGetValue(Mind.OCD, out var o))
            {
                sb.Append($"<color=#7FD1C1><b>VS2 · OCD</b> ({o.Level.ToString().ToLower()})  attention gets stuck</color>\n<size=75%>");
                sb.Append($"chores done {o.ChoresDone}/{o.ChoresTotal} · finished at {ClockText(o.EndMinute)}\n");
                sb.Append($"washed hands {o.HandWashes}× ({o.WashMinutes:0} min)\n");
                sb.Append($"checked stove / door / washer {o.Checks}× ({o.CheckMinutes:0} min)\n");
                sb.Append($"straightened things {o.Aligns}× ({o.AlignMinutes:0} min)\n");
                sb.Append($"dishes washed again {o.ExtraDishWashes}× · urges resisted {o.Resists}× · calmed down {o.Calms}×</size>\n\n");
            }
            sb.Append(Results.Count < 2
                ? "<size=70%>Not laziness. Not tidiness.\nUse the menu to live the same morning through the other mind.</size>"
                : "<size=70%>The same chores fail for opposite reasons:\ntime lost between tasks, and time lost inside them.</size>");
            return sb.ToString();
        }
    }

    /// <summary>Creates the mind systems in the Saturday Studio scene (also after a reload).</summary>
    public static class MindBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            MindState.ResetStatics();
            GrabLock.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Spawn();
        }

        private static void OnLoaded(Scene s, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Spawn();
        }

        private static void Spawn()
        {
            if (Object.FindAnyObjectByType<ChoreBoard>() == null) return; // only in the Saturday Studio room
            if (Object.FindAnyObjectByType<MindState>() != null) return;
            GrabLock.Reset();
            var go = new GameObject("Mind Simulation (VS1 ADHD / VS2 OCD)");
            go.AddComponent<MindState>();
            go.AddComponent<MindPost>();
            go.AddComponent<MindHud>();
            go.AddComponent<WallClock>();
            go.AddComponent<TvScreen>();
            go.AddComponent<StartMenu>();
            go.AddComponent<FloorChore>();
            go.AddComponent<VirtualHands>();
            go.AddComponent<HomeExtras>();
            go.AddComponent<ClothesMaker>();
            go.AddComponent<AdhdBehaviours>();
            go.AddComponent<OcdBehaviours>();
        }
    }
}
