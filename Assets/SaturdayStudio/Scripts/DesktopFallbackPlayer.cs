using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace SaturdayStudio
{
    /// <summary>
    /// Testing without a headset: if no XR device (Quest or Meta XR Simulator) is running when the scene starts,
    /// the VR rig is switched off and a simple first-person player takes over.
    /// WASD to walk, mouse to look, hold left click to carry an object, right click to throw it, Esc frees the cursor.
    /// In a headset build XR is active, so this removes itself and the Building Blocks rig is used as normal.
    /// </summary>
    public class DesktopFallbackPlayer : MonoBehaviour
    {
        private const float EyeHeight = 1.6f;
        private const float WalkSpeed = 2.2f;
        private const float LookSpeed = 0.12f;
        private const float Reach = 3f;

        private CharacterController _body;
        private Transform _cam;
        private float _pitch;
        private float _fallSpeed;

        private Rigidbody _held;
        private float _holdDistance = 0.8f;
        private bool _heldUsedGravity;

        /// <summary>True while the desktop player is in control (no headset).</summary>
        public static bool Active { get; private set; }
        /// <summary>The grabbable under the crosshair (desktop only).</summary>
        public static Rigidbody AimedBody { get; private set; }
        private static DesktopFallbackPlayer _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Active = false;
            AimedBody = null;
            HeldBody = null;
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        // also after "live it again" reloads the scene
        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) Spawn();
        }

        private static void Spawn()
        {
            if (FindAnyObjectByType<DesktopFallbackPlayer>() != null) return;
            new GameObject("Desktop Fallback Player").AddComponent<DesktopFallbackPlayer>();
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            Active = false;
            AimedBody = null;
            HeldBody = null;
        }

        /// <summary>Let go of whatever is carried (zone-out, phone pull).</summary>
        public static void ForceDrop()
        {
            if (_instance != null) _instance.Drop(Vector3.zero);
        }

        private IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // a Quest build is always VR: never replace the Building Blocks rig there
            Destroy(gameObject);
            yield break;
#else
            // give the XR loader a moment to bring up the headset or the simulator
            yield return new WaitForSeconds(1f);
            if (XRSettings.isDeviceActive || XRRunning())
            {
                Destroy(gameObject);
                yield break;
            }
