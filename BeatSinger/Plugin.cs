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
        internal static Redirection RetrieveAllSongsRedirection { get; private set; }

        public string Name => "Beat Singer";
        public string Version => "0.3.1";

        public void OnApplicationStart()
        {
            Type songLoaderType = Type.GetType("SongLoaderPlugin.SongLoader," +
                                               "SongLoaderPlugin," +
                                               "Version=1.0.0.0," +
                                               "Culture=neutral," +
                                               "PublicKeyToken=null");

            if (songLoaderType == null)
                return;

            MethodInfo retrieveAllSongsInfo = songLoaderType.GetMethod("RetrieveAllSongs",
                                                                       BindingFlags.NonPublic |
                                                                       BindingFlags.Instance);

            MethodInfo replacementInfo = typeof(Plugin).GetMethod(nameof(RetrieveAllSongsReplacement),
                                                                  BindingFlags.NonPublic |
                                                                  BindingFlags.Static);


            RetrieveAllSongsRedirection = new Redirection(retrieveAllSongsInfo, replacementInfo, true);

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private static object RetrieveAllSongsReplacement(object self)
        {
            IEnumerable result = (IEnumerable)RetrieveAllSongsRedirection.InvokeOriginal(self);

            foreach (object song in result)
            {
                if (song == null)
                    continue;

                FieldInfo pathField = song.GetType().GetField("path");
                MethodInfo idMethod = song.GetType().GetMethod("GetIdentifier");

                CustomSongs[(string)idMethod.Invoke(song, new object[0])] = (string)pathField.GetValue(song);
            }

            return result;
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
