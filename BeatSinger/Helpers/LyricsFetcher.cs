using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSinger
{
    using SimpleJSON;

    /// <summary>
    ///   Defines the time at which a <see cref="Subtitle"/> appears.
    /// </summary>
    public struct SubtitleTime
    {
        public float Total;
        public int Minutes, Seconds, Hundredths;
    }


    /// <summary>
    ///   Defines a subtitle.
    /// </summary>
    public sealed class Subtitle
    {
        public string Text { get; }
        public SubtitleTime Time { get; }

        public Subtitle(JSONNode node)
        {
            JSONNode time = node["time"];

            Text = node["text"].Value;
            
            Time = new SubtitleTime
            {
                Total = time["total"],
                Minutes = time["minutes"],
                Seconds = time["seconds"],
                Hundredths = time["hundredths"]
            };
        }
    }

    /// <summary>
    ///   Provides utilities for asynchronously fetching lyrics.
    /// </summary>
    public static class LyricsFetcher
    {
        /// <summary>
        ///   Fetches the lyrics of the given song asynchronously and, if they're found,
        ///   calls the given callback with the result afterwards.
        /// </summary>
        public static IEnumerator GetLyrics(string song, string artist, Action<Subtitle[]> callback)
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
                    JSONNode subtitleObject = res["message"]["body"]["macro_calls"]["track.subtitles.get"]["message"]["body"]["subtitle_list"].AsArray[0]["subtitle"];
                    JSONArray subtitlesArray = JSON.Parse(subtitleObject["subtitle_body"].Value).AsArray;

                    // No need to sort subtitles here, it should already be done.
                    Subtitle[] subtitles = new Subtitle[subtitlesArray.Count];

                    for (int i = 0; i < subtitles.Length; i++)
                    {
                        subtitles[i] = new Subtitle(subtitlesArray[i]);
                    }

                    // Subtitles found and parsed, do something with them.
                    callback(subtitles);
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
