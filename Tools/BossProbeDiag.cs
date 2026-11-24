using UnityEditor;
using UnityEngine;
using Slotgun;

/// <summary>Temporary: proves EditorApplication.update still ticks and what the run state is.</summary>
public static class BossProbeDiag
{
    static int _n;

    public static void Execute()
    {
        _n = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log($"[DIAG] armed playing={Application.isPlaying}");
    }

    static void Tick()
    {
        if (_n > 60) { EditorApplication.update -= Tick; Debug.Log("[DIAG] stop"); return; }

        if (_n % 10 == 0)
        {
            var d = GameDirector.Instance;
            Debug.Log($"[DIAG] tick {_n} inst={(d != null)} player={(d != null && d.player != null)} " +
                      $"playing={Application.isPlaying} phase={(d != null ? d.Phase.ToString() : "-")} " +
                      $"boss={(d != null && d.CurrentBoss != null)} scale={Time.timeScale}");
        }
        _n++;
    }
}
