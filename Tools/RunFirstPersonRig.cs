using UnityEditor;
using UnityEngine;
using Slotgun.EditorTools;

public static class RunFirstPersonRig
{
    public static void Execute()
    {
        try
        {
            FirstPersonRig.InstallInActiveScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[RunFirstPersonRig] FAILED: " + e);
        }
    }
}
