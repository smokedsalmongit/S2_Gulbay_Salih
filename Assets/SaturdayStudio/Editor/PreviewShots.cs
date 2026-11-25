using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SaturdayStudio.EditorTools
{
    /// <summary>
    /// Development helper: plays the Saturday Studio scene and saves screenshots of key spots, so changes can be
    /// checked without a headset. Run from a terminal:
    /// Unity -batchmode -projectPath . -executeMethod SaturdayStudio.EditorTools.PreviewShots.Run -shotsDir /some/folder
    /// </summary>
    public static class PreviewShots
    {
        private const string Key = "SaturdayStudio.PreviewShots";
        private static double _start;
        private static int _step;

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            string dir = Path.Combine(Application.dataPath, "../Temp/Shots");
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-shotsDir") dir = args[i + 1];
            Directory.CreateDirectory(dir);
            SessionState.SetString(Key, dir);
            EditorSceneManager.OpenScene("Assets/SaturdayStudio/Scenes/SaturdayStudio.unity");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Tick()
        {
            string dir = SessionState.GetString(Key, "");
            if (dir == "" || !EditorApplication.isPlaying) return;
            if (_start == 0) _start = EditorApplication.timeSinceStartup;
            double t = EditorApplication.timeSinceStartup - _start;

            if (_step == 0 && t > 4)
            {
                Shot(dir, "1_menu", new Vector3(0.2f, 1.6f, -1.5f), new Vector3(0.2f, 1.35f, -0.65f));
                Shot(dir, "2_tv_start", new Vector3(1.2f, 1.35f, 0.2f), new Vector3(1.2f, 1.25f, -2.45f));
                Shot(dir, "3_clock", new Vector3(-0.2f, 1.75f, 1.3f), new Vector3(-0.2f, 1.95f, 2.49f));
                Shot(dir, "4_window_city", new Vector3(1.2f, 1.6f, -0.9f), new Vector3(3.1f, 1.55f, -0.9f));
                Shot(dir, "5_clothes", new Vector3(0.9f, 1.4f, 0.9f), new Vector3(0.5f, 0.1f, 1.8f));
                Shot(dir, "6_dish_rack", new Vector3(-1.0f, 1.45f, 1.45f), new Vector3(-1.43f, 1.0f, 2.2f));
                Shot(dir, "7_room", new Vector3(2.6f, 2.2f, -2.2f), new Vector3(-0.8f, 0.8f, 1.5f));
                SaturdayStudio.MindState.Instance.Choose(SaturdayStudio.Mind.OCD, SaturdayStudio.Level.Moderate);
                if (SaturdayStudio.VirtualHands.Instance != null) SaturdayStudio.VirtualHands.Instance.Dirt = 1f;
                // fake some progress: three dishes washed and drying, all laundry washed, two pieces hung
                var rack = Object.FindAnyObjectByType<SaturdayStudio.DishRack>();
                int n = 0;
                foreach (var d in Object.FindObjectsByType<SaturdayStudio.Dish>())
                {
                    if (n++ >= 4) break;
                    d.Clean();
                    rack.TryRack(d);
                }
                var dry = Object.FindAnyObjectByType<SaturdayStudio.DryingRack>();
                int h = 0;
                foreach (var g in Object.FindObjectsByType<SaturdayStudio.Garment>())
                {
                    g.SetState(SaturdayStudio.Garment.State.Wet);
                    if (h++ < 2) dry.TryHang(g);
                }
                _step = 1;
            }
            else if (_step == 1 && t > 7)
            {
                Shot(dir, "8_tv_ocd", new Vector3(1.2f, 1.35f, 0.2f), new Vector3(1.2f, 1.25f, -2.45f));
                Shot(dir, "9_wet_clothes", new Vector3(1.0f, 1.0f, 1.3f), new Vector3(0.4f, 0.1f, 2.0f));
                Shot(dir, "11_hung_clothes", new Vector3(1.2f, 1.3f, -1.0f), new Vector3(2.25f, 0.8f, -1.0f));
                Shot(dir, "12_dishes_drying", new Vector3(-1.0f, 1.45f, 1.45f), new Vector3(-1.43f, 1.0f, 2.2f));
                Shot(dir, "13_floor_clothes", new Vector3(1.2f, 1.2f, 0.1f), new Vector3(1.1f, 0.0f, 0.9f));
                var cam = Camera.main;
                if (cam != null) Render(cam, Path.Combine(dir, "10_player_view_hands.png"));
                if (SaturdayStudio.MindPost.Instance != null) SaturdayStudio.MindPost.Instance.ZoneOut = 1f; // preview the zone-out look
                _step = 2;
            }
            else if (_step == 2 && t > 11)
            {
                var cam = Camera.main;
                if (cam != null) Render(cam, Path.Combine(dir, "14_zone_out_look.png"));
                _step = 3;
            }
            else if (_step == 3)
            {
                SessionState.EraseString(Key);
                EditorApplication.Exit(0);
            }
        }

        private static void Shot(string dir, string name, Vector3 from, Vector3 at)
        {
            var go = new GameObject("Preview Camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60;
            cam.nearClipPlane = 0.02f;
            go.transform.position = from;
            go.transform.LookAt(at);
            Render(cam, Path.Combine(dir, name + ".png"));
            Object.DestroyImmediate(go);
        }

        private static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(1024, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1024, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1024, 640), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
