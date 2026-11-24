using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CheckPipeline
{
    public static void Execute()
    {
        var g = GraphicsSettings.defaultRenderPipeline;
        var q = QualitySettings.renderPipeline;
        var cur = GraphicsSettings.currentRenderPipeline;

        Debug.Log("[PIPE] default=" + Name(g) + " qualityOverride=" + Name(q) + " current=" + Name(cur) +
                  " qualityLevel=" + QualitySettings.names[QualitySettings.GetQualityLevel()]);

        var guids = AssetDatabase.FindAssets("t:RenderPipelineAsset");
        foreach (var guid in guids)
            Debug.Log("[PIPE] asset " + AssetDatabase.GUIDToAssetPath(guid));
    }

    static string Name(RenderPipelineAsset a) => a != null ? a.GetType().Name + "(" + a.name + ")" : "<null>";
}
