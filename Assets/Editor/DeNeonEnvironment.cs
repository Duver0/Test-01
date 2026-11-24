using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Slotgun;

namespace Slotgun.EditorTools
{
    /// <summary>
    /// Strips the neon casino look out of the arenas and the prep room and rebuilds it out of the
    /// imported low-poly packs.
    ///
    /// Re-run this after a <c>Slotgun/Rebuild All Scenes</c>: the sector generator still paints its
    /// geometry with <see cref="Palette"/> (which now only belongs to the HUD, the bosses and the
    /// effects), so a rebuilt arena comes back neon until this pass maps those materials onto
    /// <see cref="EnvPalette"/> and the two generated surface textures.
    ///
    /// Only materials named "Toon_XXXXXX" are touched — those are the ones the arena builder makes.
    /// The scenery layer wears the packs' own materials (Normal / Dry / Fall / Cold / Leafs / Grass)
    /// and is left exactly as it is.
    /// </summary>
    public static class DeNeonEnvironment
    {
        const string EnvFolder = "Assets/Art/Env";
        const string SkyPath = EnvFolder + "/Env_Skybox.mat";
        const string GroundPath = EnvFolder + "/Env_Ground_Dirt.mat";
        const string StonePath = EnvFolder + "/Env_Stone_Wall.mat";
        const string WoodPath = EnvFolder + "/Env_Wood.mat";
        const string WoodDarkPath = EnvFolder + "/Env_Wood_Dark.mat";
        const string IronPath = EnvFolder + "/Env_Iron.mat";

        const string CatalogPath = "Assets/Data/SectorCatalog.asset";
        const string ScenesFolder = "Assets/Scenes";
        const string PrepPath = ScenesFolder + "/Slotgun_PrepRoom.unity";

        static readonly Color Lantern = new Color(1f, 0.88f, 0.72f, 1f);

