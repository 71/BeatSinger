using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace BeatSinger
{
    /// <summary>
    ///   Defines the main component of BeatSinger, which displays lyrics on loaded songs.
    /// </summary>
    public sealed class LyricsComponent : MonoBehaviour
    {
        private const BindingFlags NON_PUBLIC_INSTANCE = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly FieldInfo AudioTimeSyncField
            = typeof(GameSongController).GetField("_audioTimeSyncController", NON_PUBLIC_INSTANCE);

        private static readonly FieldInfo SceneSetupDataField
            = typeof(SceneSetup<GameplayCoreSceneSetupData>).GetField("_sceneSetupData", NON_PUBLIC_INSTANCE);

        private static readonly FieldInfo ContainerField
            = typeof(Zenject.MonoInstallerBase).GetField("<Container>k__BackingField", NON_PUBLIC_INSTANCE);

        private static readonly FieldInfo FlyingTextEffectPoolField
            = typeof(FlyingTextSpawner).GetField("_flyingTextEffectPool", NON_PUBLIC_INSTANCE);

        private static readonly Func<FlyingTextSpawner, float> GetTextSpawnerDuration;
        private static readonly Action<FlyingTextSpawner, float> SetTextSpawnerDuration;

        static LyricsComponent()
        {
            FieldInfo durationField = typeof(FlyingTextSpawner).GetField("_duration", NON_PUBLIC_INSTANCE);

            if (durationField == null)
                throw new Exception("Cannot find _duration field of FlyingTextSpawner.");

            // Create dynamic setter
            DynamicMethod setterMethod = new DynamicMethod("SetDuration", typeof(void), new[] { typeof(FlyingTextSpawner), typeof(float) }, typeof(FlyingTextSpawner));
            ILGenerator setterIl = setterMethod.GetILGenerator(16);

            setterIl.Emit(OpCodes.Ldarg_0);
            setterIl.Emit(OpCodes.Ldarg_1);
            setterIl.Emit(OpCodes.Stfld, durationField);
            setterIl.Emit(OpCodes.Ret);

            SetTextSpawnerDuration = setterMethod.CreateDelegate(typeof(Action<FlyingTextSpawner, float>))
                                  as Action<FlyingTextSpawner, float>;

            // Create dynamic getter
            DynamicMethod getterMethod = new DynamicMethod("GetDuration", typeof(float), new[] { typeof(FlyingTextSpawner) }, typeof(FlyingTextSpawner));
            ILGenerator getterIl = getterMethod.GetILGenerator(16);

            getterIl.Emit(OpCodes.Ldarg_0);
            getterIl.Emit(OpCodes.Ldfld, durationField);
            getterIl.Emit(OpCodes.Ret);

            GetTextSpawnerDuration = getterMethod.CreateDelegate(typeof(Func<FlyingTextSpawner, float>))
                                  as Func<FlyingTextSpawner, float>;
        }


        private GameSongController songController;
        private FlyingTextSpawner textSpawner;
        private AudioTimeSyncController audio;

        public IEnumerator Start()
        {
            // The goal now is to find the clip this scene will be playing.
            // For this, we find the single root gameObject (which is the gameObject
            // to which we are attached),
            // then we get its GameSongController to find the audio clip,
            // and its FlyingTextSpawner to display the lyrics.

            if (Settings.VerboseLogging)
            {
                Debug.Log( "[Beat Singer] Attached to scene.");
                Debug.Log($"[Beat Singer] Lyrics are enabled: {Settings.DisplayLyrics}.");
            }

            textSpawner = FindObjectOfType<FlyingTextSpawner>();
            songController = FindObjectOfType<GameSongController>();

            var sceneSetup = FindObjectOfType<GameplayCoreSceneSetup>();

            if (songController == null || sceneSetup == null)
                yield break;

            if (textSpawner == null)
            {
                var installer = FindObjectOfType<EffectPoolsInstaller>();
                var container = (Zenject.DiContainer)ContainerField.GetValue(installer);

                textSpawner = container.InstantiateComponentOnNewGameObject<FlyingTextSpawner>();
            }

            var sceneSetupData = (GameplayCoreSceneSetupData)SceneSetupDataField.GetValue(sceneSetup);

            if (sceneSetupData == null)
                yield break;

            audio = (AudioTimeSyncController)AudioTimeSyncField.GetValue(songController);

            IBeatmapLevel level = sceneSetupData.difficultyBeatmap.level;
            List<Subtitle> subtitles = new List<Subtitle>();

            Debug.Log($"[Beat Singer] Corresponding song data found: {level.songName} by {level.songAuthorName} ({(level.songSubName != null ? level.songSubName : "No sub-name")}).");

            if (LyricsFetcher.GetLocalLyrics(sceneSetupData.difficultyBeatmap.level.levelID, subtitles))
            {
                Debug.Log( "[Beat Singer] Found local lyrics.");
                Debug.Log($"[Beat Singer] These lyrics can be uploaded online using the ID: \"{level.GetLyricsHash()}\".");

                // Lyrics found locally, continue with them.
                SpawnText("Lyrics found locally", 3f);
            }
            else
            {
                Debug.Log("[Beat Singer] Did not find local lyrics, trying online lyrics...");

                // When this coroutine ends, it will call the given callback with a list
                // of all the subtitles we found, and allow us to react.
                // If no subs are found, the callback is not called.
                yield return StartCoroutine(LyricsFetcher.GetOnlineLyrics(level, subtitles));

                if (subtitles.Count != 0)
                    goto FoundOnlineLyrics;

                yield return StartCoroutine(LyricsFetcher.GetMusixmatchLyrics(level.songName, level.songAuthorName, subtitles));

                if (subtitles.Count != 0)
                    goto FoundOnlineLyrics;

                yield return StartCoroutine(LyricsFetcher.GetMusixmatchLyrics(level.songName, level.songSubName, subtitles));

                if (subtitles.Count != 0)
                    goto FoundOnlineLyrics;

                yield break;

                FoundOnlineLyrics:
                SpawnText("Lyrics found online", 3f);
            }

            StartCoroutine(DisplayLyrics(subtitles));
        }

        public void Update()
        {
            if (!Input.GetKeyUp((KeyCode)Settings.ToggleKeyCode))
                return;

            Settings.DisplayLyrics = !Settings.DisplayLyrics;

            SpawnText(Settings.DisplayLyrics ? "Lyrics enabled" : "Lyrics disabled", 3f);
        }

        private IEnumerator DisplayLyrics(IList<Subtitle> subtitles)
        {
            // Subtitles are sorted by time of appearance, so we can iterate without sorting first.
            int i = 0;

            // First, skip all subtitles that have already been seen.
            {
                float currentTime = audio.songTime;

                while (i < subtitles.Count)
                {
                    Subtitle subtitle = subtitles[i];

                    if (subtitle.Time >= currentTime)
                        // Subtitle appears after current moment, stop skipping
                        break;

                    i++;
                }
            }

            if (Settings.VerboseLogging && i > 0)
                Debug.Log($"[Beat Singer] Skipped {i} lyrics because they started too soon.");

            // Display all lyrics
            while (i < subtitles.Count)
            {
                // Wait for time to display next lyrics
                yield return new WaitForSeconds(subtitles[i++].Time - audio.songTime + Settings.DisplayDelay);

                if (!Settings.DisplayLyrics)
                    // Don't display lyrics this time
                    continue;

                // We good, display lyrics
                Subtitle subtitle = subtitles[i - 1];

                float displayDuration,
                      currentTime = audio.songTime;

                if (subtitle.EndTime.HasValue)
                {
                    displayDuration = subtitle.EndTime.Value - currentTime;
                }
                else
                {
                    displayDuration = i == subtitles.Count
                                    ? audio.songLength - currentTime
                                    : subtitles[i].Time - currentTime;
                }

                if (Settings.VerboseLogging)
                    Debug.Log($"[Beat Singer] At {currentTime} and for {displayDuration} seconds, displaying lyrics \"{subtitle.Text}\".");

                SpawnText(subtitle.Text, displayDuration + Settings.HideDelay);
            }
        }

        private void SpawnText(string text, float duration)
        {
            // Little hack to spawn text for a chosen duration in seconds:
            // Save the initial float _duration field to a variable,
            // then set it to the chosen duration, call SpawnText, and restore the
            // previously saved duration.
            float initialDuration = GetTextSpawnerDuration(textSpawner);

            SetTextSpawnerDuration(textSpawner, duration);
            textSpawner.SpawnText(new Vector3(0, 4, 0), text);
            SetTextSpawnerDuration(textSpawner, initialDuration);
        }
    }
}
