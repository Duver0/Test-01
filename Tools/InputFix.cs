using UnityEditor;
using UnityEngine.InputSystem;

public static class InputFix
{
    public static string Execute()
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (asset == null) return "asset not found";
        string before = asset.enabled ? "enabled" : "disabled";
        asset.Disable();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return $"was {before}, now {(asset.enabled ? "enabled" : "disabled")}";
    }
}
