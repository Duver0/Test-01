using System.Reflection;
using UnityEngine;
using Slotgun;

/// <summary>
/// Confirms the assembly currently loaded in play mode is the polling build, so a sprint
/// harness result cannot be attributed to a stale pre-edit assembly.
/// </summary>
public static class SprintAssemblyProbe
{
    public static void Execute()
    {
        var t = typeof(PlayerController);
        var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

        bool hasPoll = t.GetMethod("PollSprint", flags) != null;
        bool hasMessage = t.GetMethod("OnSprint", flags) != null;

        var asm = t.Assembly;
        Debug.Log("[ASMPROBE] assembly=" + asm.GetName().Name +
                  " version=" + asm.GetName().Version +
                  " PollSprint=" + hasPoll +
                  " OnSprint=" + hasMessage +
                  " playing=" + Application.isPlaying);
    }
}
