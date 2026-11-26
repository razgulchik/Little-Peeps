using UnityEngine;

namespace LittlePeeps
{
    // The Quit buttons' one way out. In the editor Application.Quit does nothing, so there it leaves Play
    // mode instead — the same click means "stop the game" in both.
    public static class AppQuit
    {
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
