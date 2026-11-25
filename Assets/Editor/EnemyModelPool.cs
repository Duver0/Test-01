using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slotgun.EditorTools
{
    /// <summary>
    /// Fills the wave's body pool with every rigged low-poly enemy character the project owns — and
    /// deliberately not the player's own Blink body — so a sector's wave is a mixed crowd of zombies,
    /// skeletons, knights and demons instead of one pack repeated. Idempotent: re-running it only
    /// rewrites the same list.
    /// </summary>
    public static class EnemyModelPool
    {
        const string CoreScenePath = "Assets/Scenes/Slotgun_Core.unity";

        /// <summary>
        /// Every rigged enemy body in the project. The player's own character is left out on purpose:
        /// it is the one model the first-person camera must never see from the inside.
        /// </summary>
        static readonly string[] Bodies =
        {
            "Assets/3D Characters Zombie City Streets Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_SuitMan_1.prefab",
            "Assets/3D Characters Zombie Hospital Lowpoly Pack - Lite/Prefabs/(P) Characters_Zombie_Pacient_04.prefab",
            "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_NPC/Skeleton/PT_Skeleton_Male_Modular.prefab",
            "Assets/Polytope Studio/Lowpoly_Characters/Prefabs/Modular_Armors/PT_Male_Armors_Skeleton_Modular.prefab",
            "Assets/tt-3d/LowPolyMedievalEurope_StarterPack/Character/Prefabs/assasin_v2.prefab",
            "Assets/tt-3d/LowPolyMedievalEurope_StarterPack/Character/Prefabs/axe_v2.prefab",
            "Assets/tt-3d/LowPolyMedievalEurope_StarterPack/Character/Prefabs/demonking_v2.prefab",
            "Assets/tt-3d/LowPolyMedievalEurope_StarterPack/Character/Prefabs/knight_v2.prefab",
            "Assets/tt-3d/LowPolyMedievalEurope_StarterPack/Character/Prefabs/knight_woman_v2.prefab",
        };

        [MenuItem("Slotgun/Wire Enemy Models (all low-poly bodies)")]
        public static void Wire()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != CoreScenePath)
                scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);

            var director = Object.FindAnyObjectByType<GameDirector>();
            if (director == null)
            {
                Debug.LogError("EnemyModelPool: no GameDirector found in " + CoreScenePath + ".");
                return;
            }

            int wired = 0;
            var so = new SerializedObject(director);
            var list = so.FindProperty("enemyModels");
            if (list == null)
            {
                Debug.LogError("EnemyModelPool: GameDirector has no enemyModels field — recompile first.");
                return;
            }

            list.arraySize = Bodies.Length;
            for (int i = 0; i < Bodies.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Bodies[i]);
                if (prefab == null) Debug.LogWarning("EnemyModelPool: missing " + Bodies[i]);
                else wired++;

                list.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"EnemyModelPool: wired {wired}/{Bodies.Length} enemy bodies into {scene.name}.");
        }
    }
}
