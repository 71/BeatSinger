using IllusionPlugin;
using UnityEngine;

namespace BeatSinger
{
    public sealed class Plugin : IPlugin
    {
        public string Name => "Beat Singer";
        public string Version => "0.1.0";

        public void OnApplicationQuit()
        {
        }

        public void OnApplicationStart()
        {
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            if (level == 1)
                // Load the lyrics component as soon as we reach the menu.
                new GameObject("LyricsObject").AddComponent<LyricsComponent>();
        }

        public void OnUpdate()
        {
        }
    }
}