        [MenuItem("Slotgun/De-Neon Environment (daylight)", false, 43)]
        public static void Run()
        {
            var sky = SkyboxMaterial();
            var ground = Textured(GroundPath, 18f);
            var stone = Textured(StonePath, 24f);
            var wood = Solid(WoodPath, EnvPalette.Wood);
            var woodDark = Solid(WoodDarkPath, EnvPalette.WoodDark);
            var iron = Solid(IronPath, EnvPalette.Iron);

            int swapped = 0;
            int scenes = 0;

            var catalog = AssetDatabase.LoadAssetAtPath<SectorCatalog>(CatalogPath);
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    var def = catalog.Get(i);
                    if (def == null) continue;

                    string path = ScenesFolder + "/" + def.sceneName + ".unity";
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    {
                        Debug.LogWarning("[DeNeon] missing scene " + path);
                        continue;
                    }

                    int n = Dress(path, def, ground, stone, wood, woodDark, iron);
                    swapped += n;
                    scenes++;
                    Debug.Log("[DeNeon] " + def.sceneName + ": " + n + " renderers restyled");
                }
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PrepPath) != null)
            {
                int n = Dress(PrepPath, null, ground, stone, wood, woodDark, iron);
                swapped += n;
                scenes++;
                Debug.Log("[DeNeon] Slotgun_PrepRoom: " + n + " renderers restyled");
            }

            Core(sky);

            AssetDatabase.SaveAssets();
            Debug.Log("[DeNeon] done — " + swapped + " renderers across " + scenes + " scenes, daylight applied");
        }

        // ---- one scene -----------------------------------------------------

        static int Dress(string path, SectorDefinition def, Material ground, Material stone,
                         Material wood, Material woodDark, Material iron)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (!scene.IsValid()) { Debug.LogWarning("[DeNeon] could not open " + path); return 0; }

            int swapped = 0;
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        bool touched = false;

                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null) continue;
                            if (!TryNeonColor(mats[i], out var old)) continue;

                            var target = Role(old, def, ground, stone, wood, woodDark, iron);
                            if (target == null) continue;

                            mats[i] = target;
                            touched = true;
                        }

                        if (touched) { r.sharedMaterials = mats; swapped++; }
                    }

                    // The six arena lights were pink/gold/cyan bulbs. With a sun overhead they become
                    // warm lantern pools at the walls instead of coloured neon spill.
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                        if (light.type == LightType.Point)
                        {
                            light.color = Lantern;
                            light.intensity = Mathf.Min(light.intensity, 1.8f);
                        }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }

            return swapped;
        }

        /// <summary>Reads the colour out of a builder material, named "Toon_RRGGBB" or "Toon_RRGGBB_E".</summary>
        static bool TryNeonColor(Material mat, out Color color)
        {
            color = Color.magenta;
            const string Prefix = "Toon_";
            string n = mat.name;
            if (!n.StartsWith(Prefix) || n.Length < Prefix.Length + 6) return false;

            string hex = n.Substring(Prefix.Length, 6);
            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
                if (!ok) return false;
            }

            return ColorUtility.TryParseHtmlString("#" + hex, out color);
        }

        /// <summary>Old neon colour to new surface. Order matters: floor before wall before accent.</summary>
        static Material Role(Color c, SectorDefinition def, Material ground, Material stone,
                             Material wood, Material woodDark, Material iron)
        {
            Color floor = def != null ? def.floorTint : Palette.Floor;
            Color wall = def != null ? def.wallTint : Palette.Walls;
            Color accent = def != null ? def.accent : Palette.Accent;

            if (EnvPalette.Same(c, floor)) return ground;
            if (EnvPalette.Same(c, wall) || EnvPalette.Same(c, Palette.Walls)) return stone;
            if (EnvPalette.Same(c, accent) || EnvPalette.Same(c, Palette.Accent)) return iron;
            if (EnvPalette.Same(c, Palette.Props)) return wood;
            if (EnvPalette.Same(c, Palette.CharacterPrimary)) return woodDark;
            if (EnvPalette.Same(c, Palette.CharacterSecondary)) return wood;

            // A colour the builder no longer uses: keep its brightness, drop the neon.
            float value = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return value > 0.6f ? stone : woodDark;
        }

        // ---- core scene: sun, sky, camera ----------------------------------

        static void Core(Material sky)
        {
            Scene core = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path.Replace('\\', '/').EndsWith("Slotgun_Core.unity")) { core = s; break; }
            }

            if (!core.IsValid())
            {
                Debug.LogWarning("[DeNeon] Slotgun_Core is not open — daylight not applied");
                return;
            }

            Light key = null;

            foreach (var root in core.GetRootGameObjects())
            {
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.name == "KeyLight")
                    {
                        light.color = EnvPalette.Sun;
                        light.intensity = 1.45f;
                        light.shadows = LightShadows.Soft;
                        light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                        key = light;
                    }
                    else if (light.name == "RimLight")
                    {
                        light.color = EnvPalette.Fill;
                        light.intensity = 0.28f;
                        light.shadows = LightShadows.None;
                        light.transform.rotation = Quaternion.Euler(18f, 145f, 0f);
                    }
                }

                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    cam.clearFlags = CameraClearFlags.Skybox;
            }

            RenderSettings.sun = key;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = EnvPalette.Fog;
            RenderSettings.fogDensity = 0.0045f;

            var rig = Object.FindAnyObjectByType<LightingRig>();
            if (rig != null)
            {
                rig.skybox = sky;
                rig.defaultFogDensity = 0.0045f;
                rig.fogScale = 0.4f;
                rig.Apply(null);
                EditorUtility.SetDirty(rig);
            }

            EditorSceneManager.MarkSceneDirty(core);
            EditorSceneManager.SaveScene(core, core.path);
            Debug.Log("[DeNeon] daylight applied to Slotgun_Core (key=" + (key != null ? key.name : "none") + ")");
        }

        // ---- assets --------------------------------------------------------

        static Material SkyboxMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (mat == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                {
                    Debug.LogWarning("[DeNeon] Skybox/Procedural shader not found");
                    return null;
                }

                EnsureFolder(EnvFolder);
                mat = new Material(shader) { name = "Env_Skybox" };
                AssetDatabase.CreateAsset(mat, SkyPath);
            }

            if (mat.HasProperty("_SkyTint")) mat.SetColor("_SkyTint", EnvPalette.Sky);
            if (mat.HasProperty("_GroundColor")) mat.SetColor("_GroundColor", EnvPalette.SkyGround);
            if (mat.HasProperty("_AtmosphereThickness")) mat.SetFloat("_AtmosphereThickness", 0.75f);
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", 1.25f);
            if (mat.HasProperty("_SunSize")) mat.SetFloat("_SunSize", 0.04f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Textured(string path, float tiling)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogWarning("[DeNeon] missing material " + path);
                return null;
            }

            mat.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Solid(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogWarning("[DeNeon] URP/Lit shader not found");
                    return null;
                }

                EnsureFolder(EnvFolder);
                mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
