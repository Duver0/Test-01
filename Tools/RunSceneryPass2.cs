using UnityEditor;
using UnityEngine;
using Slotgun.EditorTools;

public static class RunSceneryPass2
{
    public static void Execute()
    {
        try
        {
            SectorScenery.DressAllSectors();
            Debug.Log("[SceneryPass2] finished OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SceneryPass2] FAILED: " + e);
        }
    }
}
