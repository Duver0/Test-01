using UnityEditor;
using UnityEngine;
using Slotgun.EditorTools;

public static class RunDeNeon
{
    public static string Execute()
    {
        DeNeonEnvironment.Run();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Env/Env_Stone_Wall.mat");
        var ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Env/Env_Ground_Dirt.mat");

        string stoneTile = stone != null ? stone.GetTextureScale("_BaseMap").x.ToString("0.#") : "missing";
        string groundTile = ground != null ? ground.GetTextureScale("_BaseMap").x.ToString("0.#") : "missing";

        return "[DeNeon] stone tiling = " + stoneTile + ", ground tiling = " + groundTile;
    }
}
