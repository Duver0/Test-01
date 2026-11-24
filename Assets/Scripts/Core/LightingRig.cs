using UnityEngine;
using UnityEngine.Rendering;

namespace Slotgun
{
    /// <summary>
    /// Applies the run's lighting. It lives in the persistent core scene, so a streamed sector can
    /// never leave its own fog, ambient or a template skybox behind when it unloads — every sector's
    /// atmosphere is authored in one place.
    ///
    /// The old art bible was a neon casino: no skybox, a violet ambient and a tar-black fog that ate
    /// the horizon. This is ordinary daylight instead — a procedural sky, ambient lifted from it and
    /// a pale haze — so the imported low-poly packs read as scenery instead of silhouettes.
    /// </summary>
    public class LightingRig : MonoBehaviour
    {
        public static LightingRig Instance { get; private set; }

        [Tooltip("Skybox applied to every sector. When empty the world falls back to a flat sky tint.")]
        public Material skybox;

        [Tooltip("Fog density used when no sector definition is active.")]
        public float defaultFogDensity = 0.0045f;

        [Tooltip("Sector densities were authored for the old black void; this softens them into daylight haze.")]
        [Range(0.1f, 1f)] public float fogScale = 0.4f;

        void Awake()
        {
            Instance = this;
            Apply(null);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Apply(SectorDefinition def)
        {
            RenderSettings.skybox = skybox;

            if (skybox != null)
            {
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1f;
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = EnvPalette.Ambient;
            }

            RenderSettings.reflectionIntensity = 0.6f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = EnvPalette.Fog;
            RenderSettings.fogDensity = (def != null ? def.fogDensity : defaultFogDensity) * fogScale;
        }
    }
}
