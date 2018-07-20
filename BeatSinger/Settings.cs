using IllusionPlugin;

namespace BeatSinger
{
    public static class Settings
    {
        public const string PrefsSection = "BeatSinger";

        public static bool DisplayLyrics { get; set; }

        public static void Save()
        {
            ModPrefs.SetBool(PrefsSection, "Enabled", DisplayLyrics);
        }

        public static void Load()
        {
            DisplayLyrics = ModPrefs.GetBool(PrefsSection, "Enabled", true);
        }
    }
}
