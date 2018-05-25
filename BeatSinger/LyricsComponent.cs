using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BeatSinger
{
    /// <summary>
    ///   Defines the main component of BeatSinger, which displays lyrics on loaded songs.
    /// </summary>
    public sealed class LyricsComponent : MonoBehaviour
    {
        // Keep track of the latest chosen option globally.
        private static bool shouldEnable = true;
        
        private static readonly FieldInfo DurationField = typeof(FlyingTextSpawner).GetField("_duration", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SongFileField = typeof(GameSongController).GetField("_songFile", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AudioTimeSyncField = typeof(GameSongController).GetField("_audioTimeSyncController", BindingFlags.NonPublic | BindingFlags.Instance);

        private AudioClip audioClip;
        private LevelStaticData levelData;
        private GameSongController songController;
        private FlyingTextSpawner textSpawner;

        private bool isEnabled = shouldEnable;

        public void Start()
        {
            // The goal now is to find the clip this scene will be playing.
            // For this, we find the single root gameObject (which is the gameObject
            // to which we are attached),
            // then we get its GameSongController to find the audio clip,
            // and its FlyingTextSpawner to display the lyrics.

            textSpawner = gameObject.GetComponentInChildren<FlyingTextSpawner>();
            songController = gameObject.GetComponentInChildren<GameSongController>();

            if (textSpawner == null || songController == null)
                return;


            audioClip = (AudioClip)SongFileField.GetValue(songController);

            // Clip found, now select the first song that has the same clip (using reference comparison).
            levelData = (from world in PersistentSingleton<GameDataModel>.instance.gameStaticData.worldsData
                         from levelStaticData in world.levelsData
                         from difficultyLevel in levelStaticData.difficultyLevels
                         where ReferenceEquals(difficultyLevel.audioClip, audioClip)
                         select levelStaticData).FirstOrDefault();

            if (levelData == null)
            {
                Debug.Log("Corresponding song not found.");

                return;
            }

            // We found the matching song, we can get started.
            Debug.Log($"Corresponding song data found: {levelData.songName} by {levelData.authorName}.");

            // When this coroutine ends, it will call the given callback with a list
            // of all the subtitles we found, and allow us to react.
            // If no subs are found, the callback is not called.
            StartCoroutine(LyricsFetcher.GetLyrics(levelData.songName, levelData.authorName, subs =>
            {
                SpawnText("Lyrics found", 3f);
                StartCoroutine(DisplayLyrics(subs));
            }));
        }

        public void Destroy()
        {
            shouldEnable = isEnabled;
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                isEnabled = !isEnabled;
            }
        }

        private IEnumerator DisplayLyrics(Subtitle[] subtitles)
        {
            AudioTimeSyncController audio = (AudioTimeSyncController)AudioTimeSyncField.GetValue(songController);

            // Since subtitles are sorted by time of appearance, we save in the index variable
            // the next subtitle to display (initially the first one at index 0), and when
            // said subtitle is supposed to appear, we spawn it using SpawnText, until either
            // the end of the song if it's the last subtitle, or the next subtitle otherwise.
            for (int i = 0; i < subtitles.Length;)
            {
                Subtitle subtitle = subtitles[i];

                float subtitleTime = subtitle.Time.Total,
                      currentTime = audio.songTime;

                if (subtitleTime > currentTime)
                {
                    // Not there yet, wait for next subtitle.
                    if (subtitleTime - currentTime > 1f)
                        yield return new WaitForSeconds(subtitleTime - currentTime);
                    else
                        yield return new WaitForEndOfFrame();

                    continue;
                }

                // We good, display subtitle and increase current index.
                if (isEnabled)
                    SpawnText(subtitle.Text, ++i == subtitles.Length ? audio.songLength - currentTime
                                                                     : subtitles[i].Time.Total - currentTime);
                else
                    i++;
            }
        }

        private void SpawnText(string text, float duration)
        {
            // Little hack to spawn text for a chosen duration in seconds:
            // Save the initial float _duration field to a variable,
            // then set it to the chosen duration, call SpawnText, and restore the
            // previously saved duration.
            object initialDuration = DurationField.GetValue(textSpawner);

            DurationField.SetValue(textSpawner, duration);
            textSpawner.SpawnText(new Vector3(0, 4, 0), text);
            DurationField.SetValue(textSpawner, initialDuration);
        }
    }
}
