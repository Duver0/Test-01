using UnityEngine;
using UnityEngine.Rendering;

namespace Slotgun
{
    /// <summary>
    /// Applies the art bible's environment settings. It lives in the persistent core scene, so a
    /// streamed sector can never leave its own fog, ambient or a template skybox behind when it
    /// unloads — every sector's atmosphere is authored in one place.
    /// </summary>
    public class LightingRig : MonoBehaviour
    {
        public static LightingRig Instance { get; private set; }

        public float defaultFogDensity = 0.011f;

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
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Palette.Shadow, Palette.Walls, 0.8f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Palette.Shadow;
            RenderSettings.fogDensity = def != null ? def.fogDensity : defaultFogDensity;
            RenderSettings.reflectionIntensity = 0.15f;
        }
    }
}
