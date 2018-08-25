using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SongLoaderPlugin;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSinger
{
    using SimpleJSON;


    /// <summary>
    ///   Defines a subtitle.
    /// </summary>
    public sealed class Subtitle
    {
        public string Text    { get; }
        public float  Time    { get; }
        public float? EndTime { get; }

        public Subtitle(JSONNode node)
        {
            JSONNode time = node["time"];

            Text = node["text"];

            if (time.IsNumber)
            {
                Time = time;

                if (node["end"])
                    EndTime = node["end"].AsFloat;
            }
            else
            {
                Time = time["total"];
            }
        }

        public Subtitle(string text, float time, float end)
        {
            Text = text;
            Time = time;
            EndTime = end;
        }
    }

    /// <summary>
    ///   Provides utilities for asynchronously fetching lyrics.
    /// </summary>
    public static class LyricsFetcher
    {
        /// <summary>
        ///   Fetches the lyrics of the given song on the local file system and, if they're found,
        ///   populates the given list.
        /// </summary>
        public static bool GetLocalLyrics(string songId, List<Subtitle> subtitles)
        {
            string songDirectory = SongLoader.CustomLevels.Find(x => x.levelID == songId)
                                                         ?.customSongInfo
                                                         ?.GetAudioPath();
            if (songDirectory == null)
                return false;

            Debug.Log("Found song directory: " + songDirectory);

            // Find JSON lyrics
            string jsonFile = Path.Combine(songDirectory, "lyrics.json");

            if (File.Exists(jsonFile))
            {
                string json = File.ReadAllText(jsonFile);
                JSONArray subtitlesArray = JSON.Parse(json).AsArray;

                subtitles.Capacity = subtitlesArray.Count;

                foreach (JSONNode node in subtitlesArray)
                {
                    subtitles.Add(new Subtitle(node));
                }

                return true;
            }

            // Find SRT lyrics
            string srtFile = Path.Combine(songDirectory, "lyrics.srt");

            if (File.Exists(srtFile))
            {
                using (FileStream fs = File.OpenRead(srtFile))
                using (StreamReader reader = new StreamReader(fs))
                {
                    // Parse using a simple state machine:
                    //   0: Parsing number
                    //   1: Parsing start / end time
                    //   2: Parsing text
                    byte state = 0;

                    float startTime = 0f,
                          endTime = 0f;

                    StringBuilder text = new StringBuilder();

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        switch (state)
                        {
                            case 0:
                                if (string.IsNullOrEmpty(line))
                                    // No number found; continue in same state.
                                    continue;

                                if (!int.TryParse(line, out int _))
                                    goto Invalid;

                                // Number found; continue to next state.
                                state = 1;
                                break;

                            case 1:
                                Match m = Regex.Match(line, @"(\d+):(\d+):(\d+,\d+) *--> *(\d+):(\d+):(\d+,\d+)");

                                if (!m.Success)
                                    goto Invalid;

                                startTime = int.Parse(m.Groups[1].Value) * 3600
                                          + int.Parse(m.Groups[2].Value) * 60
                                          + float.Parse(m.Groups[3].Value.Replace(',', '.'));

                                endTime = int.Parse(m.Groups[4].Value) * 3600
                                        + int.Parse(m.Groups[5].Value) * 60
                                        + float.Parse(m.Groups[6].Value.Replace(',', '.'));

                                // Subtitle start / end found; continue to next state.
                                state = 2;
                                break;

                            case 2:
                                if (string.IsNullOrEmpty(line))
                                {
                                    // End of text; continue to next state.
                                    subtitles.Add(new Subtitle(text.ToString(), startTime, endTime));

                                    text.Length = 0;
                                    state = 0;
                                }
                                else
                                {
                                    // Continuation of text; continue in same state.
                                    text.AppendLine(line);
                                }

                                break;

                            default:
                                // Shouldn't happen.
                                throw new Exception();
                        }
                    }

                    return true;
                }

                Invalid:

                Debug.Log("Invalid subtiles file found, cancelling load...");
                subtitles.Clear();
            }

            return false;
        }

        /// <summary>
        ///   Fetches the lyrics of the given song online asynchronously and, if they're found,
        ///   populates the given list.
        /// </summary>
        public static IEnumerator GetOnlineLyrics(string song, string artist, List<Subtitle> subtitles)
        {
            // Perform request
            string qTrack = UnityWebRequest.EscapeURL(song);
            string qArtist = UnityWebRequest.EscapeURL(artist);
            string url = $"https://apic-desktop.musixmatch.com/ws/1.1/macro.subtitles.get?format=json&q_track={qTrack}&q_artist={qArtist}&user_language=en&userblob_id=aG9va2VkIG9uIGEgZmVlbGluZ19ibHVlIHN3ZWRlXzE3Mg&subtitle_format=mxm&app_id=web-desktop-app-v1.0&usertoken=180220daeb2405592f296c4aea0f6d15e90e08222b559182bacf92";

            UnityWebRequest req = UnityWebRequest.Get(url);

            req.SetRequestHeader("Cookie", "x-mxm-token-guid=cd25ed55-85ea-445b-83cd-c4b173e20ce7");

            yield return req.SendWebRequest();

            if (req.isNetworkError || req.isHttpError)
            {
                Debug.Log(req.error);
            }
            else
            {
                // Request done, process result
                try
                {
                    JSONNode res = JSON.Parse(req.downloadHandler.text);
                    JSONNode subtitleObject = res["message"]["body"]["macro_calls"]["track.subtitles.get"]
                                                 ["message"]["body"]["subtitle_list"]
                                                 .AsArray[0]["subtitle"];

                    JSONArray subtitlesArray = JSON.Parse(subtitleObject["subtitle_body"].Value).AsArray;

                    // No need to sort subtitles here, it should already be done.
                    subtitles.Capacity = subtitlesArray.Count;

                    foreach (JSONNode node in subtitlesArray)
                    {
                        subtitles.Add(new Subtitle(node));
                    }
                }
                catch (NullReferenceException)
                {
                    // JSON key not found.
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            req.Dispose();
        }
    }
}
