using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Slotgun;

public static class DiagStream
{
    public static void Execute()
    {
        var d = GameDirector.Instance;
        var s = SceneStreamer.Instance;
        var names = new List<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            names.Add(SceneManager.GetSceneAt(i).name + (SceneManager.GetSceneAt(i).isLoaded ? "" : "(x)"));

        string preload = "-";
        var f = typeof(GameDirector).GetField("_preloadOp", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null)
        {
            var op = f.GetValue(d) as AsyncOperation;
            preload = op == null ? "null" : $"progress={op.progress:0.00} done={op.isDone} active={op.allowSceneActivation}";
        }

        Debug.Log($"[DIAG] phase={(d != null ? d.Phase.ToString() : "none")} frame={Time.frameCount} " +
                  $"realtime={Time.realtimeSinceStartup:0.00} paused={EditorApplication.isPaused} focused={Application.isFocused} runInBg={Application.runInBackground} " +
                  $"streamer(busy={(s != null ? s.Busy.ToString() : "n/a")}, current={(s != null ? s.CurrentScene : "n/a")}) " +
                  $"preload=[{preload}] scenes=[{string.Join(", ", names)}]");
    }
}
