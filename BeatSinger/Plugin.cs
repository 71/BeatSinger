using IllusionPlugin;
using UnityEngine.SceneManagement;

namespace BeatSinger
{
    /// <summary>
    ///   Entry point of the plugin.
    /// </summary>
    public sealed class Plugin : IPlugin
    {
        public string Name => "Beat Singer";
        public string Version => "0.2.0";

        public void OnApplicationStart()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        public void OnApplicationQuit()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        #region Unused
        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }
        #endregion

        public void OnActiveSceneChanged(Scene _, Scene newScene)
        {
            if (newScene.buildIndex != 4)
                // Return if we're not playing in a song.
                return;

            // We're in a song: attach our component.
            newScene.GetRootGameObjects()[0].AddComponent<LyricsComponent>();
        }
    }
}
