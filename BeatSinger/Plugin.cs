using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using IllusionPlugin;
using Ryder.Lightweight;
using UnityEngine.SceneManagement;

namespace BeatSinger
{
    /// <summary>
    ///   Entry point of the plugin.
    /// </summary>
    public sealed class Plugin : IPlugin
    {
        internal static Dictionary<string, string> CustomSongs { get; } = new Dictionary<string, string>();
        internal static Redirection GetCustomSongInfoRedirection { get; private set; }

        public string Name => "Beat Singer";
        public string Version => "0.4.0";

        public void OnApplicationStart()
        {
            Settings.Load();

            Type songLoaderType = Type.GetType("SongLoaderPlugin.SongLoader," +
                                               "SongLoaderPlugin," +
                                               "Version=1.0.0.0," +
                                               "Culture=neutral," +
                                               "PublicKeyToken=null");

            if (songLoaderType == null)
                return;

            MethodInfo getCustomSongInfoInfo = songLoaderType.GetMethod("GetCustomSongInfo",
                                                                        BindingFlags.NonPublic |
                                                                        BindingFlags.Instance);

            MethodInfo replacementInfo = typeof(Plugin).GetMethod(nameof(GetCustomSongInfoReplacement),
                                                                  BindingFlags.NonPublic |
                                                                  BindingFlags.Static);


            GetCustomSongInfoRedirection = new Redirection(getCustomSongInfoInfo, replacementInfo, true);

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static object GetCustomSongInfoReplacement(object self, string songPath)
        {
            object result = GetCustomSongInfoRedirection.InvokeOriginal(self, songPath);

            MethodInfo idMethod = result.GetType().GetMethod("GetIdentifier");
            string identifier = (string)idMethod.Invoke(result, new object[0]);

            CustomSongs[identifier] = songPath;

            return result;
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
            if (newScene.buildIndex != 4)
                // Return if we're not playing in a song.
                return;

            // We're in a song: attach our component.
            newScene.GetRootGameObjects()[0].AddComponent<LyricsComponent>();
        }
    }
}
