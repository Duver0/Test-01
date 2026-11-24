using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Removes leftover inspection objects from the saved scene and saves it back clean.</summary>
public class ClearProbes
{
    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();
        int removed = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            bool probe = root.name == "FaceProbe" || root.name == "ZombieFaceProbe"
                         || root.name == "ZombieSuit" || root.name == "ZombiePatient";

            if (probe)
            {
                Debug.Log("[CLEAN] removing " + root.name);
                Object.DestroyImmediate(root);
                removed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[CLEAN] removed=" + removed + " saved=" + scene.name);

        foreach (var root in scene.GetRootGameObjects())
            Debug.Log("[CLEAN] root " + root.name);
    }
}
