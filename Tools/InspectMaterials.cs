using System.Text;
using UnityEditor;
using UnityEngine;

public class InspectMaterials
{
    static readonly string[] Paths =
    {
        "Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_NPC/Materials/PT_NPC_Mat.mat",
        "Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_Armors/Materials/PT_Armors_Material.mat",
        "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Materials/BaseMaterial.mat",
        "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Materials/BaseMaterial.mat",
    };

    public static void Execute()
    {
        var sb = new StringBuilder();

        foreach (var path in Paths)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { sb.AppendLine("[MAT] missing " + path); continue; }

            sb.AppendLine($"[MAT] {path}");
            sb.AppendLine($"  shader={mat.shader.name} renderQueue={mat.renderQueue}");

            var shader = mat.shader;
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                var type = shader.GetPropertyType(i);
                var name = shader.GetPropertyName(i);
                if (type == UnityEngine.Rendering.ShaderPropertyType.Texture)
                {
                    var tex = mat.GetTexture(name);
                    sb.AppendLine($"  tex {name} = {(tex != null ? tex.name : "<none>")}");
                }
                else if (type == UnityEngine.Rendering.ShaderPropertyType.Color)
                {
                    sb.AppendLine($"  color {name} = {mat.GetColor(name)}");
                }
            }
        }

        Debug.Log(sb.ToString());
    }
}
