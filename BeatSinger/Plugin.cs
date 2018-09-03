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
        public string Version => "0.6.0";

        public void OnApplicationStart()
        {
            Settings.Load();

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        public void OnApplicationQuit()
        {
            Settings.Save();

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
            switch (newScene.buildIndex)
            {
                case 1:
                case 2:
                    // Return if we're not playing in a song.
                    return;
                case 8:
                case 9:
                    // We're in a song: attach our component.
                    newScene.GetRootGameObjects()[0].AddComponent<LyricsComponent>();
                    return;
                
                default:
                    // Unknown, fallback to matching the name.
                    if (newScene.name.Contains("Environment"))
                        goto case 8;
                    else
                        return;
            }
        }
    }
}
