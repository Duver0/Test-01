using UnityEditor;

public static class RemoveDupController
{
    public static void Execute()
    {
        string[] paths =
        {
            "Assets/Scripts/Player/PlayerController_backup.cs",
            "Assets/Scripts/Player/PlayerController_fix.cs",
            "Assets/Scripts/Player/PlayerController_fixed.cs"
        };

        foreach (var p in paths)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p) != null)
                AssetDatabase.DeleteAsset(p);
        }

        AssetDatabase.Refresh();
    }
}
