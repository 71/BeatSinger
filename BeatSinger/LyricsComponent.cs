using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSinger
{
    /// <summary>
    ///   Defines the main component of BeatSinger, which displays lyrics on loaded songs.
    /// </summary>
    public sealed class LyricsComponent : MonoBehaviour
    {
        private static readonly FieldInfo DurationField = typeof(FlyingTextSpawner).GetField("_duration", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SongFileField = typeof(GameSongController).GetField("_songFile", BindingFlags.NonPublic | BindingFlags.Instance);

        // ReSharper disable once InconsistentNaming
        private static bool IsInitialized;

        // ReSharper disable once UnusedMember.Local
        private void Awake()
        {
            if (IsInitialized)
                return;

            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            IsInitialized = true;
            DontDestroyOnLoad(gameObject);
        }

        private void OnActiveSceneChanged(Scene _, Scene newScene)
        {
            if (newScene.buildIndex != 4)
                // Return if we're not playing in a song.
                return;

            // The goal now is to find the clip this scene will be playing.
            // For this, we find the single root gameObject (wrapper),
            // then we get its GameSongController to find the audio clip,
            // and its FlyingTextSpawner to display the lyrics.
            GameObject wrapper = newScene.GetRootGameObjects()[0];

            FlyingTextSpawner textSpawner = wrapper.GetComponentInChildren<FlyingTextSpawner>();
            SongController controller = wrapper.GetComponentInChildren<GameSongController>();

            AudioClip clip = (AudioClip)SongFileField.GetValue(controller);

            // Clip found, now select the first song that has the same clip (using reference comparison).
            LevelStaticData levelData =
                   (from world in PersistentSingleton<GameDataModel>.instance.gameStaticData.worldsData
                    from levelStaticData in world.levelsData
                    from difficultyLevel in levelStaticData.difficultyLevels
                    where ReferenceEquals(difficultyLevel.audioClip, clip)
                    select levelStaticData).FirstOrDefault();

            if (levelData == null)
            {
                Debug.Log("Corresponding song not found.");

                return;
            }

            // We found the matching song, we can get started.
            Debug.Log($"Corresponding song data found: {levelData.songName} by {levelData.authorName}.");

            int index = 0;
            Subtitle[] subtitles = null;

            // When this coroutine ends, it will call the given callback with a list
            // of all the subtitles we found, and allow us to react.
            // If no subs are found, the callback is not called.
            StartCoroutine(LyricsFetcher.GetLyrics(levelData.songName, levelData.authorName, subs =>
            {
                SpawnText("Lyrics found", 3f);

                subtitles = subs;

                controller.songEvent += Callback;
                controller.songDidFinishEvent += () => controller.songEvent -= Callback;
            }));

            void Callback(SongEventData ev)
            {
                if (subtitles == null)
                    return;

                // Since subtitles are sorted by time of appearance, we save in the index variable
                // the next subtitle to display (initially the first one at index 0), and when
                // said subtitle is supposed to appear, we spawn it using SpawnText, until either
                // the end of the song if it's the last subtitle, or the next subtitle otherwise.
                for (int i = index; i < subtitles.Length; i++)
                {
                    Subtitle subtitle = subtitles[i];

                    if (subtitle.Time.Total > ev.time)
                        continue;

                    index = i + 1;
                    SpawnText(subtitle.Text, i == subtitles.Length - 1 ? clip.length - ev.time : subtitles[i + 1].Time.Total - ev.time);

                    return;
                }
            }

            void SpawnText(string text, float duration)
            {
                // Little hack to spawn text for a chosen duration in seconds:
                // Save the initial float _duration field to a variable,
                // then set it to the chosen duration, call SpawnText, and restore the
                // previously saved duration.
                object initialDuration = DurationField.GetValue(textSpawner);

                DurationField.SetValue(textSpawner, duration);
                textSpawner.SpawnText(Vector3.one, text);
                DurationField.SetValue(textSpawner, initialDuration);
            }
        }
    }
}
