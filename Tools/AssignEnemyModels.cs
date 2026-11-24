using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Removes the inspection probe and wires the imported characters onto the scene's GameDirector.</summary>
public class AssignEnemyModels
{
    const string ZombieSuit    = "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab";
    const string ZombiePatient = "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab";
    const string Skeleton      = "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_NPC/Skeleton/PT_Skeleton_Male_Modular.prefab";
    const string ArmorSkeleton = "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_Armors/PT_Male_Armors_Skeleton_Modular.prefab";

    public static void Execute()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // 1. Drop the probe the facing inspection left behind, without saving it.
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "FaceProbe")
            {
                Object.DestroyImmediate(root);
                Debug.Log("[MODELS] FaceProbe removed");
            }
        }

        // 2. Find the director that owns the run.
        Slotgun.GameDirector director = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            director = root.GetComponentInChildren<Slotgun.GameDirector>(true);
            if (director != null) break;
        }

        if (director == null) { Debug.LogError("[MODELS] GameDirector not found"); return; }

        var suit = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieSuit);
        var patient = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePatient);
        var skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(Skeleton);
        var armor = AssetDatabase.LoadAssetAtPath<GameObject>(ArmorSkeleton);

        director.zombieModels = new[] { suit, patient };
        director.skeletonModel = skeleton;
        director.armoredSkeletonModel = armor;

        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[MODELS] zombie={suit.name},{patient.name} skeleton={skeleton.name} armor={armor.name} saved={scene.name}");
    }
}
