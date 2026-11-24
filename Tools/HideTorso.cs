using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HideTorso
{
    static readonly string[] Parts = { "Chest", "Starter_Chest", "PlateSet1_Chest" };

    public static void Execute()
    {
        var player = GameObject.Find("Player");
        var body = player != null ? player.transform.Find("Body") : null;
        if (body == null) { Debug.Log("[Torso] no Player/Body in the open scene"); return; }

        int off = 0;
        foreach (var r in body.GetComponentsInChildren<Renderer>(true))
        {
            for (int i = 0; i < Parts.Length; i++)
                if (r.gameObject.name == Parts[i]) { r.enabled = false; off++; break; }
        }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene, scene.path);

        Debug.Log("[Torso] disabled " + off + " torso renderers in " + scene.name);
    }
}
