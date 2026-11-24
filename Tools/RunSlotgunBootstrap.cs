using UnityEditor;

public static class RunSlotgunBootstrap
{
    public static void Execute()
    {
        Slotgun.EditorTools.SlotgunBootstrap.RebuildAll();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
