using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slotgun.EditorTools
{
    /// <summary>
    /// Dresses the five arenas with the imported environment packs.
    ///
    /// The zombie and skeleton packs are character-only "Lite" packs — their demo scenes contain a
    /// ground plane and the characters, no scenery — so the environment art that actually exists in
    /// this project is the three nature packs (BrokenVector's 38 low-poly trees in four colour
    /// sheets, the Snowy pine set and the FreeVegetation ground set). Each sector keeps the exact
    /// same arena footprint and fight layout; only where the scenery sits, which pack it comes from
    /// and how much of it there is changes, so every sector reads as its own place.
    ///
    /// The dress is deterministic per sector (seeded by the sector index), so re-running it — or a
    /// full "Slotgun/Rebuild All Scenes" — always produces the same arena.
    /// </summary>
    public static class SectorScenery
    {
        const string TreeFolder = "Assets/BrokenVector/LowPolyTreePack/Prefabs";
        const string PineFolder = "Assets/Snowy_Low_Poly_Trees";
        const string PlantFolder = "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Prefabs";

        const string TreeMatFolder = "Assets/BrokenVector/LowPolyTreePack/Materials";
        const string PineMatFolder = "Assets/Snowy_Low_Poly_Trees/Materials";
        const string PlantMatFolder = "Assets/FreeVegetation-LowPolyNature/FreeVegetation/Materials";
        const string HumanMatFolder = "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Materials_Humans";

        const string CatalogPath = "Assets/Data/SectorCatalog.asset";
        const string ScenesFolder = "Assets/Scenes";
        const string CoreScenePath = ScenesFolder + "/Slotgun_Core.unity";

        // ---- materials -------------------------------------------------------

        /// <summary>
        /// The imported packs were authored for the Built-in pipeline and every one of their
        /// materials still uses the Standard shader, which renders magenta under URP. Move them to
        /// URP/Lit keeping the colour sheet, tint and textures they were authored with.
        /// </summary>
        [MenuItem("Slotgun/Upgrade Imported Materials to URP", false, 40)]
        public static void UpgradeEnvironmentMaterials()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[Slotgun] URP/Lit shader not found — is the Universal RP package installed?");
                return;
            }

            string[] folders = { TreeMatFolder, PineMatFolder, PlantMatFolder, HumanMatFolder };
            int changed = 0;

            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null || mat.shader == null) continue;
                    if (mat.shader.name.StartsWith("Universal")) continue;

                    Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Color tint = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                    mat.shader = urpLit;
                    if (tex != null)
                    {
                        mat.SetTexture("_BaseMap", tex);
                        mat.SetTexture("_MainTex", tex);
                    }
                    mat.SetColor("_BaseColor", tint);
                    mat.SetColor("_Color", tint);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

                    EditorUtility.SetDirty(mat);
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Slotgun] {changed} environment materials moved to URP/Lit.");
        }

        // ---- dressing --------------------------------------------------------

        /// <summary>
        /// Rebuilds the whole scenery layer of every sector scene. Only the "Scenery" child of the
        /// arena is touched, so the geometry, the exit door and the anchors stay exactly as built.
        /// </summary>
        [MenuItem("Slotgun/Dress Sector Scenery", false, 41)]
        public static void DressAllSectors()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SectorCatalog>(CatalogPath);
            if (catalog == null || catalog.Count == 0)
            {
                Debug.LogError("[Slotgun] No sector catalog at " + CatalogPath + " — run Slotgun/Rebuild All Scenes first.");
                return;
            }

            var active = EditorSceneManager.GetActiveScene();
            if (active.IsValid() && active.isDirty && !EditorApplication.isPlaying)
                EditorSceneManager.SaveScene(active);

            for (int i = 0; i < catalog.Count; i++)
            {
                var def = catalog.Get(i);
                if (def == null) continue;

                string scenePath = ScenesFolder + "/" + def.sceneName + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    Debug.LogWarning("[Slotgun] Missing sector scene " + scenePath);
                    continue;
                }

                // Additively, not single: a single-mode load from inside a menu callback tears the
                // active scene down under the running loop, which is why this pass used to stop
                // dead after the first arena.
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    var arena = Find(scene, "Arena");
                    if (arena == null)
                    {
                        Debug.LogWarning("[Slotgun] " + scenePath + " has no 'Arena' root — scenery skipped.");
                        continue;
                    }

                    Dress(arena.transform, def.arenaRadius, i);

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, scenePath);
                    Debug.Log($"[Slotgun] Scenery rebuilt for sector {i + 1} '{def.displayName}'.");
                }
                finally
                {
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (EditorSceneManager.GetActiveScene().name != "Slotgun_Core" &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath) != null)
                EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// Builds the scenery layer of one arena. Called by the sector generator as well, so a full
        /// scene rebuild ends up dressed exactly like this.
        /// </summary>
        public static void Dress(Transform arenaRoot, float arenaRadius, int index)
        {
            if (arenaRoot == null || arenaRadius <= 1f) return;

            var previous = arenaRoot.Find("Scenery");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);

            string[] sheets;
            bool snowy;
            int treeCount, plantCount;
            Theme(index, out sheets, out snowy, out treeCount, out plantCount);

            var trees = AllTrees();
            var pines = snowy ? Pines() : new List<GameObject>();
            var plants = Plants();

            if (trees.Count == 0)
            {
                Debug.LogWarning("[Slotgun] No tree prefabs under " + TreeFolder + " — is the pack imported?");
                return;
            }

            var root = new GameObject("Scenery").transform;
            root.SetParent(arenaRoot, false);

            var rng = new System.Random(7919 + index * 131);

            // A belt of trees just inside the walls. The exit gate sits at +Z, so that slice of the
            // ring is left clear — nothing may hide the way out.
            float inner = arenaRadius * 0.93f;
            float outer = arenaRadius + 4.4f;

            for (int i = 0; i < treeCount; i++)
            {
                float angle = (float)rng.NextDouble() * 360f;
                if (angle < 16f || angle > 344f) angle += 30f;

                float dist = Mathf.Lerp(inner, outer, (float)rng.NextDouble());

                // A snowy sector mixes the pine set into the belt; everything else is a tree model
                // wearing this sector's colour sheet.
                bool usePine = pines.Count > 0 && rng.NextDouble() < 0.35;
                var prefab = usePine ? pines[rng.Next(pines.Count)] : trees[rng.Next(trees.Count)];
                var sheet = usePine ? null : SheetMaterial(sheets[rng.Next(sheets.Length)]);

                var pos = new Vector3(
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist,
                    0f,
                    Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                float scale = Mathf.Lerp(0.85f, 1.25f, (float)rng.NextDouble());
                Place(prefab, root, "Tree_" + i, pos, (float)rng.NextDouble() * 360f, scale, sheet);
            }

            // Ground cover across the floor, denser towards the walls. Area-uniform, so the middle
            // of the arena does not end up with a clump and a bald ring.
            for (int i = 0; i < plantCount && plants.Count > 0; i++)
            {
                float angle = (float)rng.NextDouble() * 360f;
                float dist = Mathf.Lerp(arenaRadius * 0.28f, arenaRadius * 0.97f, Mathf.Sqrt((float)rng.NextDouble()));
                var prefab = plants[rng.Next(plants.Count)];

                var pos = new Vector3(
                    Mathf.Sin(angle * Mathf.Deg2Rad) * dist,
                    0f,
                    Mathf.Cos(angle * Mathf.Deg2Rad) * dist);

                float scale = Mathf.Lerp(0.8f, 1.35f, (float)rng.NextDouble());
                Place(prefab, root, "Plant_" + i, pos, (float)rng.NextDouble() * 360f, scale, null);
            }
        }

        /// <summary>Which pack dresses which sector, and how much of it.</summary>
        static void Theme(int index, out string[] sheets, out bool pines, out int trees, out int plants)
        {
            switch (index % 5)
            {
                case 0: sheets = new[] { "Normal" }; pines = false; trees = 24; plants = 30; break;
                case 1: sheets = new[] { "Dry" }; pines = false; trees = 27; plants = 26; break;
                case 2: sheets = new[] { "Fall" }; pines = false; trees = 30; plants = 34; break;
                case 3: sheets = new[] { "Cold" }; pines = true; trees = 24; plants = 20; break;
                default: sheets = new[] { "Fall", "Normal" }; pines = false; trees = 32; plants = 38; break;
            }
        }

        static readonly Dictionary<string, Material> SheetCache = new Dictionary<string, Material>();

        /// <summary>
        /// A tree's colour comes from its material, not from its mesh: the pack ships one set of 38
        /// tree models and four colour sheets, and every prefab is authored on the "Normal" sheet.
        /// So a sector picks a sheet and the placed instance takes it as a material override —
        /// filtering the prefabs by material name would find only "Normal" and leave every other
        /// sector with no trees at all.
        /// </summary>
        static Material SheetMaterial(string sheet)
        {
            if (string.IsNullOrEmpty(sheet)) return null;
            if (SheetCache.TryGetValue(sheet, out var cached)) return cached;

            string path = TreeMatFolder + "/" + sheet + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) Debug.LogWarning("[Slotgun] No colour sheet '" + sheet + "' at " + path);

            SheetCache[sheet] = mat;
            return mat;
        }

        static List<GameObject> AllTrees()
        {
            var result = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(TreeFolder)) return result;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { TreeFolder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null) result.Add(go);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        static List<GameObject> Pines()
        {
            var result = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(PineFolder)) return result;

            foreach (var guid in AssetDatabase.FindAssets("Pine_Snowy t:Prefab", new[] { PineFolder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null) result.Add(go);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        static List<GameObject> Plants()
        {
            var result = new List<GameObject>();
            if (!AssetDatabase.IsValidFolder(PlantFolder)) return result;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PlantFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                // The FreeVegetation trees carry their LOD meshes far off the pivot; only the
                // ground cover (grass, bushes, plants) is used here.
                string n = go.name;
                if (n.StartsWith("Tree_") || n.StartsWith("FallenTree_")) continue;
                result.Add(go);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        static void Place(GameObject prefab, Transform parent, string name, Vector3 position, float yaw, float scale, Material sheet)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name + " " + prefab.name;

            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;

            // The colour sheet is a material override on the instance: one mesh set, four seasons.
            if (sheet != null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = sheet;

            // Nothing in an arena may be walked into: scenery is decoration only.
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // Pivots are inconsistent across the packs, so put the lowest vertex on the floor.
            float minY = LocalMinY(go);
            if (!Mathf.Approximately(minY, 0f))
                go.transform.localPosition = position + Vector3.up * (-minY * scale);
        }

        static float LocalMinY(GameObject go)
        {
            Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
            float min = float.MaxValue;

            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                Bounds b = mesh.bounds;
                Matrix4x4 m = toLocal * filter.transform.localToWorldMatrix;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);

                    float y = m.MultiplyPoint3x4(corner).y;
                    if (y < min) min = y;
                }
            }

            return min == float.MaxValue ? 0f : min;
        }

        static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var hit = FindIn(root.transform, name);
                if (hit != null) return hit.gameObject;
            }
            return null;
        }

        static Transform FindIn(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var hit = FindIn(parent.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
