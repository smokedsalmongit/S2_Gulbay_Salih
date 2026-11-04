using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SaturdayStudio.EditorTools
{
    /// <summary>
    /// Builds the "Saturday Studio" apartment: a small messy studio (kitchen with a real sink, washing machine,
    /// drying rack, unmade bed, table, TV corner) with grabbable props made from procedural meshes.
    /// Grabbables are cloned from the Meta "Grab Interaction" Building Block cube, so they keep its
    /// Grabbable + GrabInteractable + HandGrabInteractable setup. Locomotion comes from the
    /// OVRComprehensiveInteractionRig Building Block (thumbstick sliding / turning).
    /// Chores: wash dishes in the sink, make the bed (ray select), wash clothes, hang them on the rack.
    /// Menu: Saturday Studio > Build Room
    /// </summary>
    public static class SaturdayStudioBuilder
    {
        private const string Root = "Assets/SaturdayStudio";
        private const string ScenePath = Root + "/Scenes/SaturdayStudio.unity";
        private const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
        private const string RootName = "Saturday Studio";

        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        [MenuItem("Saturday Studio/Build Room")]
        public static void BuildRoom()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                EditorUtility.DisplayDialog("Saturday Studio",
                    "TextMeshPro resources are being imported for the wall note and signs.\n\nWhen Unity has finished importing, click Saturday Studio > Build Room again.", "OK");
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EnsureFolder(Root + "/Scenes");
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    if (!AssetDatabase.CopyAsset(SourceScenePath, ScenePath))
                    {
                        Debug.LogError($"[Saturday Studio] Could not copy {SourceScenePath}. It needs the Camera Rig and Grab Interaction Building Blocks.");
                        return;
                    }
                }
                EditorSceneManager.OpenScene(ScenePath);
            }

            EnsureFolder(Root + "/Meshes");
            EnsureFolder(Root + "/Materials");
            Meshes.Clear();

            var template = FindTemplate();
            if (template == null)
            {
                Debug.LogError("[Saturday Studio] No Grab Interaction Building Block found. Add one from Meta XR Tools > Building Blocks > Grab Interaction, then run Build Room again.");
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName).transform;
            var env = new GameObject("Environment").transform;
            env.SetParent(root);
            var chores = new GameObject("Chores").transform;
            chores.SetParent(root);
            var props = new GameObject("Grabbables").transform;
            props.SetParent(root);

            BuildShell(env, chores);
            BuildKitchen(env, chores);
            BuildWasher(env, chores);
            BuildBed(env, chores);
            BuildDryingRack(env, chores);
            BuildLivingArea(env);
            BuildLighting();
            BuildGrabbables(props, template);
            PlaceRig();

            template.SetActive(false);

            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddSceneToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("[Saturday Studio] Room built and saved to " + ScenePath);
        }

        // ---------------------------------------------------------------- room

        private static void BuildShell(Transform p, Transform chores)
        {
            var floor = Mat("Floor", "#FBF7DF", 0.15f);
            var plank = Mat("FloorPlank", "#E8DDBC", 0.1f);
            var wall = Mat("Wall", "#EFEBE3", 0.05f);
            var trim = Mat("Trim", "#D9D2C4", 0.1f);
            var rug = Mat("Rug", "#D3E7DE", 0f);
            var stain = Mat("Stain", "#9C917A", 0f);
            var wood = Mat("Wood", "#C9AE8C", 0.2f);
            var metal = Mat("Metal", "#B8BEC2", 0.6f);

            Box("Floor", p, new Vector3(0, -0.05f, 0), new Vector3(6, 0.1f, 5), floor);
            for (int i = 1; i < 20; i++)
                Box("Plank", p, new Vector3(-3 + i * 0.3f, 0.001f, 0), new Vector3(0.01f, 0.002f, 5), plank, false);

            // walls: back (kitchen, bed), left, front (door, TV), right (window)
            Box("Wall Back", p, new Vector3(0, 1.35f, 2.55f), new Vector3(6.2f, 2.7f, 0.1f), wall);
            Box("Wall Left", p, new Vector3(-3.05f, 1.35f, 0), new Vector3(0.1f, 2.7f, 5), wall);
            Box("Wall Front", p, new Vector3(0, 1.35f, -2.55f), new Vector3(6.2f, 2.7f, 0.1f), wall);
            Box("Wall Right A", p, new Vector3(3.05f, 1.35f, -2.025f), new Vector3(0.1f, 2.7f, 0.95f), wall);
            Box("Wall Right B", p, new Vector3(3.05f, 1.35f, 1.125f), new Vector3(0.1f, 2.7f, 2.75f), wall);
            Box("Wall Right Below Window", p, new Vector3(3.05f, 0.45f, -0.9f), new Vector3(0.1f, 0.9f, 1.3f), wall);
            Box("Wall Right Above Window", p, new Vector3(3.05f, 2.45f, -0.9f), new Vector3(0.1f, 0.5f, 1.3f), wall);
            Box("Window Sill", p, new Vector3(2.98f, 0.91f, -0.9f), new Vector3(0.2f, 0.03f, 1.4f), trim);
            Box("Window Mullion", p, new Vector3(3.05f, 1.55f, -0.9f), new Vector3(0.04f, 1.3f, 0.04f), trim);

            Box("Skirting Back", p, new Vector3(0, 0.04f, 2.495f), new Vector3(6, 0.08f, 0.01f), trim, false);
            Box("Skirting Front", p, new Vector3(0, 0.04f, -2.495f), new Vector3(6, 0.08f, 0.01f), trim, false);
            Box("Skirting Left", p, new Vector3(-2.995f, 0.04f, 0), new Vector3(0.01f, 0.08f, 5), trim, false);

            // front door
            Box("Door", p, new Vector3(-2.1f, 1.0f, -2.48f), new Vector3(0.9f, 2.0f, 0.04f), wood);
            Box("Door Frame Top", p, new Vector3(-2.1f, 2.03f, -2.47f), new Vector3(1.0f, 0.06f, 0.06f), trim, false);
            MeshObj("Door Knob", p, new Vector3(-1.75f, 1.0f, -2.45f), Quaternion.Euler(-90, 0, 0),
                Lathe("DoorKnob", 16, V(0, 0), V(0.012f, 0), V(0.012f, 0.03f), V(0.025f, 0.045f), V(0.02f, 0.06f), V(0, 0.062f)), metal);

            Box("Rug", p, new Vector3(0.3f, 0.005f, -0.6f), new Vector3(2.2f, 0.01f, 1.6f), rug, false);

            // a slightly dirty floor
            var disc = Lathe("StainDisc", 24, V(0, 0), V(0.25f, 0), V(0.25f, 0.002f), V(0, 0.002f));
            MeshObj("Stain", p, new Vector3(-0.6f, 0.012f, 0.9f), Quaternion.identity, disc, stain, false, new Vector3(1.2f, 1, 0.8f));
            MeshObj("Stain", p, new Vector3(1.3f, 0.012f, -0.4f), Quaternion.Euler(0, 40, 0), disc, stain, false, new Vector3(0.7f, 1, 1.1f));
            MeshObj("Stain", p, new Vector3(-1.6f, 0.012f, -1.4f), Quaternion.identity, disc, stain, false, new Vector3(0.5f, 1, 0.5f));

            // the note on the wall is a live chore list with a clock
            var paper = Mat("Paper", "#FFFDF5", 0f);
            Box("Note", p, new Vector3(0.6f, 1.45f, 2.495f), new Vector3(0.52f, 0.5f, 0.01f), paper, false);
            var boardText = Label("Chore List Text", chores, new Vector3(0.6f, 1.45f, 2.488f), Quaternion.identity,
                "SATURDAY", 0.22f, new Vector2(0.46f, 0.44f), TextAlignmentOptions.TopLeft);
            var board = boardText.gameObject.AddComponent<ChoreBoard>();
            SetField(board, "_text", boardText);
        }

        private static void BuildKitchen(Transform p, Transform chores)
        {
            var cabinet = Mat("Cabinet", "#9DB0B8", 0.25f);
            var counter = Mat("Countertop", "#F4F2EC", 0.4f);
            var white = Mat("White", "#FAFAF7", 0.4f);
            var dark = Mat("Dark", "#2B2F33", 0.5f);
            var metal = Mat("Metal", "#B8BEC2", 0.6f);
            var sink = Mat("Sink", "#9AA4A9", 0.7f);
            var water = Mat("Water", "#9FD3EC", 0.95f);

            // counter base, split around a real sink basin (x -1.25..-0.75, z 1.98..2.38, 18 cm deep)
            Box("Counter Base L", p, new Vector3(-1.775f, 0.45f, 2.2f), new Vector3(1.05f, 0.9f, 0.6f), cabinet);
            Box("Counter Base R", p, new Vector3(-0.675f, 0.45f, 2.2f), new Vector3(0.15f, 0.9f, 0.6f), cabinet);
            Box("Under Sink", p, new Vector3(-1.0f, 0.35f, 2.2f), new Vector3(0.5f, 0.7f, 0.6f), cabinet);
            Box("Sink Front Panel", p, new Vector3(-1.0f, 0.8f, 1.94f), new Vector3(0.5f, 0.2f, 0.08f), cabinet);
            Box("Sink Back Panel", p, new Vector3(-1.0f, 0.8f, 2.44f), new Vector3(0.5f, 0.2f, 0.12f), cabinet);
            Box("Countertop L", p, new Vector3(-1.785f, 0.92f, 2.19f), new Vector3(1.07f, 0.04f, 0.64f), counter);
            Box("Countertop R", p, new Vector3(-0.665f, 0.92f, 2.19f), new Vector3(0.17f, 0.04f, 0.64f), counter);
            Box("Countertop Front", p, new Vector3(-1.0f, 0.92f, 1.925f), new Vector3(0.5f, 0.04f, 0.11f), counter);
            Box("Countertop Back", p, new Vector3(-1.0f, 0.92f, 2.445f), new Vector3(0.5f, 0.04f, 0.13f), counter);
            Box("Sink Bottom", p, new Vector3(-1.0f, 0.71f, 2.18f), new Vector3(0.5f, 0.02f, 0.4f), sink);
            Box("Sink Wall Front", p, new Vector3(-1.0f, 0.81f, 1.985f), new Vector3(0.5f, 0.2f, 0.01f), sink);
            Box("Sink Wall Back", p, new Vector3(-1.0f, 0.81f, 2.375f), new Vector3(0.5f, 0.2f, 0.01f), sink);
            Box("Sink Wall Left", p, new Vector3(-1.245f, 0.81f, 2.18f), new Vector3(0.01f, 0.2f, 0.4f), sink);
            Box("Sink Wall Right", p, new Vector3(-0.755f, 0.81f, 2.18f), new Vector3(0.01f, 0.2f, 0.4f), sink);
            MeshObj("Drain", p, new Vector3(-1.0f, 0.721f, 2.18f), Quaternion.identity,
                Lathe("Drain", 16, V(0, 0), V(0.03f, 0), V(0.03f, 0.002f), V(0, 0.002f)), dark, false);

            Box("Upper Cabinets", p, new Vector3(-1.45f, 1.85f, 2.33f), new Vector3(1.7f, 0.7f, 0.34f), cabinet);
            for (int i = 0; i < 4; i++)
            {
                float x = -2.1f + i * 0.43f;
                Box("Cabinet Handle", p, new Vector3(x, 0.75f, 1.895f), new Vector3(0.12f, 0.015f, 0.015f), metal, false);
                Box("Cabinet Handle", p, new Vector3(x, 1.58f, 2.155f), new Vector3(0.12f, 0.015f, 0.015f), metal, false);
            }

            // tap, and the water that runs while a dirty dish is in the sink
            MeshObj("Faucet", p, new Vector3(-1.0f, 0.94f, 2.44f), Quaternion.identity,
                Lathe("Faucet", 16, V(0, 0), V(0.025f, 0), V(0.025f, 0.02f), V(0.012f, 0.03f), V(0.012f, 0.25f), V(0, 0.26f)), metal, false);
            Box("Faucet Spout", p, new Vector3(-1.0f, 1.18f, 2.35f), new Vector3(0.024f, 0.024f, 0.2f), metal, false);
            var stream = Cyl("Water Stream", p, new Vector3(-1.0f, 0.945f, 2.26f), 0.012f, 0.46f, water);
            Object.DestroyImmediate(stream.GetComponent<Collider>());
            stream.SetActive(false);

            var zone = new GameObject("Sink Zone (wash dishes here)");
            zone.transform.SetParent(chores, false);
            zone.transform.position = new Vector3(-1.0f, 0.87f, 2.18f);
            zone.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.32f, 0.4f);
            zone.GetComponent<BoxCollider>().isTrigger = true;
            SetField(zone.AddComponent<SinkZone>(), "_water", stream);
            Label("Sink Sign", chores, new Vector3(-1.0f, 1.3f, 2.488f), Quaternion.identity,
                "<b>SINK</b>\n<size=70%>hold or drop dirty dishes under the tap</size>", 0.3f, new Vector2(0.6f, 0.15f));

            // stove
            Box("Stove Top", p, new Vector3(-1.9f, 0.942f, 2.2f), new Vector3(0.56f, 0.006f, 0.5f), dark, false);
            var burner = Lathe("Burner", 24, V(0, 0), V(0.08f, 0), V(0.085f, 0.006f), V(0.06f, 0.008f), V(0, 0.008f));
            foreach (var o in new[] { V(-0.13f, -0.11f), V(0.13f, -0.11f), V(-0.13f, 0.11f), V(0.13f, 0.11f) })
                MeshObj("Burner", p, new Vector3(-1.9f + o.x, 0.945f, 2.2f + o.y), Quaternion.identity, burner, Mat("Burner", "#45494D", 0.3f), false);
            var knob = Lathe("StoveKnob", 16, V(0, 0), V(0.022f, 0), V(0.022f, 0.02f), V(0.012f, 0.028f), V(0, 0.028f));
            for (int i = 0; i < 4; i++)
                MeshObj("Stove Knob", p, new Vector3(-2.08f + i * 0.12f, 0.82f, 1.9f), Quaternion.Euler(-90, 0, 0), knob, white, false);

            // fridge
            Box("Fridge", p, new Vector3(-2.65f, 0.95f, 2.17f), new Vector3(0.66f, 1.9f, 0.66f), white);
            Box("Fridge Gap", p, new Vector3(-2.65f, 1.3f, 1.838f), new Vector3(0.64f, 0.01f, 0.01f), Mat("Trim", "#D9D2C4", 0.1f), false);
            Box("Fridge Handle", p, new Vector3(-2.4f, 1.6f, 1.82f), new Vector3(0.02f, 0.4f, 0.03f), metal, false);
            Box("Fridge Handle", p, new Vector3(-2.4f, 1.0f, 1.82f), new Vector3(0.02f, 0.3f, 0.03f), metal, false);
        }

        private static void BuildWasher(Transform p, Transform chores)
        {
            var white = Mat("White", "#FAFAF7", 0.4f);
            var dark = Mat("Dark", "#2B2F33", 0.5f);
            var glassIdle = Mat("Glass", "#5D6E78", 0.9f);
            var glassWash = Mat("GlassWashing", "#5FA8D3", 0.95f);
            var foam = Mat("Foam", "#FFFFFF", 0.3f);

            var washer = new GameObject("Washing Machine").transform;
            washer.SetParent(p, false);
            washer.position = new Vector3(-0.25f, 0, 2.2f);

            Box("Washer Body", washer, new Vector3(-0.25f, 0.43f, 2.2f), new Vector3(0.6f, 0.86f, 0.6f), white);
            MeshObj("Washer Door Ring", washer, new Vector3(-0.25f, 0.4f, 1.9f), Quaternion.Euler(-90, 0, 0),
                Lathe("WasherRing", 32, V(0.12f, 0), V(0.17f, 0), V(0.17f, 0.025f), V(0.15f, 0.035f), V(0.12f, 0.03f)), Mat("Metal", "#B8BEC2", 0.6f), false);
            var drum = MeshObj("Washer Drum Glass", washer, new Vector3(-0.25f, 0.4f, 1.905f), Quaternion.Euler(-90, 0, 0),
                Lathe("WasherGlass", 32, V(0, 0), V(0.125f, 0), V(0.11f, 0.02f), V(0, 0.025f)), glassIdle, false);
            // a few suds stuck to the glass so you can see the drum spin
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2;
                var s = Sphere("Suds", drum.transform, Vector3.zero, 0.035f, foam, false);
                s.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.07f, 0.02f, Mathf.Sin(a) * 0.07f);
            }
            Box("Washer Panel", washer, new Vector3(-0.25f, 0.78f, 1.899f), new Vector3(0.56f, 0.1f, 0.005f), Mat("Panel", "#2E3A3F", 0.3f), false);

            var display = Label("Washer Display", washer, new Vector3(-0.25f, 0.78f, 1.893f), Quaternion.identity,
                "Drop dirty clothes in here", 0.17f, new Vector2(0.52f, 0.08f), TextAlignmentOptions.Center, "#9FE3C8");

            var output = new GameObject("Washer Output").transform;
            output.SetParent(washer, false);
            output.position = new Vector3(-0.25f, 0.92f, 2.2f);

            var zone = new GameObject("Washer Zone (drop dirty clothes)");
            zone.transform.SetParent(chores, false);
            zone.transform.position = new Vector3(-0.25f, 0.4f, 1.72f);
            var col = zone.AddComponent<BoxCollider>();
            col.size = new Vector3(0.6f, 0.8f, 0.36f);
            col.isTrigger = true;
            var machine = zone.AddComponent<WasherMachine>();
            SetField(machine, "_drum", drum.transform);
            SetField(machine, "_body", washer);
            SetField(machine, "_output", output);
            SetField(machine, "_display", display);
            SetField(machine, "_drumRenderer", drum.GetComponent<Renderer>());
            SetField(machine, "_drumIdle", glassIdle);
            SetField(machine, "_drumWashing", glassWash);

            Label("Washer Sign", chores, new Vector3(-0.25f, 1.18f, 2.488f), Quaternion.identity,
                "<b>WASHING MACHINE</b>\n<size=70%>drop dirty clothes in front of the door</size>", 0.3f, new Vector2(0.6f, 0.15f));
        }

        private static void BuildBed(Transform p, Transform chores)
        {
            var wood = Mat("Wood", "#C9AE8C", 0.2f);
            var sheet = Mat("Sheet", "#FAFAF7", 0.05f);
            var blanketMat = Mat("Blanket", "#B5CBE9", 0.05f);
            var light = Mat("LightWood", "#E5D3B3", 0.2f);
            var shade = Mat("LampShade", "#FFF3D6", 0f);

            var bed = new GameObject("Bed (select to make it)");
            bed.transform.SetParent(chores, false);
            bed.transform.position = new Vector3(2.2f, 0, 1.45f);

            Box("Bed Frame", bed.transform, new Vector3(2.2f, 0.17f, 1.45f), new Vector3(1.45f, 0.24f, 2.05f), wood);
            Box("Mattress", bed.transform, new Vector3(2.2f, 0.4f, 1.45f), new Vector3(1.4f, 0.22f, 2.0f), sheet);
            Box("Headboard", bed.transform, new Vector3(2.2f, 0.7f, 2.47f), new Vector3(1.5f, 1.0f, 0.06f), wood);

            // an unmade blanket: a rumpled rounded slab plus a crumpled fold
            var slab = RoundedBox("Blanket", new Vector3(1.3f, 0.07f, 1.2f), 0.03f, 8, 0.2f);
            var blanket = MeshObj("Blanket (unmade)", bed.transform, new Vector3(2.05f, 0.56f, 0.95f), Quaternion.Euler(4, -18, -9), slab, blanketMat, false);
            var fold = MeshObj("Blanket Fold", bed.transform, new Vector3(2.35f, 0.6f, 1.55f), Quaternion.Euler(-6, 25, 5), slab, blanketMat, false, new Vector3(0.6f, 1.4f, 0.5f));

            // one select volume over the whole bed, used by the VR ray and by desktop mode
            var volume = bed.AddComponent<BoxCollider>();
            volume.center = new Vector3(0, 0.5f, 0);
            volume.size = new Vector3(1.5f, 0.6f, 2.1f);
            volume.isTrigger = true;
            var maker = bed.AddComponent<BedMaker>();
            maker.Setup(blanket.transform, fold.transform, new Vector3(2.2f, 0.545f, 1.25f), Vector3.zero, new Vector3(1.08f, 1, 1.4f));
            var surface = bed.AddComponent<ColliderSurface>();
            surface.InjectCollider(volume);
            bed.AddComponent<RayInteractable>().InjectSurface(surface);
            bed.AddComponent<TaskClickable>().SetTarget(maker);

            Label("Bed Sign", chores, new Vector3(2.2f, 1.05f, 2.435f), Quaternion.identity,
                "<b>MAKE THE BED</b>\n<size=70%>point at the bed + trigger / pinch</size>", 0.3f, new Vector2(0.8f, 0.15f));

            // nightstand + lamp
            Box("Nightstand", p, new Vector3(1.15f, 0.25f, 2.25f), new Vector3(0.45f, 0.5f, 0.4f), light);
            Box("Nightstand Drawer", p, new Vector3(1.15f, 0.32f, 2.048f), new Vector3(0.38f, 0.14f, 0.005f), wood, false);
            MeshObj("Lamp Base", p, new Vector3(1.15f, 0.5f, 2.3f), Quaternion.identity,
                Lathe("LampBase", 24, V(0, 0), V(0.07f, 0), V(0.07f, 0.015f), V(0.015f, 0.03f), V(0.012f, 0.3f), V(0, 0.3f)), Mat("White", "#FAFAF7", 0.4f));
            MeshObj("Lamp Shade", p, new Vector3(1.15f, 0.72f, 2.3f), Quaternion.identity,
                Lathe("LampShade", 32, V(0.13f, 0), V(0.135f, 0), V(0.085f, 0.17f), V(0.08f, 0.17f), V(0.125f, 0.005f)), shade, false);
            var lamp = new GameObject("Lamp Light").AddComponent<Light>();
            lamp.transform.SetParent(p);
            lamp.transform.position = new Vector3(1.15f, 0.8f, 2.25f);
            lamp.type = LightType.Point;
            lamp.range = 2.5f;
            lamp.intensity = 1.2f;
            lamp.color = Hex("#FFD9A0");
        }

        private static void BuildDryingRack(Transform p, Transform chores)
        {
            var frame = Mat("RackFrame", "#F4F4F2", 0.5f);
            var rack = new GameObject("Drying Rack").transform;
            rack.SetParent(p, false);
            var c = new Vector3(2.25f, 0, -1.0f);
            rack.position = c;

            // two A-frame ends with three bars between them (like the one in the 3D prototype)
            foreach (var dz in new[] { -0.5f, 0.5f })
            {
                foreach (var dx in new[] { -0.22f, 0.22f })
                {
                    var leg = Box("Rack Leg", rack, c + new Vector3(dx * 0.6f, 0.48f, dz), new Vector3(0.025f, 0.98f, 0.025f), frame);
                    leg.transform.rotation = Quaternion.Euler(0, 0, dx > 0 ? -12 : 12);
                }
                Box("Rack Top", rack, c + new Vector3(0, 0.96f, dz), new Vector3(0.42f, 0.025f, 0.025f), frame);
            }
            var slots = new List<Transform>();
            foreach (var dx in new[] { -0.18f, 0f, 0.18f })
            {
                var bar = Cyl("Rack Bar", rack, c + new Vector3(dx, 0.97f, 0), 0.008f, 1.0f, frame);
                bar.transform.rotation = Quaternion.Euler(90, 0, 0);
                foreach (var dz in new[] { -0.22f, 0.22f })
                {
                    var slot = new GameObject("Hang Slot").transform;
                    slot.SetParent(rack, false);
                    slot.SetPositionAndRotation(c + new Vector3(dx, 0.83f, dz), Quaternion.Euler(0, 0, 90));
                    slots.Add(slot);
                }
            }

            var zone = new GameObject("Drying Rack Zone (let go of wet clothes)");
            zone.transform.SetParent(chores, false);
            zone.transform.position = c + new Vector3(0, 0.75f, 0);
            var col = zone.AddComponent<BoxCollider>();
            col.size = new Vector3(0.9f, 1.0f, 1.3f);
            col.isTrigger = true;
            zone.AddComponent<DryingRack>().SetSlots(slots.ToArray());

            Label("Rack Sign", chores, new Vector3(2.99f, 2.35f, -0.9f), Quaternion.Euler(0, 90, 0),
                "<b>DRYING RACK</b>\n<size=70%>let go of wet clothes next to it</size>", 0.3f, new Vector2(0.9f, 0.15f));
        }

        private static void BuildLivingArea(Transform p)
        {
            var light = Mat("LightWood", "#E5D3B3", 0.2f);
            var wood = Mat("Wood", "#C9AE8C", 0.2f);
            var clay = Mat("Clay", "#F2EBDD", 0.1f);
            var dark = Mat("Dark", "#2B2F33", 0.5f);
            var terra = Mat("Terracotta", "#D9A083", 0.1f);
            var leaf = Mat("Leaf", "#A9C79A", 0.05f);

            // dining table with two clay stools (like the paper prototype)
            Box("Table Top", p, new Vector3(-1.2f, 0.74f, -0.2f), new Vector3(1.2f, 0.05f, 0.8f), light);
            foreach (var o in new[] { V(-0.52f, -0.32f), V(0.52f, -0.32f), V(-0.52f, 0.32f), V(0.52f, 0.32f) })
                Cyl("Table Leg", p, new Vector3(-1.2f + o.x, 0.36f, -0.2f + o.y), 0.025f, 0.72f, wood);
            var seat = Lathe("StoolSeat", 32, V(0, 0), V(0.16f, 0), V(0.18f, 0.025f), V(0.17f, 0.06f), V(0.1f, 0.075f), V(0, 0.078f));
            foreach (var z in new[] { -0.85f, 0.45f })
            {
                MeshObj("Stool Seat", p, new Vector3(-1.2f, 0.45f, z), Quaternion.identity, seat, clay);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * Mathf.PI * 2 / 3;
                    var leg = Cyl("Stool Leg", p, new Vector3(-1.2f + Mathf.Cos(a) * 0.1f, 0.225f, z + Mathf.Sin(a) * 0.1f), 0.015f, 0.46f, wood);
                    leg.transform.rotation = Quaternion.Euler(Mathf.Sin(a) * 8, 0, -Mathf.Cos(a) * 8);
                }
            }

            // TV corner
            Box("TV Stand", p, new Vector3(1.2f, 0.25f, -2.25f), new Vector3(1.4f, 0.5f, 0.4f), wood);
            Box("TV", p, new Vector3(1.2f, 1.25f, -2.47f), new Vector3(1.3f, 0.75f, 0.04f), dark);
            Box("TV Screen", p, new Vector3(1.2f, 1.25f, -2.448f), new Vector3(1.24f, 0.69f, 0.002f), Mat("Screen", "#3A4A55", 0.95f), false);

            // plant in the corner
            MeshObj("Plant Pot", p, new Vector3(2.65f, 0, -2.15f), Quaternion.identity,
                Lathe("PlantPot", 24, V(0, 0), V(0.13f, 0), V(0.18f, 0.32f), V(0.19f, 0.34f), V(0.17f, 0.34f), V(0, 0.3f)), terra);
            Sphere("Foliage", p, new Vector3(2.65f, 0.62f, -2.15f), 0.48f, leaf);
            Sphere("Foliage", p, new Vector3(2.55f, 0.85f, -2.05f), 0.32f, leaf);
            Sphere("Foliage", p, new Vector3(2.78f, 0.78f, -2.25f), 0.3f, leaf);
        }

        private static void BuildLighting()
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(55, -35, 0);
            sun.color = Hex("#FFF2DC");
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#CFE0EA");
            RenderSettings.ambientEquatorColor = Hex("#EFE6D6");
            RenderSettings.ambientGroundColor = Hex("#B9AE98");
        }

        // ---------------------------------------------------------------- grabbables

        private static void BuildGrabbables(Transform p, GameObject t)
        {
            var glass = Mat("BottleGlass", "#4F8A6B", 0.85f);
            var vase = Mat("Vase", "#9B8CE0", 0.5f);
            var basket = Mat("Basket", "#E5D3B3", 0.1f);
            var apple = Mat("Apple", "#D9534F", 0.55f);
            var phone = Mat("Phone", "#1E2124", 0.8f);
            var pillow = Mat("Pillow", "#FAFAF7", 0.05f);

            var plateMesh = Lathe("Plate", 40, V(0, 0), V(0.07f, 0), V(0.075f, 0.005f), V(0.12f, 0.018f), V(0.125f, 0.022f),
                V(0.12f, 0.025f), V(0.072f, 0.011f), V(0, 0.011f));
            var bowlMesh = Lathe("Bowl", 40, V(0, 0), V(0.04f, 0), V(0.045f, 0.005f), V(0.07f, 0.03f), V(0.08f, 0.065f),
                V(0.075f, 0.067f), V(0.066f, 0.034f), V(0.04f, 0.012f), V(0, 0.012f));
            var mugMesh = Combine("Mug",
                (Lathe("MugBody", 32, V(0, 0), V(0.04f, 0), V(0.042f, 0.004f), V(0.042f, 0.095f), V(0.038f, 0.095f), V(0.038f, 0.008f), V(0, 0.008f)), Matrix4x4.identity),
                (TorusArc("MugHandle", new Vector3(0.04f, 0.05f, 0), 0.026f, 0.007f, -90, 90, 16, 10), Matrix4x4.identity));
            var bottleMesh = Lathe("Bottle", 32, V(0, 0), V(0.035f, 0), V(0.037f, 0.004f), V(0.037f, 0.17f), V(0.03f, 0.2f),
                V(0.013f, 0.235f), V(0.013f, 0.27f), V(0.016f, 0.272f), V(0.016f, 0.285f), V(0, 0.285f));
            var panMesh = Combine("Pan",
                (Lathe("PanBody", 40, V(0, 0), V(0.1f, 0), V(0.11f, 0.005f), V(0.125f, 0.045f), V(0.118f, 0.046f), V(0.1f, 0.012f), V(0, 0.012f)), Matrix4x4.identity),
                (RoundedBox("PanHandle", new Vector3(0.17f, 0.02f, 0.032f), 0.009f, 3), Matrix4x4.Translate(new Vector3(0.2f, 0.035f, 0))));
            var vaseMesh = Lathe("Vase", 32, V(0, 0), V(0.04f, 0), V(0.062f, 0.06f), V(0.066f, 0.1f), V(0.045f, 0.17f), V(0.032f, 0.2f),
                V(0.04f, 0.225f), V(0.034f, 0.226f), V(0, 0.2f));
            var appleMesh = AppleMesh();
            var pillowMesh = RoundedBox("Pillow", new Vector3(0.6f, 0.15f, 0.4f), 0.06f, 8, 0.45f);
            var clothesMesh = RoundedBox("FoldedClothes", new Vector3(0.3f, 0.06f, 0.25f), 0.02f, 5, 0.15f);
            var phoneMesh = RoundedBox("Phone", new Vector3(0.075f, 0.009f, 0.155f), 0.007f, 4);
            var basketMesh = Lathe("LaundryBasket", 28, V(0, 0), V(0.2f, 0), V(0.205f, 0.005f), V(0.23f, 0.32f), V(0.215f, 0.32f), V(0.19f, 0.015f), V(0, 0.015f));

            // dirty dishes on the table and the counter: each one has a dirty and a clean look
            void DishItem(string name, Mesh mesh, string cleanHex, float smooth, Vector3 pos, Vector3 euler, float mass)
            {
                var clean = Mat(name + "Clean", cleanHex, smooth);
                var dirty = MatC(name + "Dirty", Color.Lerp(Hex(cleanHex), Hex("#8A6E4B"), 0.45f), 0.1f);
                var go = Grab(t, p, name, mesh, dirty, pos, euler, mass);
                go.AddComponent<Dish>().Setup(dirty, clean);
            }
            for (int i = 0; i < 3; i++)
                DishItem("Plate", plateMesh, "#FFFFFF", 0.7f, new Vector3(-1.4f, 0.77f + i * 0.016f, -0.3f), new Vector3(0, i * 20, 0), 0.4f);
            DishItem("Plate", plateMesh, "#FFFFFF", 0.7f, new Vector3(-0.95f, 0.77f, 0.0f), Vector3.zero, 0.4f);
            DishItem("Plate", plateMesh, "#FFFFFF", 0.7f, new Vector3(-1.5f, 0.945f, 2.15f), Vector3.zero, 0.4f);
            DishItem("Bowl", bowlMesh, "#FFFFFF", 0.7f, new Vector3(-1.6f, 0.945f, 2.32f), Vector3.zero, 0.35f);
            DishItem("Mug Blue", mugMesh, "#6C8EBF", 0.6f, new Vector3(-1.6f, 0.77f, 0.0f), new Vector3(0, 30, 0), 0.3f);
            DishItem("Mug Coral", mugMesh, "#E3876A", 0.6f, new Vector3(-1.75f, 0.945f, 2.05f), new Vector3(0, -60, 0), 0.3f);
            DishItem("Frying Pan", panMesh, "#3B3F44", 0.5f, new Vector3(-2.03f, 0.96f, 2.09f), new Vector3(0, 200, 0), 0.8f);

            // fruit bowl (clean) and other things to pick up
            Grab(t, p, "Fruit Bowl", bowlMesh, Mat("BowlFruit", "#F2D57E", 0.5f), new Vector3(-0.9f, 0.77f, -0.45f), Vector3.zero, 0.35f);
            for (int i = 0; i < 3; i++)
                Grab(t, p, "Apple", appleMesh, apple, new Vector3(-0.9f + (i - 1) * 0.06f, 0.8f + i * 0.01f, -0.45f + (i % 2) * 0.03f), new Vector3(0, i * 70, 0), 0.15f);
            Grab(t, p, "Bottle", bottleMesh, glass, new Vector3(-1.05f, 0.77f, -0.05f), Vector3.zero, 0.5f);
            Grab(t, p, "Phone", phoneMesh, phone, new Vector3(1.05f, 0.51f, 2.18f), new Vector3(0, 20, 0), 0.2f);
            Grab(t, p, "Vase", vaseMesh, vase, new Vector3(0.75f, 0.51f, -2.25f), Vector3.zero, 0.5f);
            Grab(t, p, "Pillow", pillowMesh, pillow, new Vector3(1.95f, 0.6f, 2.15f), new Vector3(0, 8, 0), 0.4f);
            Grab(t, p, "Pillow", pillowMesh, pillow, new Vector3(1.3f, 0.09f, 0.7f), new Vector3(0, -35, 6), 0.4f);

            // laundry: three pieces in the basket, two on the floor
            var b = Grab(t, p, "Laundry Basket", basketMesh, basket, new Vector3(0.35f, 0.01f, 2.05f), Vector3.zero, 0.8f, false);
            AddBasketColliders(b);
            var clothes = new[]
            {
                ("Lavender", "#9B8CE0", new Vector3(0.35f, 0.07f, 2.05f), 10f),
                ("Teal", "#6FB8A3", new Vector3(0.35f, 0.14f, 2.05f), 55f),
                ("Yellow", "#F2D57E", new Vector3(0.35f, 0.21f, 2.05f), -30f),
                ("Coral", "#E3876A", new Vector3(0.9f, 0.04f, 1.0f), 37f),
                ("Sky", "#8DB6E8", new Vector3(1.55f, 0.04f, 0.05f), 74f),
            };
            foreach (var (name, hex, pos, yaw) in clothes)
            {
                var dry = Mat("Cloth" + name, hex, 0.05f);
                var dirty = MatC("Cloth" + name + "Dirty", Color.Lerp(Hex(hex), Hex("#7D7466"), 0.5f), 0f);
                var wet = MatC("Cloth" + name + "Wet", Hex(hex) * 0.72f, 0.65f);
                var go = Grab(t, p, "Clothes " + name, clothesMesh, dirty, pos, new Vector3(0, yaw, 0), 0.25f);
                go.AddComponent<Garment>().Setup(dirty, wet, dry);
            }
        }

        /// <summary>Clones the Grab Interaction Building Block and swaps in our own mesh, material and collider.</summary>
        private static GameObject Grab(GameObject template, Transform parent, string name, Mesh mesh, Material mat,
            Vector3 pos, Vector3 euler, float mass, bool convexCollider = true)
        {
            var go = Object.Instantiate(template);
            go.SetActive(true);
            go.name = "[Grab] " + name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            go.transform.localScale = Vector3.one;

            // the clones are copies of a block, not new blocks: keep the BuildingBlock tag on the original only
            foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (c != null && c.GetType().Name == "BuildingBlock") Object.DestroyImmediate(c);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            foreach (var col in go.GetComponents<Collider>()) Object.DestroyImmediate(col);
            if (convexCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            go.AddComponent<RespawnIfFallen>();
            AddDistanceGrab(go);
            return go;
        }

        /// <summary>
        /// Lets the hands / controllers of the Building Blocks rig grab this object from a distance:
        /// point at it (a reticle appears), pinch or press grip, and it flies to the hand.
        /// </summary>
        private static void AddDistanceGrab(GameObject go)
        {
            var grabbable = go.GetComponent<Grabbable>();
            var rb = go.GetComponent<Rigidbody>();
            if (grabbable == null || rb == null) return;
            var old = go.transform.Find("Distance Grab");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var child = new GameObject("Distance Grab");
            child.transform.SetParent(go.transform, false);
            var dg = child.AddComponent<Oculus.Interaction.HandGrab.DistanceHandGrabInteractable>();
            dg.InjectAllDistanceHandGrabInteractable(Oculus.Interaction.Grab.GrabTypeFlags.All, rb,
                Oculus.Interaction.GrabAPI.GrabbingRule.DefaultPinchRule, Oculus.Interaction.GrabAPI.GrabbingRule.DefaultPalmRule);
            dg.InjectOptionalPointableElement(grabbable);
            EditorUtility.SetDirty(child);
        }

        [MenuItem("Saturday Studio/Add Distance Grab To Objects")]
        private static void AddDistanceGrabToScene()
        {
            int n = 0;
            foreach (var g in Object.FindObjectsByType<Grabbable>(FindObjectsInactive.Include))
            {
                if (!g.name.StartsWith("[Grab] ")) continue;
                AddDistanceGrab(g.gameObject);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorUtility.DisplayDialog("Saturday Studio", $"Distance grab added to {n} objects and the scene was saved.", "OK");
        }

        /// <summary>The basket gets a compound collider (bottom + ring of walls) so clothes can sit inside it.</summary>
        private static void AddBasketColliders(GameObject basket)
        {
            var bottom = new GameObject("Collider Bottom");
            bottom.transform.SetParent(basket.transform, false);
            bottom.transform.localPosition = new Vector3(0, 0.008f, 0);
            bottom.AddComponent<BoxCollider>().size = new Vector3(0.38f, 0.016f, 0.38f);
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var w = new GameObject("Collider Wall " + i);
                w.transform.SetParent(basket.transform, false);
                w.transform.localPosition = dir * 0.215f + Vector3.up * 0.16f;
                w.transform.localRotation = Quaternion.LookRotation(dir);
                w.AddComponent<BoxCollider>().size = new Vector3(0.13f, 0.32f, 0.015f);
            }
        }

        private static GameObject FindTemplate()
        {
            GameObject byType = null;
            foreach (var rootGo in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var tr in rootGo.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name == "[BuildingBlock] Cube" && tr.GetComponent<Rigidbody>() != null) return tr.gameObject;
                    if (byType == null && tr.GetComponent<MeshFilter>() != null && tr.GetComponent<Grabbable>() != null)
                        byType = tr.gameObject;
                }
            }
            return byType;
        }

        private static void PlaceRig()
        {
            foreach (var rootGo in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootGo.name.Contains("Camera Rig"))
                    rootGo.transform.SetPositionAndRotation(new Vector3(0.2f, 0, -1.4f), Quaternion.identity);
                // a leftover desktop camera fights with the headset camera
                if (rootGo.name == "Main Camera") rootGo.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- scene objects

        private static GameObject Box(string name, Transform p, Vector3 pos, Vector3 size, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Setup(go, name, p, pos, m, collider);
            go.transform.localScale = size;
            return go;
        }

        private static GameObject Cyl(string name, Transform p, Vector3 pos, float radius, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Setup(go, name, p, pos, m, true);
            go.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            return go;
        }

        private static GameObject Sphere(string name, Transform p, Vector3 pos, float diameter, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Setup(go, name, p, pos, m, collider);
            go.transform.localScale = Vector3.one * diameter;
            return go;
        }

        private static GameObject MeshObj(string name, Transform p, Vector3 pos, Quaternion rot, Mesh mesh, Material m,
            bool collider = true, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Setup(go, name, p, pos, m, collider);
            go.transform.rotation = rot;
            go.transform.localScale = scale ?? Vector3.one;
            return go;
        }

        private static void Setup(GameObject go, string name, Transform p, Vector3 pos, Material m, bool collider)
        {
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
        }

        /// <summary>World-space TextMeshPro label (renders in both eyes on the headset, unlike legacy TextMesh).</summary>
        private static TextMeshPro Label(string name, Transform p, Vector3 pos, Quaternion rot, string text, float size, Vector2 box,
            TextAlignmentOptions align = TextAlignmentOptions.Center, string color = "#2B302D")
        {
            var go = new GameObject(name);
            go.transform.SetParent(p, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Hex(color);
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.rectTransform.sizeDelta = box;
            return tmp;
        }

        private static void SetField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- procedural meshes

        private static Vector2 V(float r, float y) => new Vector2(r, y);

        /// <summary>Revolves a (radius, height) profile around the Y axis. Profiles run bottom-centre → outside → rim → inside.</summary>
        private static Mesh Lathe(string name, int seg, params Vector2[] prof)
        {
            if (Meshes.TryGetValue(name, out var cached)) return cached;
            var v = new List<Vector3>();
            var t = new List<int>();
            int n = prof.Length;
            for (int j = 0; j <= seg; j++)
            {
                float a = j * Mathf.PI * 2 / seg;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                foreach (var pt in prof) v.Add(new Vector3(pt.x * c, pt.y, pt.x * s));
            }
            for (int j = 0; j < seg; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = (j + 1) * n + i, c = a + 1, d = b + 1;
                    t.Add(a); t.Add(c); t.Add(b);
                    t.Add(b); t.Add(c); t.Add(d);
                }
            return Save(name, v, t);
        }

        /// <summary>A tube bent along an arc in the XY plane (mug handle).</summary>
        private static Mesh TorusArc(string name, Vector3 centre, float radius, float tube, float fromDeg, float toDeg, int segU, int segV)
        {
            if (Meshes.TryGetValue(name, out var cached)) return cached;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int iu = 0; iu <= segU; iu++)
            {
                float u = Mathf.Lerp(fromDeg, toDeg, (float)iu / segU) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0);
                var c = centre + dir * radius;
                for (int iv = 0; iv <= segV; iv++)
                {
                    float a = iv * Mathf.PI * 2 / segV;
                    v.Add(c + tube * (Mathf.Cos(a) * dir + Mathf.Sin(a) * Vector3.forward));
                }
            }
            int stride = segV + 1;
            for (int iu = 0; iu < segU; iu++)
                for (int iv = 0; iv < segV; iv++)
                {
                    int a = iu * stride + iv, b = a + 1, c = a + stride, d = c + 1;
                    t.Add(a); t.Add(c); t.Add(b);
                    t.Add(b); t.Add(c); t.Add(d);
                }
            // make sure the faces point out of the tube
            var first = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            var outward = v[t[0]] - (centre + new Vector3(Mathf.Cos(fromDeg * Mathf.Deg2Rad), Mathf.Sin(fromDeg * Mathf.Deg2Rad), 0) * radius);
            if (Vector3.Dot(first, outward) < 0) Flip(t);
            return Save(name, v, t);
        }

        /// <summary>A box with rounded edges; puff squashes the edges so it reads as soft (pillows, clothes).</summary>
        private static Mesh RoundedBox(string name, Vector3 size, float r, int n, float puff = 0)
        {
            if (Meshes.TryGetValue(name, out var cached)) return cached;
            var half = size * 0.5f;
            var inner = new Vector3(Mathf.Max(0, half.x - r), Mathf.Max(0, half.y - r), Mathf.Max(0, half.z - r));
            var faces = new[]
            {
                (Vector3.right, Vector3.up, Vector3.forward), (Vector3.left, Vector3.forward, Vector3.up),
                (Vector3.up, Vector3.forward, Vector3.right), (Vector3.down, Vector3.right, Vector3.forward),
                (Vector3.forward, Vector3.right, Vector3.up), (Vector3.back, Vector3.up, Vector3.right),
            };
            var v = new List<Vector3>();
            var t = new List<int>();
            foreach (var (nrm, u, w) in faces)
            {
                int start = v.Count;
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++)
                    {
                        var q = Vector3.Scale(nrm + u * (i * 2f / n - 1) + w * (j * 2f / n - 1), half);
                        var core = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y), Mathf.Clamp(q.z, -inner.z, inner.z));
                        var off = q - core;
                        var pos = off.sqrMagnitude > 1e-10f ? core + off.normalized * r : q;
                        if (puff > 0)
                        {
                            float edge = Mathf.Max(Mathf.Abs(pos.x) / half.x, Mathf.Abs(pos.z) / half.z);
                            pos.y *= 1 - puff * edge * edge * edge;
                        }
                        v.Add(pos);
                    }
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int p00 = start + j * (n + 1) + i, p10 = p00 + 1, p01 = p00 + n + 1, p11 = p01 + 1;
                        t.Add(p00); t.Add(p10); t.Add(p01);
                        t.Add(p10); t.Add(p11); t.Add(p01);
                    }
            }
            return Save(name, v, t);
        }

        private static Mesh AppleMesh()
        {
            var prof = new List<Vector2>();
            const int steps = 14;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI * i / steps;
                float r = 0.042f * Mathf.Sin(a) * (1 + 0.1f * Mathf.Sin(a * 0.5f));
                float y = 0.04f * (1 - Mathf.Cos(a));
                if (i == 0) y = 0.006f;           // small dimple at the bottom
                if (i == steps) y = 0.07f;        // and at the top
                prof.Add(new Vector2(r, y));
            }
            var body = Lathe("AppleBody", 24, prof.ToArray());
            var stem = Lathe("AppleStem", 8, V(0, 0), V(0.003f, 0), V(0.0025f, 0.018f), V(0, 0.018f));
            return Combine("Apple", (body, Matrix4x4.identity), (stem, Matrix4x4.TRS(new Vector3(0, 0.068f, 0), Quaternion.Euler(0, 0, 12), Vector3.one)));
        }

        private static Mesh Combine(string name, params (Mesh mesh, Matrix4x4 m)[] parts)
        {
            if (Meshes.TryGetValue(name, out var cached)) return cached;
            var ci = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++) ci[i] = new CombineInstance { mesh = parts[i].mesh, transform = parts[i].m };
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(ci, true, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return Store(name, mesh);
        }

        private static void Flip(List<int> t)
        {
            for (int i = 0; i < t.Count; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
        }

        private static Mesh Save(string name, List<Vector3> v, List<int> t)
        {
            if (Meshes.TryGetValue(name, out var cached)) return cached;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return Store(name, mesh);
        }

        private static Mesh Store(string name, Mesh mesh)
        {
            var path = $"{Root}/Meshes/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            Meshes[name] = mesh;
            return mesh;
        }

        // ---------------------------------------------------------------- assets

        private static Material Mat(string name, string hex, float smoothness) => MatC(name, Hex(hex), smoothness);

        private static Material MatC(string name, Color c, float smoothness)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            c.a = 1;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void AddSceneToBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
