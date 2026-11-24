using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slotgun
{
    /// <summary>
    /// Play mode boots with whatever scenes are open in the editor, so pressing Play while an arena
    /// or the vestibule is the open scene starts a session with no player, no HUD and no camera —
    /// which the Game View reports as "No cameras rendering".
    ///
    /// The player, the camera and the <see cref="GameDirector"/> all live in the core scene, so this
    /// makes the core scene the entry point no matter which scene is open: the run always starts the
    /// same way. In a build the core scene is build index 0, so this stays a no-op.
    /// </summary>
    public static class RunBootstrapper
    {
        public const string CoreSceneName = "Slotgun_Core";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureCoreScene()
        {
            // A run is already alive: the core scene is open (or open additively) as it should be.
            if (Object.FindAnyObjectByType<GameDirector>() != null) return;

            var core = SceneManager.GetSceneByName(CoreSceneName);
            if (core.isLoaded)
            {
                SceneManager.SetActiveScene(core);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(CoreSceneName))
            {
                Debug.LogError($"[Slotgun] '{CoreSceneName}' is missing from Build Settings, so a run cannot start. " +
                               "Open Slotgun_Core, or add it as build index 0.");
                return;
            }

            Debug.LogWarning($"[Slotgun] Started from '{SceneManager.GetActiveScene().name}'; " +
                             $"loading '{CoreSceneName}' so the run has a player and a camera.");
            SceneManager.LoadScene(CoreSceneName, LoadSceneMode.Single);
        }
    }
}
