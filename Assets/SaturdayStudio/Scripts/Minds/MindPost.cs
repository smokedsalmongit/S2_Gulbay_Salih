using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SaturdayStudio
{
    /// <summary>
    /// What the mind does to vision: VS1 zone-outs blur and grey the room; VS2 anxiety narrows the view
    /// (vignette), tinted brown while the hands feel contaminated.
    /// Uses a URP global Volume; the profile asset in Resources keeps these effects in the Quest build.
    /// </summary>
    public class MindPost : MonoBehaviour
    {
        public static MindPost Instance { get; private set; }

        /// <summary>0 = present, 1 = fully zoned out.</summary>
        public float ZoneOut;
        /// <summary>0..1, how narrow the vision is.</summary>
        public float Narrow;
        /// <summary>0..1, brown tint of the vignette while the hands feel dirty.</summary>
        public float Dirt;
        /// <summary>0..1 fade to black (between the introduction and the apartment).</summary>
        public float Fade;

        private ColorAdjustments _color;
        private Vignette _vignette;
        private DepthOfField _dof;
        private float _zoneShown, _narrowShown, _dirtShown;

        private void Awake()
        {
            Instance = this;
            // a fresh runtime profile (the asset in Resources only makes sure URP keeps these effects in the build)
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = Get<ColorAdjustments>(profile);
            _vignette = Get<Vignette>(profile);
            _dof = Get<DepthOfField>(profile);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianMaxRadius.Override(1.5f);
            _dof.highQualitySampling.Override(false);
            // turning post-processing on for the camera also wakes up the scene's old bloom, which makes the room hazy
            Get<Bloom>(profile).intensity.Override(0f);
            _vignette.smoothness.Override(0.75f);
            _vignette.rounded.Override(true);

            var vol = gameObject.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 50;
            vol.sharedProfile = profile;
            Apply();
        }

        private static T Get<T>(VolumeProfile p) where T : VolumeComponent
        {
            if (!p.TryGet(out T c)) c = p.Add<T>(true);
            c.active = true;
            return c;
        }

        private void LateUpdate()
        {
            float k = 1 - Mathf.Exp(-Time.deltaTime * 2.5f);
            // drifting away is gradual, snapping back is quick
            _zoneShown = Mathf.Lerp(_zoneShown, ZoneOut, ZoneOut > _zoneShown ? k : 1 - Mathf.Exp(-Time.deltaTime * 7f));
            _narrowShown = Mathf.Lerp(_narrowShown, Narrow, k);
            _dirtShown = Mathf.Lerp(_dirtShown, Dirt, k);

            // post-processing only while an effect is showing: the normal view stays crisp (and the Quest stays fast)
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                bool need = _zoneShown > 0.02f || _narrowShown > 0.02f || Fade > 0.001f;
                if (data != null && data.renderPostProcessing != need) data.renderPostProcessing = need;
            }
            Apply();
        }

        private void Apply()
        {
            float z = _zoneShown;
            _color.saturation.Override(Mathf.Lerp(0, -85, z));
            _color.postExposure.Override(Mathf.Lerp(0, -0.35f, z));
            _color.colorFilter.Override(Color.Lerp(Color.white, Color.black, Fade));
            _dof.active = z > 0.03f;
            _dof.gaussianStart.Override(Mathf.Lerp(20f, 0.2f, z));
            _dof.gaussianEnd.Override(Mathf.Lerp(30f, 1.8f, z));
            _vignette.intensity.Override(Mathf.Clamp01(Mathf.Max(_narrowShown * 0.62f, z * 0.3f)));
            _vignette.color.Override(Color.Lerp(Color.black, new Color(0.32f, 0.22f, 0.1f), _dirtShown));
        }
    }
}
