using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SaturdayStudio.EditorTools
{
    /// <summary>
    /// Development helper (batch mode): loads the apartment model in an empty scene, writes every part's position and
    /// size to a text file, and saves a top-down view plus a few views from inside, so the game can be fitted to it.
    /// Unity -batchmode -projectPath . -executeMethod SaturdayStudio.EditorTools.ApartmentInspector.Run -outDir /some/folder
    /// </summary>
    public static class ApartmentInspector
    {
        public const string ModelPath = "Assets/VR_3d_ASSETS/apartamento/source/apartamento.fbx";

        public static void Run()
        {
            ApartmentMaterials.Run();
            string dir = Arg("-outDir", Path.Combine(Application.dataPath, "../Temp/Apartment"));
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var sb = new StringBuilder();
            var all = new Bounds();
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                if (first) { all = b; first = false; } else all.Encapsulate(b);
                var mats = new StringBuilder();
                foreach (var m in r.sharedMaterials) mats.Append(m != null ? $"{m.name}[{(m.mainTexture != null ? m.mainTexture.name : "-")}] " : "null ");
                sb.AppendLine($"{r.name} | center {V(b.center)} | size {V(b.size)} | {mats}");
            }
            sb.Insert(0, $"ROOT scale {go.transform.localScale} rot {go.transform.eulerAngles} | ALL center {V(all.center)} size {V(all.size)}\n");
            File.WriteAllText(Path.Combine(dir, "parts.txt"), sb.ToString());

            // top-down orthographic view of the whole flat
            var cam = new GameObject("Shot").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(all.size.x, all.size.z) * 0.55f;
            cam.transform.position = all.center + Vector3.up * (all.size.y + 10);
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = all.size.y + 30;
            // look under the ceiling: clip just above floor + 2 m
            cam.transform.position = new Vector3(all.center.x, all.min.y + 2.0f, all.center.z);
            cam.nearClipPlane = 0.01f;
            Render(cam, Path.Combine(dir, "top.png"), 1400, 1400);

            cam.orthographic = false;
            cam.fieldOfView = 80;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            // views from inside each area: (position, yaw, pitch)
            var views = new (string name, Vector3 pos, float yaw, float pitch)[]
            {
                ("kitchen_to_counter", new Vector3(1.0f, 1.6f, -1.0f), 90, 15),
                ("kitchen_to_fridge", new Vector3(1.0f, 1.6f, -2.2f), 20, 10),
                ("dining_to_hall", new Vector3(1.6f, 1.6f, -2.5f), 270, 5),
                ("living_to_tv", new Vector3(1.6f, 1.6f, -5.1f), 270, 12),
                ("living_to_balcony", new Vector3(1.0f, 1.6f, -4.0f), 180, 5),
                ("living_to_kitchen", new Vector3(1.0f, 1.6f, -6.5f), 0, 5),
                ("bedroom", new Vector3(-1.6f, 1.6f, -5.0f), 270, 15),
                ("bedroom_window", new Vector3(-2.6f, 1.6f, -4.2f), 180, 5),
                ("hall_north", new Vector3(-1.2f, 1.6f, -2.8f), 0, 5),
                ("hall_south", new Vector3(-1.2f, 1.6f, -0.5f), 180, 5),
                ("balcony", new Vector3(1.0f, 1.6f, -8.3f), 0, 5),
            };
            foreach (var v in views)
            {
                cam.transform.SetPositionAndRotation(v.pos, Quaternion.Euler(v.pitch, v.yaw, 0));
                Render(cam, Path.Combine(dir, $"view_{v.name}.png"), 1200, 750);
            }
            EditorApplication.Exit(0);
        }

        private static string Arg(string name, string fallback)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return fallback;
        }

        private static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

        public static void Render(Camera cam, string path, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
