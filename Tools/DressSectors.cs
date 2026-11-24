using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slotgun;
using Slotgun.EditorTools;

/// <summary>
/// Dresses every sector arena one at a time, opening each scene <b>additively</b> so the scene the
/// editor already has open is never replaced. The menu-item version opened each sector with
/// OpenSceneMode.Single from inside a menu callback, which stopped dead after the first arena —
/// the single-mode load tears the active scene down under the running callback.
/// </summary>
public static class DressSectors
{
    const string CatalogPath = "Assets/Data/SectorCatalog.asset";
    const string ScenesFolder = "Assets/Scenes";
    const string CoreScenePath = ScenesFolder + "/Slotgun_Core.unity";

    public static void Execute()
    {
        var log = new StringBuilder();

        var catalog = AssetDatabase.LoadAssetAtPath<SectorCatalog>(CatalogPath);
        if (catalog == null || catalog.Count == 0)
        {
            Debug.LogError("[Dress] no catalog");
            return;
        }

        var active = EditorSceneManager.GetActiveScene();
        if (active.IsValid() && active.isDirty && !EditorApplication.isPlaying)
            EditorSceneManager.SaveScene(active);

        log.AppendLine("[Dress] catalog count=" + catalog.Count);

        for (int i = 0; i < catalog.Count; i++)
        {
            var def = catalog.Get(i);
            if (def == null) { log.AppendLine("[Dress] " + i + " null def"); continue; }

            string scenePath = ScenesFolder + "/" + def.sceneName + ".unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                log.AppendLine("[Dress] " + i + " missing scene " + scenePath);
                continue;
            }

            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                GameObject arena = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == "Arena") { arena = root; break; }

                if (arena == null)
                {
                    log.AppendLine("[Dress] " + i + " '" + def.sceneName + "' has no Arena root");
                }
                else
                {
                    SectorScenery.Dress(arena.transform, def.arenaRadius, i);

                    var scenery = arena.transform.Find("Scenery");
                    int count = scenery != null ? scenery.childCount : 0;

                    EditorSceneManager.MarkSceneDirty(scene);
                    bool saved = EditorSceneManager.SaveScene(scene, scenePath);

                    log.AppendLine("[Dress] " + i + " '" + def.displayName + "' radius=" + def.arenaRadius +
                                   " children=" + count + " saved=" + saved);
                }
            }
            catch (System.Exception e)
            {
                log.AppendLine("[Dress] " + i + " FAILED " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        if (EditorSceneManager.GetActiveScene().path != CoreScenePath &&
            AssetDatabase.LoadAssetAtPath<SceneAsset>(CoreScenePath) != null &&
            EditorSceneManager.GetActiveScene().name != "Slotgun_Core")
            EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);

        Debug.Log(log.ToString());
    }
}
