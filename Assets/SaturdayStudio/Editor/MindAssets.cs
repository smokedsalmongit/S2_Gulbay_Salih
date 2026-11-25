using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SaturdayStudio.EditorTools
{
    /// <summary>
    /// Creates the assets the mind effects need in a build: a Volume profile with Depth of Field, Color Adjustments and
    /// Vignette (URP strips post effects that no asset uses), and an emissive material so the glow variant is kept.
    /// </summary>
    [InitializeOnLoad]
    public static class MindAssets
    {
        private const string Folder = "Assets/SaturdayStudio/Resources";
        private const string ProfilePath = Folder + "/MindVolumeProfile.asset";
        private const string GlowPath = Folder + "/MindGlow.mat";

        static MindAssets() => EditorApplication.delayCall += Ensure;

        [MenuItem("Saturday Studio/Create Mind Effect Assets")]
        public static void Ensure()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SaturdayStudio", "Resources");

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) == null)
            {
                var p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, ProfilePath);
                var dof = p.Add<DepthOfField>(true);
                dof.mode.Override(DepthOfFieldMode.Gaussian);
                p.Add<ColorAdjustments>(true);
                p.Add<Vignette>(true);
                foreach (var c in p.components)
                {
                    c.name = c.GetType().Name;
                    AssetDatabase.AddObjectToAsset(c, p);
                }
                EditorUtility.SetDirty(p);
                AssetDatabase.SaveAssets();
            }

            if (AssetDatabase.LoadAssetAtPath<Material>(GlowPath) == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader != null)
                {
                    var m = new Material(shader);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.white * 0.5f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    AssetDatabase.CreateAsset(m, GlowPath);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
