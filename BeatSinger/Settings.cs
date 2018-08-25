using IllusionPlugin;
using UnityEngine.XR;

namespace BeatSinger
{
    public static class Settings
    {
        public const string PrefsSection = "BeatSinger";

        public static bool DisplayLyrics { get; set; }
        public static int ToggleKeyCode { get; set; }

        public static void Save()
        {
            ModPrefs.SetBool(PrefsSection, "Enabled", DisplayLyrics);
            ModPrefs.SetInt(PrefsSection, nameof(ToggleKeyCode), ToggleKeyCode);
        }

        public static void Load()
        {
            int defaultKeycode;

            if (XRDevice.model.ToLower().Contains("rift"))
                defaultKeycode = (int)ConInput.Oculus.LeftThumbstickPress;
            else if (XRDevice.model.ToLower().Contains("vive"))
                defaultKeycode = (int)ConInput.Vive.LeftTrackpadPress;
            else
                defaultKeycode = (int)ConInput.WinMR.LeftThumbstickPress;

            DisplayLyrics = ModPrefs.GetBool(PrefsSection, "Enabled", true);
            ToggleKeyCode = ModPrefs.GetInt(PrefsSection, nameof(ToggleKeyCode), defaultKeycode);
        }
    }
}
