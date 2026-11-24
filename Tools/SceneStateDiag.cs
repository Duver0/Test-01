using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneStateDiag
{
    public static void Execute()
    {
        var active = EditorSceneManager.GetActiveScene();
        Debug.Log("[SceneState] isPlaying=" + EditorApplication.isPlaying +
                  " willChange=" + EditorApplication.isPlayingOrWillChangePlaymode +
                  " isUpdating=" + EditorApplication.isUpdating +
                  " isCompiling=" + EditorApplication.isCompiling +
                  " active=" + active.name + " valid=" + active.IsValid() + " dirty=" + active.isDirty +
                  " loaded=" + active.isLoaded + " path=" + active.path);

        try
        {
            bool ok = EditorSceneManager.SaveScene(active);
            Debug.Log("[SceneState] SaveScene -> " + ok);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SceneState] SaveScene threw: " + e.GetType().Name + " " + e.Message);
        }
    }
}
