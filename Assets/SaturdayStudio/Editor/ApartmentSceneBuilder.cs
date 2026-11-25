using System.Collections.Generic;
using System.Linq;
using SaturdayStudio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SaturdayStudio.EditorTools
{
    /// <summary>
    /// Builds Scenes/SaturdayStudio_Apartment.unity: a copy of the original game scene where the procedural room is
    /// replaced by the textured apartment model, and every chore is moved onto the apartment's own kitchen, bed and TV.
    /// The original scene is not touched. Menu: Saturday Studio → Apartment → Build Apartment Scene.
    /// </summary>
    public static class ApartmentSceneBuilder
    {
        private const string Source = "Assets/SaturdayStudio/Scenes/SaturdayStudio.unity";
        public const string Target = "Assets/SaturdayStudio/Scenes/SaturdayStudio_Apartment.unity";

        [MenuItem("Saturday Studio/Apartment/Build Apartment Scene")]
        public static void Build()
        {
            ApartmentMaterials.Run();
            AssetDatabase.DeleteAsset(Target);
            AssetDatabase.CopyAsset(Source, Target);
            var scene = EditorSceneManager.OpenScene(Target);

            var studio = GameObject.Find("Saturday Studio").transform;
            var env = studio.Find("Environment");
            var chores = studio.Find("Chores");
            var grabs = studio.Find("Grabbables");

            // ---------- the apartment ----------
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ApartmentInspector.ModelPath);
            var apt = (GameObject)PrefabUtility.InstantiatePrefab(model);
            apt.name = "Apartment (asset)";
            foreach (var r in apt.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.name;
                // its own table settings and centrepiece make room for our dishes and fruit bowl
                if (n.StartsWith("7_Dinner") || n == "7_Vegetation_Red Plant" || n == "7_Vegetation_Green Plant") { r.gameObject.SetActive(false); continue; }
                // walk-into-able collision for walls, floor, furniture (not for tiny decor)
                if (n.StartsWith("7_") || n.StartsWith("5_") || n.StartsWith("6_")) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mc = r.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }

            // ---------- keep the interactive parts of the old room, drop the rest ----------
            var stream = env.Find("Water Stream");
            var washer = env.Find("Washing Machine");
            var drum = washer.Find("Washer Drum Glass");
            var display = washer.Find("Washer Display");
            var rack = env.Find("Drying Rack");
            var stainMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/SaturdayStudio/Meshes/StainDisc.asset");
            var stainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SaturdayStudio/Materials/Stain.mat");
            foreach (var t in new[] { stream, drum, display, rack }) t.SetParent(chores, true);
            Object.DestroyImmediate(env.gameObject);
            foreach (var tmp in chores.GetComponentsInChildren<TextMeshPro>(true).ToList())
                if (tmp.name.EndsWith(" Sign")) Object.DestroyImmediate(tmp.gameObject); // no text pop-ups in the flat

            var props = new GameObject("Apartment Props").transform;
            props.SetParent(studio, false);

            // ---------- kitchen: sink, water, tap ----------
            var sinkZone = chores.GetComponentInChildren<SinkZone>().transform;
            sinkZone.position = new Vector3(2.41f, 0.86f, -1.27f);
            sinkZone.rotation = Quaternion.identity;
            var sinkBox = sinkZone.GetComponent<BoxCollider>();
            sinkBox.center = Vector3.zero;
            sinkBox.size = new Vector3(0.44f, 0.32f, 0.77f);
            stream.position = new Vector3(2.47f, 0.94f, -1.3f);
            stream.localScale = new Vector3(0.024f, 0.17f, 0.024f);

            // ---------- washing machine: the apartment's own appliance ----------
            var aptWasher = apt.transform.GetComponentsInChildren<Transform>(true).First(t => t.name == "4_Kitchen_WashMachine");
            var facing = Quaternion.Euler(0, 90, 0); // its door faces -x, into the kitchen
            drum.SetPositionAndRotation(new Vector3(2.105f, 0.42f, -2.05f), facing * Quaternion.Euler(-90, 0, 0));
            display.SetPositionAndRotation(new Vector3(2.06f, 0.76f, -2.05f), facing);
            display.localScale = Vector3.one * 0.8f;
            var output = new GameObject("Washer Output").transform;
            output.SetParent(chores, false);
            output.position = new Vector3(1.62f, 0.35f, -2.05f);
            var washerZone = chores.GetComponentInChildren<WasherMachine>().transform;
            washerZone.SetPositionAndRotation(new Vector3(1.86f, 0.4f, -2.05f), Quaternion.Euler(0, 90, 0));
            var wm = new SerializedObject(washerZone.GetComponent<WasherMachine>());
            wm.FindProperty("_drum").objectReferenceValue = drum;
            wm.FindProperty("_body").objectReferenceValue = aptWasher;
            wm.FindProperty("_output").objectReferenceValue = output;
            wm.ApplyModifiedPropertiesWithoutUndo();

            // ---------- bed: our crumpled blanket on the apartment's bed ----------
            var bed = chores.Find("Bed (select to make it)");
            var oldBed = bed.position;
            foreach (var n in new[] { "Bed Frame", "Mattress", "Headboard" })
            {
                var c = bed.Find(n);
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
            bed.SetPositionAndRotation(new Vector3(-3.0f, 0.1f, -5.05f), Quaternion.Euler(0, -90, 0));
            var maker = bed.GetComponent<BedMaker>();
            maker.Setup(bed.Find("Blanket (unmade)"), bed.Find("Blanket Fold"),
                bed.TransformPoint(new Vector3(2.2f, 0.545f, 1.25f) - oldBed), new Vector3(0, -90, 0), new Vector3(1.2f, 1, 1.45f));
            EditorUtility.SetDirty(maker);

            // ---------- drying rack on the balcony ----------
            var rackZone = chores.GetComponentInChildren<DryingRack>().transform;
            var rackOffset = rackZone.position - rack.position;
            rack.SetPositionAndRotation(new Vector3(2.05f, 0, -7.65f), Quaternion.Euler(0, 90, 0));
            rackZone.SetPositionAndRotation(rack.position + Quaternion.Euler(0, 90, 0) * rackOffset, Quaternion.Euler(0, 90, 0));

            // ---------- the note on the fridge ----------
            var note = chores.Find("Chore List Text");
            if (note != null)
            {
                note.SetPositionAndRotation(new Vector3(1.05f, 1.38f, 0.297f), Quaternion.identity);
                var tmp = note.GetComponent<TextMeshPro>();
                tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tmp.rectTransform.sizeDelta = new Vector2(0.31f, 0.25f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 0.1f;
                tmp.fontSizeMax = 0.3f;
                var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(paper.GetComponent<Collider>());
                paper.name = "Note Paper";
                paper.transform.SetParent(props, false);
                paper.transform.position = new Vector3(1.05f, 1.38f, 0.305f);
                paper.transform.localScale = new Vector3(0.36f, 0.3f, 0.004f);
                paper.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/SaturdayStudio/Materials/Paper.mat");
            }

            // ---------- floor stains (ADHD only, removed for OCD at runtime) ----------
            foreach (var (pos, yaw, scale) in new[]
                     {
                         (new Vector3(1.45f, 0.006f, -0.95f), 0f, new Vector3(1.0f, 1, 0.8f)),
                         (new Vector3(0.35f, 0.006f, -3.6f), 40f, new Vector3(0.8f, 1, 1.1f)),
                         (new Vector3(-2.0f, 0.006f, -4.3f), 0f, new Vector3(0.6f, 1, 0.6f)),
                     })
            {
                var s = new GameObject("Stain");
                s.transform.SetParent(props, false);
                s.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
                s.transform.localScale = scale;
                s.AddComponent<MeshFilter>().sharedMesh = stainMesh;
                s.AddComponent<MeshRenderer>().sharedMaterial = stainMat;
            }

            // ---------- movable things ----------
            PlaceGrabbables(grabs);

            // ---------- spots the runtime systems read ----------
            var spots = new GameObject("Spots").transform;
            spots.SetParent(studio, false);
            Spot(spots, "Tap", new Vector3(2.44f, 0.98f, -1.3f));
            Spot(spots, "Stove", new Vector3(2.15f, 0.93f, -0.48f), target: apt.transform.GetComponentsInChildren<Transform>(true).First(t => t.name == "4_Kitchen_Cooktop").gameObject);
            Spot(spots, "Front Door", new Vector3(-0.8f, 1.0f, 0.9f), target: null);
            Spot(spots, "Washer Door", new Vector3(2.08f, 0.42f, -2.05f), target: drum.gameObject);
            Spot(spots, "Dish Rack", new Vector3(2.44f, 0.905f, -2.05f));
            Spot(spots, "Clock", new Vector3(-0.585f, 2.37f, -2.45f), Quaternion.Euler(0, 90, 0));
            Spot(spots, "TV Screen", new Vector3(-0.335f, 0.805f, -5.16f), Quaternion.Euler(0, 90, 0), 0.78f);
            Spot(spots, "Rag", new Vector3(1.8f, 0.04f, -1.0f));
            Spot(spots, "City", new Vector3(0, 0, -9.2f), Quaternion.Euler(0, 90, 0));
            var spawn = new GameObject("Player Spawn").transform;
            spawn.SetParent(spots, false);
            spawn.SetPositionAndRotation(new Vector3(1.75f, 0, -5.0f), Quaternion.Euler(0, 270, 0)); // looking at the TV

            // ---------- light for the movable (lit) objects; the flat itself is baked ----------
            var lights = new GameObject("Apartment Lights").transform;
            lights.SetParent(studio, false);
            foreach (var p in new[] { new Vector3(1.4f, 2.4f, -1.4f), new Vector3(0.9f, 2.4f, -5.0f), new Vector3(-2.6f, 2.4f, -5.0f) })
            {
                var l = new GameObject("Lamp").AddComponent<Light>();
                l.transform.SetParent(lights, false);
                l.transform.position = p;
                l.type = LightType.Point;
                l.range = 5f;
                l.intensity = 0.6f;
                l.color = new Color(1f, 0.92f, 0.82f);
                l.shadows = LightShadows.None;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // the apartment scene starts first in builds; the original room stays in the list
            var list = EditorBuildSettings.scenes.Where(s => s.path != Target).ToList();
            list.Insert(0, new EditorBuildSettingsScene(Target, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        private static void Spot(Transform parent, string name, Vector3 pos, Quaternion? rot = null, float scale = 1f, GameObject target = null)
        {
            var t = new GameObject("Spot · " + name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
            t.localScale = Vector3.one * scale;
            if (target != null) t.gameObject.AddComponent<SpotTarget>().Target = target;
        }

        private static void PlaceGrabbables(Transform grabs)
        {
            var count = new Dictionary<string, int>();
            const float table = 0.765f;   // top of the round dining table
            const float counter = 0.912f; // kitchen worktop
            const float coffee = 0.385f;  // coffee table in the living room

            var plan = new Dictionary<string, Vector3[]>
            {
                { "Plate", new[] { new Vector3(0.55f, table, -1.5f), new Vector3(0.55f, table + 0.016f, -1.5f), new Vector3(0.55f, table + 0.032f, -1.5f), new Vector3(1.05f, table, -2.0f), new Vector3(0.55f, table, -2.0f) } },
                { "Bowl", new[] { new Vector3(1.05f, table, -1.5f) } },
                { "Mug Blue", new[] { new Vector3(0.8f, table, -2.18f) } },
                { "Mug Coral", new[] { new Vector3(2.4f, counter, 0.55f) } },
                { "Frying Pan", new[] { new Vector3(2.44f, counter + 0.02f, -0.3f) } },
                { "Fruit Bowl", new[] { new Vector3(0.8f, table, -1.75f) } },
                { "Apple", new[] { new Vector3(0.75f, table + 0.04f, -1.75f), new Vector3(0.84f, table + 0.05f, -1.72f), new Vector3(0.8f, table + 0.06f, -1.8f) } },
                { "Bottle", new[] { new Vector3(2.45f, counter, 0.0f) } },
                { "Phone", new[] { new Vector3(0.95f, coffee, -4.95f) } },
                { "Vase", new[] { new Vector3(0.85f, coffee, -5.4f) } },
                { "Pillow", new[] { new Vector3(2.35f, 0.55f, -4.75f), new Vector3(-3.55f, 0.72f, -5.45f) } },
                { "Laundry Basket", new[] { new Vector3(-1.3f, 0.01f, -6.3f) } },
                { "Clothes Lavender", new[] { new Vector3(-1.3f, 0.07f, -6.3f) } },
                { "Clothes Teal", new[] { new Vector3(-1.3f, 0.14f, -6.3f) } },
                { "Clothes Yellow", new[] { new Vector3(-1.3f, 0.21f, -6.3f) } },
                { "Clothes Coral", new[] { new Vector3(-2.0f, 0.04f, -3.95f) } },
                { "Clothes Sky", new[] { new Vector3(0.9f, 0.04f, -6.4f) } },
            };
            foreach (Transform g in grabs)
            {
                string n = g.name.Replace("[Grab] ", "");
                if (!plan.TryGetValue(n, out var spots)) continue;
                count.TryGetValue(n, out int i);
                count[n] = i + 1;
                if (i < spots.Length) g.position = spots[i];
            }
        }
    }
}
