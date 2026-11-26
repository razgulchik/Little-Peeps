using UnityEngine;

namespace LittlePeeps
{
    // The player's settings — the machine's, not the profile's: kept in PlayerPrefs, so a progress reset
    // never touches them and a later profile save does not have to carry them.
    //
    // Volume is the one this game stores itself. The window mode is remembered by the player between
    // launches on its own (Unity keeps the last screen mode and size), so it is only set here.
    public static class GameSettings
    {
        private const string VolumeKey = "LittlePeeps.Volume";

        // The whole game's volume, 0..1. AudioListener.volume, so it covers every sound, through a mixer
        // group or straight to the listener. Separate music and effects come with music and a mixer.
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, volume);
                AudioListener.volume = volume;
            }
        }

        // At boot (GameBootstrap.Awake), before anything can make a sound.
        public static void ApplySaved() => AudioListener.volume = Volume;

        // Write what changed to disk now rather than at quit, which a crash never reaches.
        public static void Save() => PlayerPrefs.Save();

        public static bool Fullscreen => Screen.fullScreen;

        // The game's own pixel size (the Pixel Perfect Camera's reference resolution and the Canvas's width):
        // a window a whole number of these keeps every pixel of the game the same size.
        private const int ReferenceWidth = 640;
        private const int ReferenceHeight = 360;

        // Fullscreen on the whole display, or a window. A window takes the largest whole multiple of the
        // game's pixel size that leaves room for the window frame on this display (1280×720 on 1080p, 1920×1080
        // on 1440p), never smaller than one; leaving fullscreen by mode alone would keep the display's size,
        // a window as big as the screen. Takes effect at the end of the frame, so Screen.fullScreen reads
        // the old mode until then.
        public static void SetFullscreen(bool fullscreen)
        {
            int displayWidth = Display.main.systemWidth;
            int displayHeight = Display.main.systemHeight;

            if (fullscreen)
            {
                Screen.SetResolution(displayWidth, displayHeight, FullScreenMode.FullScreenWindow);
                return;
            }

            // Strictly smaller than the display in both directions: a window exactly its size has no room
            // for its title bar.
            int scale = Mathf.Max(1, Mathf.Min((displayWidth - 1) / ReferenceWidth, (displayHeight - 1) / ReferenceHeight));
            Screen.SetResolution(ReferenceWidth * scale, ReferenceHeight * scale, FullScreenMode.Windowed);
        }
    }
}
