using System;
using IllusionPlugin;
using UnityEngine.XR;

namespace BeatSinger
{
    public static class Settings
    {
        public const string PrefsSection = "BeatSinger";

        public static bool DisplayLyrics { get; set; }
        public static int  ToggleKeyCode { get; set; }
        public static float DisplayDelay { get; set; }
        public static float HideDelay    { get; set; }
        public static bool VerboseLogging { get; set; }

        public static void Save()
        {
            ModPrefs.SetBool (PrefsSection, "Enabled"            , DisplayLyrics);
            ModPrefs.SetInt  (PrefsSection, nameof(ToggleKeyCode), ToggleKeyCode);
            ModPrefs.SetFloat(PrefsSection, nameof(DisplayDelay) , DisplayDelay);
            ModPrefs.SetFloat(PrefsSection, nameof(HideDelay)    , HideDelay);

            if (VerboseLogging)
                ModPrefs.SetBool(PrefsSection, nameof(VerboseLogging), true);
        }

        public static void Load()
        {
            int defaultKeycode;

            if (XRDevice.model.IndexOf("rift", StringComparison.InvariantCultureIgnoreCase) != -1)
                defaultKeycode = (int)ConInput.Oculus.LeftThumbstickPress;
            else if (XRDevice.model.IndexOf("vive", StringComparison.InvariantCultureIgnoreCase) != -1)
                defaultKeycode = (int)ConInput.Vive.LeftTrackpadPress;
            else
                defaultKeycode = (int)ConInput.WinMR.LeftThumbstickPress;

            DisplayLyrics = ModPrefs.GetBool(PrefsSection, "Enabled"            , true);
            ToggleKeyCode = ModPrefs.GetInt (PrefsSection, nameof(ToggleKeyCode), defaultKeycode);

            DisplayDelay = ModPrefs.GetFloat(PrefsSection, nameof(DisplayDelay), -.1f);
            HideDelay    = ModPrefs.GetFloat(PrefsSection, nameof(HideDelay)   , 0f);

            VerboseLogging = ModPrefs.GetBool(PrefsSection, nameof(VerboseLogging), false);
        }
    }
}