#endif

            var spawn = new Vector3(0.2f, 0, -1.4f);
            var spawnRot = Quaternion.identity;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (!root.name.Contains("Camera Rig")) continue;
                spawn = root.transform.position;
                spawnRot = Quaternion.Euler(0, root.transform.eulerAngles.y, 0);
                root.SetActive(false);
            }

            transform.SetPositionAndRotation(spawn, spawnRot);
            _body = gameObject.AddComponent<CharacterController>();
            _body.height = 1.75f;
            _body.radius = 0.25f;
            _body.center = new Vector3(0, 0.875f, 0);

            var camGo = new GameObject("Desktop Camera") { tag = "MainCamera" };
            _cam = camGo.transform;
            _cam.SetParent(transform, false);
            _cam.localPosition = new Vector3(0, EyeHeight, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.fieldOfView = 70;
            camGo.AddComponent<AudioListener>();

            Cursor.lockState = CursorLockMode.Locked;
            _instance = this;
            Active = true;
            Debug.Log("[Saturday Studio] No headset or Meta XR Simulator detected: desktop test mode.");
        }

        private static bool XRRunning()
        {
            var displays = new System.Collections.Generic.List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays) if (d.running) return true;
            return false;
        }

        private void Update()
        {
            if (_body == null) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (kb.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                return;
            }

            // look
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var d = mouse.delta.ReadValue() * LookSpeed;
                transform.Rotate(0, d.x, 0);
                _pitch = Mathf.Clamp(_pitch - d.y, -85, 85);
                _cam.localRotation = Quaternion.Euler(_pitch, 0, 0);
            }

            // walk
            var input = new Vector3((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), 0,
                (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            var move = transform.TransformDirection(Vector3.ClampMagnitude(input, 1)) * WalkSpeed * (kb.leftShiftKey.isPressed ? 1.8f : 1);
            _fallSpeed = _body.isGrounded ? -1f : _fallSpeed - 9.81f * Time.deltaTime;
            move.y = _fallSpeed;
            _body.Move(move * Time.deltaTime);

            // grab / drop / throw: E or a click toggles carrying, so it also works with a trackpad
            _aimed = null;
            _aimedTask = null;
            if (_held == null) FindTarget();
            AimedBody = _aimed;
            bool toggle = kb.eKey.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame;
            if (toggle)
            {
                if (_held != null)
                {
                    // carrying laundry next to the washer / rack: put it in or hang it right away
                    var g = _held.GetComponent<Garment>();
                    var action = ChoreFor(g);
                    Drop(Vector3.zero);
                    action?.Invoke();
                }
                else if (_aimed != null && GrabLock.CanGrab(_aimed)) PickUp(_aimed);
                else if (_aimedTask != null && _aimedTask.Target.CanActivate) _aimedTask.Target.Activate();
            }
            if (_held != null && (kb.qKey.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) Drop(_cam.forward * 5f);
            if (_held != null)
                _holdDistance = Mathf.Clamp(_holdDistance + mouse.scroll.ReadValue().y * 0.001f, 0.4f, 2f);
        }

        private Rigidbody _aimed;
        private TaskClickable _aimedTask;

        /// <summary>The object being carried in desktop mode, so chore zones can tell it is still in someone's hand.</summary>
        public static Rigidbody HeldBody { get; private set; }

        private void FindTarget()
        {
            // a thin sphere cast so small things like the phone or an apple are easy to aim at.
            // Solid hits: the nearest one wins (a pickup, or a wall that blocks the view).
            // Trigger hits only count if they are something to use (bed, tap, stove knob…), not the sink/washer/rack zones,
            // and they win when nothing grabbable is aimed at and they are not far behind the nearest solid surface.
            var hits = Physics.SphereCastAll(_cam.position, 0.06f, _cam.forward, Reach, ~0, QueryTriggerInteraction.Collide);
            float solidDist = float.MaxValue, taskDist = float.MaxValue;
            Rigidbody solidBody = null;
            TaskClickable triggerTask = null;
            foreach (var h in hits)
            {
                if (h.collider is CharacterController) continue;
                if (h.collider.isTrigger)
                {
                    var task = h.collider.GetComponentInParent<TaskClickable>();
                    if (task == null || task.Target == null || !task.Target.CanActivate || h.distance >= taskDist) continue;
                    taskDist = h.distance;
                    triggerTask = task;
                    continue;
                }
                if (h.distance >= solidDist) continue;
                solidDist = h.distance;
                solidBody = h.rigidbody != null && h.rigidbody.GetComponent<RespawnIfFallen>() != null ? h.rigidbody : null;
            }

            if (solidBody != null) _aimed = solidBody;
            else if (triggerTask != null && taskDist <= solidDist + 0.35f) _aimedTask = triggerTask;
        }

        private WasherMachine _washer;
        private DryingRack _rack;

        /// <summary>What dropping the carried piece of laundry here would do, or null.</summary>
        private System.Action ChoreFor(Garment g)
        {
            if (g == null) return null;
            if (_washer == null) _washer = FindAnyObjectByType<WasherMachine>();
            if (_rack == null) _rack = FindAnyObjectByType<DryingRack>();
            var p = g.transform.position;
            if (_washer != null && _washer.Accepts(g) && (_washer.IsNear(p) || _washer.IsNear(transform.position, 0.9f)))
                return () => _washer.TryLoad(g);
            if (_rack != null && _rack.Accepts(g) && (_rack.IsNear(p) || _rack.IsNear(transform.position, 1f)))
                return () => _rack.TryHang(g);
            return null;
        }

        private string ChoreHint(Garment g)
        {
            var a = ChoreFor(g);
            if (a == null) return null;
            return g.Current == Garment.State.Dirty ? "E / click: put it in the washing machine" : "E / click: hang it on the rack";
        }

        private void FixedUpdate()
        {
            if (_held == null) return;
            var target = _cam.position + _cam.forward * _holdDistance;
            _held.linearVelocity = Vector3.ClampMagnitude((target - _held.position) / Time.fixedDeltaTime * 0.5f, 8f);
            _held.angularVelocity *= 0.8f;
        }

        private void PickUp(Rigidbody rb)
        {
            _held = rb;
            HeldBody = rb;
            rb.isKinematic = false; // e.g. clothes hanging on the rack
            _heldUsedGravity = rb.useGravity;
            rb.useGravity = false;
            _holdDistance = Mathf.Clamp(Vector3.Distance(_cam.position, rb.position), 0.6f, 1.2f);
            // don't let the carried object shove the player around
            foreach (var col in rb.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, _body, true);
        }

        private void Drop(Vector3 throwVelocity)
        {
            if (_held == null) return;
            _held.useGravity = _heldUsedGravity;
            _held.linearVelocity = throwVelocity;
            foreach (var col in _held.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, _body, false);
            _held = null;
            HeldBody = null;
        }

        private GUIStyle _style;

        private void OnGUI()
        {
            if (_body == null) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            var c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            GUI.color = _aimed != null || _held != null || _aimedTask != null ? new Color(0.44f, 0.72f, 0.64f) : Color.white;
            GUI.Box(new Rect(c.x - 4, c.y - 4, 8, 8), GUIContent.none);
            GUI.color = Color.white;

            string chore = _held != null ? ChoreHint(_held.GetComponent<Garment>()) : null;
            string hint = chore != null ? $"Carrying {Describe(_held)}  ·  {chore}"
                : _held != null ? $"Carrying {Describe(_held)}  ·  E / click: drop  ·  Q / right click: throw"
                : _aimed != null ? (GrabLock.CanGrab(_aimed) ? $"{Describe(_aimed)}  ·  E / click: pick up" : Describe(_aimed))
                : _aimedTask != null ? (_aimedTask.Target.CanActivate ? $"{_aimedTask.Target.Label}  ·  E / click" : _aimedTask.Target.Label) : "";
            if (hint.Length > 0) GUI.Label(new Rect(c.x - 300, c.y + 24, 600, 26), hint, _style);
            // no other on-screen text: controls are on the TV, the time on the wall clock
        }

        private static string Describe(Rigidbody rb)
        {
            string n = rb.name.Replace("[Grab] ", "");
            var dish = rb.GetComponent<Dish>();
            if (n == "Cleaning Rag") return "Cleaning Rag (hold it on a floor stain)";
            if (dish != null) return dish.IsDirty ? $"{n} (dirty → sink)" : $"{n} (clean)";
            var g = rb.GetComponent<Garment>();
            if (g != null)
                return g.Current == Garment.State.Dirty ? $"{n} (dirty → washer)"
                    : g.Current == Garment.State.Wet ? $"{n} (wet → drying rack)" : $"{n} (dry)";
            return n;
        }
    }
}
