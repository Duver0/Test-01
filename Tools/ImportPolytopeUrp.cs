using UnityEditor;
using UnityEngine;

public class ImportPolytopeUrp
{
    const string Package = "Assets/Polytope Studio/Lowpoly_Characters/URP/PT_Skeleton_URP_17.unitypackage";

    public static void Execute()
    {
        Debug.Log("[PTURP] importing " + Package);
        AssetDatabase.ImportPackage(Package, false);
    }
}
