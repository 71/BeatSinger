using System.Collections;
using System.Collections.Generic;
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
        private static bool shouldDisplayLyrics = true;

        private static readonly FieldInfo AudioTimeSyncField  = typeof(GameSongController).GetField("_audioTimeSyncController", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DurationField       = typeof(FlyingTextSpawner).GetField("_duration", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SetupDataField      = typeof(MainGameSceneSetup).GetField("_mainGameSceneSetupData", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SongFileField       = typeof(GameSongController).GetField("_songFile", BindingFlags.NonPublic | BindingFlags.Instance);

        private LevelStaticData levelData;
        private GameSongController songController;
        private FlyingTextSpawner textSpawner;

        public IEnumerator Start()
        {
            // The goal now is to find the clip this scene will be playing.
            // For this, we find the single root gameObject (which is the gameObject
            // to which we are attached),
            // then we get its GameSongController to find the audio clip,
            // and its FlyingTextSpawner to display the lyrics.


            textSpawner = gameObject.GetComponentInChildren<FlyingTextSpawner>();
            songController = gameObject.GetComponentInChildren<GameSongController>();

            if (textSpawner == null || songController == null)
                yield break;

            Debug.Log(FindObjectsOfType<MainGameSceneSetup>().Length);

            MainGameSceneSetup sceneSetup = FindObjectOfType<MainGameSceneSetup>();

            if (sceneSetup == null)
                yield break;

            MainGameSceneSetupData sceneSetupData = SetupDataField.GetValue(sceneSetup) as MainGameSceneSetupData;

            if (sceneSetupData == null)
                yield break;

            List<Subtitle> subtitles = new List<Subtitle>();

            if (LyricsFetcher.GetLocalLyrics(sceneSetupData.levelId, subtitles))
            {
                // Lyrics found locally, continue with them.
                SpawnText("Lyrics found locally", 3f);
            }
            else
            {
                // Clip found, now select the first song that has the same clip (using reference comparison).
                AudioClip audioClip = (AudioClip)SongFileField.GetValue(songController);

                levelData = (from world in PersistentSingleton<GameDataModel>.instance.gameStaticData.worldsData
                             from levelStaticData in world.levelsData
                             from difficultyLevel in levelStaticData.difficultyLevels
                             where ReferenceEquals(difficultyLevel.audioClip, audioClip)
                             select levelStaticData).FirstOrDefault();

                if (levelData == null)
                {
                    Debug.Log("Corresponding song not found.");

                    yield break;
                }

                // We found the matching song, we can get started.
                Debug.Log($"Corresponding song data found: {levelData.songName} by {levelData.authorName}.");

                // When this coroutine ends, it will call the given callback with a list
                // of all the subtitles we found, and allow us to react.
                // If no subs are found, the callback is not called.
                yield return StartCoroutine(LyricsFetcher.GetOnlineLyrics(levelData.songName, levelData.authorName, subtitles));

                if (subtitles.Count == 0)
                    yield break;

                SpawnText("Lyrics found online", 3f);
            }

            StartCoroutine(DisplayLyrics(subtitles));
        }

        public void Update()
        {
            if (!Input.GetKeyDown(KeyCode.S))
                return;

            shouldDisplayLyrics = !shouldDisplayLyrics;

            SpawnText(shouldDisplayLyrics ? "Lyrics enabled." : "Lyrics disabled.", 3f);
        }

        private IEnumerator DisplayLyrics(IList<Subtitle> subtitles)
        {
            AudioTimeSyncController audio = (AudioTimeSyncController)AudioTimeSyncField.GetValue(songController);

            // Since subtitles are sorted by time of appearance, we save in the index variable
            // the next subtitle to display (initially the first one at index 0), and when
            // said subtitle is supposed to appear, we spawn it using SpawnText, until either
            // the end of the song if it's the last subtitle, or the next subtitle otherwise.
            for (int i = 0; i < subtitles.Count;)
            {
                Subtitle subtitle = subtitles[i];

                float subtitleTime = subtitle.Time,
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
                float displayDuration;

                if (subtitle.EndTime.HasValue)
                {
                    displayDuration = subtitle.EndTime.Value - currentTime;
                    i++;
                }
                else
                {
                    displayDuration = ++i == subtitles.Count
                                    ? audio.songLength - currentTime
                                    : subtitles[i].Time - currentTime;
                }

                if (shouldDisplayLyrics)
                    SpawnText(subtitle.Text, displayDuration);
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
