using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SaturdayStudio.EditorTools
{
    /// <summary>
    /// Links the apartment model's materials to its texture files. The room textures have the lighting baked in, so they
    /// use URP Unlit (they look exactly as rendered by the artist and need no lights); plants get alpha cut-outs.
    /// The materials are saved in Assets/SaturdayStudio/ApartmentMaterials and remapped on the FBX importer.
    /// </summary>
    public static class ApartmentMaterials
    {
        private const string Tex = "Assets/VR_3d_ASSETS/apartamento/textures/";
        private const string Out = "Assets/SaturdayStudio/ApartmentMaterials";

        // FBX material name → texture (and optional alpha texture)
        private static readonly Dictionary<string, (string tex, string alpha)> Map = new Dictionary<string, (string, string)>
        {
            { "Material #122", ("1_Structure.jpeg", null) },
            { "Material #123", ("2_Door_Windows.jpeg", null) },
            { "Material #124", ("3_Bathroom.jpeg", null) },
            { "Material #126", ("3_Bedroom.jpeg", null) },
            { "Material #129", ("3_Kitchen.jpeg", null) },
            { "Material #130", ("3_Living Room.jpeg", null) },
            { "Material #131", ("3_Picture.jpeg", null) },
            { "Material #133", ("3_Closet.jpeg", null) },
            { "Material #136", ("7_Books_21_Diffuse.jpeg", null) },
            { "Material #137", ("7_Books_10_Diffuse.jpeg", null) },
            { "Material #138", ("7_Vase.jpeg", null) },
            { "Material #139", ("7_Tablet_Self-Illumination.jpeg", null) },
            { "Material #140", ("7_Coffee machine_Diffuse.jpeg", null) },
            { "Material #141", ("7_Coffee_Pot_Diffuse.jpeg", null) },
            { "Material #142", ("7_Sugar_Pot_Diffuse.jpeg", null) },
            { "Material #143", ("7_Chocolate_Pot_Diffuse.jpeg", null) },
            { "Material #144", ("7_Meat Board.jpeg", null) },
            { "7_Dinner", ("7_Dinner.jpeg", null) },
            { "7_Knifes", ("7_Knifes.jpeg", null) },
            { "7_Magazines", ("7_Magazines.jpeg", null) },
            { "5_Vegetation_Palm", ("5_Vegetation_Palm_Diffuse.jpeg", "5_Vegetation_Palm_Alpha.jpeg") },
            { "7_Vegetation_Green Plant", ("7_Vegetation_Green Plant_Diffuse.jpeg", "7_Vegetation_Green Plant_Alpha.jpeg") },
            { "7_Vegetation_Red Plant", ("berberys_Red_Diffuse.jpeg", null) },
        };

        [MenuItem("Saturday Studio/Apartment/Link Textures")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Out)) AssetDatabase.CreateFolder("Assets/SaturdayStudio", "ApartmentMaterials");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var importer = (ModelImporter)AssetImporter.GetAtPath(ApartmentInspector.ModelPath);

            foreach (var kv in Map)
            {
                string safe = kv.Key.Replace("#", "").Replace(" ", "_");
                string path = $"{Out}/{safe}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(unlit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = unlit;
                var tex = kv.Value.alpha == null
                    ? AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + kv.Value.tex)
                    : CombineAlpha(Tex + kv.Value.tex, Tex + kv.Value.alpha, $"{Out}/{safe}_RGBA.png");
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                if (kv.Value.alpha != null)
                {
                    mat.SetFloat("_AlphaClip", 1);
                    mat.SetFloat("_Cutoff", 0.5f);
                    mat.SetFloat("_Cull", 0); // leaves are seen from both sides
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), mat);
            }
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
        }

        /// <summary>Puts a separate black/white alpha texture into the alpha channel of the colour texture.</summary>
        private static Texture2D CombineAlpha(string colorPath, string alphaPath, string outPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (existing != null) return existing;
            var c = ReadPixels(colorPath, out int w, out int h);
            var a = ReadPixels(alphaPath, out int aw, out int ah);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var col = c[y * w + x];
                col.a = a[(y * ah / h) * aw + x * aw / w].grayscale;
                px[y * w + x] = col;
            }
            tex.SetPixels(px);
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(outPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static Color[] ReadPixels(string path, out int w, out int h)
        {
            var t = new Texture2D(2, 2);
            t.LoadImage(File.ReadAllBytes(path));
            w = t.width;
            h = t.height;
            var px = t.GetPixels();
            Object.DestroyImmediate(t);
            return px;
        }
    }
}
