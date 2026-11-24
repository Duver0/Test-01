using UnityEditor;
using UnityEngine;

public static class RunSceneryPass
{
    public static void Execute()
    {
        EditorApplication.ExecuteMenuItem("Slotgun/Upgrade Environment Materials to URP");
        EditorApplication.ExecuteMenuItem("Slotgun/Dress Sector Scenery");
        Debug.Log("[SceneryPass] finished");
    }
}
