using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slotgun
{
    /// <summary>
    /// Owns every additive scene load and unload, so the run never keeps two finished maps
    /// resident: the core scene (player, HUD, director) loads once and survives, while the arena
    /// and the vestibule take turns in the same single slot.
    ///
    /// Note: Unity's scene queue serialises operations, and an unload will never complete while a
    /// load is parked waiting for activation. The vestibule therefore activates the next arena
    /// before unloading itself (see <see cref="Activate"/>).
    /// </summary>
    public class SceneStreamer : MonoBehaviour
    {
        public static SceneStreamer Instance { get; private set; }

        public bool Busy { get; private set; }
        public float Progress { get; private set; }
        public string CurrentScene { get; private set; }

        void Awake()
        {
            Instance = this;
            Progress = 1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return SceneManager.GetSceneByName(sceneName).isLoaded;
        }

        public IEnumerator Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[Slotgun] SceneStreamer.Load called with an empty scene name.");
                yield break;
            }

            if (IsLoaded(sceneName))
            {
                CurrentScene = sceneName;
                Progress = 1f;
                yield break;
            }

            Busy = true;
            Progress = 0f;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[Slotgun] Scene '{sceneName}' could not be loaded. Is it in Build Settings?");
                Busy = false;
                yield break;
            }

            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                Progress = Mathf.Clamp01(op.progress);
                yield return null;
            }

            Progress = 1f;
            CurrentScene = sceneName;
            Busy = false;
        }

        /// <summary>
        /// Streams a scene in without activating it, so the next map is fully prepared while the
        /// player is still shopping and never pops into view behind the vestibule.
        /// </summary>
        public AsyncOperation Preload(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || IsLoaded(sceneName)) return null;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[Slotgun] Scene '{sceneName}' could not be preloaded. Is it in Build Settings?");
                return null;
            }

            op.allowSceneActivation = false;
            Busy = true;
            Progress = 0f;
            return op;
        }

        /// <summary>Releases a preloaded scene into the world.</summary>
        public IEnumerator Activate(AsyncOperation op, string sceneName)
        {
            if (op == null)
            {
                yield return Load(sceneName);
                yield break;
            }

            Busy = true;
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                Progress = Mathf.Clamp01(op.progress);
                yield return null;
            }

            Progress = 1f;
            CurrentScene = sceneName;
            Busy = false;
        }

        /// <summary>Unloads a scene and then drops every asset it was the only user of.</summary>
        public IEnumerator Unload(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !IsLoaded(sceneName)) yield break;

            Busy = true;

            var op = SceneManager.UnloadSceneAsync(sceneName);
            float waited = 0f;
            while (op != null && !op.isDone)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > 10f)
                {
                    Debug.LogWarning($"[Slotgun] Unloading '{sceneName}' did not report completion; moving on.");
                    break;
                }
                yield return null;
            }

            if (CurrentScene == sceneName) CurrentScene = null;
            Busy = false;

            // The previous map is gone; release the meshes/materials that only it referenced. The
            // toon material cache is static, so shared materials survive and are not rebuilt.
            yield return Resources.UnloadUnusedAssets();
        }
    }
}
