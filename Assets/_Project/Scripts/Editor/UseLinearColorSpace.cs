using UnityEditor;
using UnityEngine;

namespace ZombiesFPS.Editor
{
    /// <summary>
    /// The lighting, sky and post effects are tuned for Linear colour space (Unity's modern default). A project
    /// opened without its settings file can start in Gamma, which makes everything look washed out and too dark
    /// in the shadows, so switch it once when the editor loads.
    /// </summary>
    [InitializeOnLoad]
    static class UseLinearColorSpace
    {
        static UseLinearColorSpace()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (PlayerSettings.colorSpace == ColorSpace.Linear) return;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Debug.Log("Zombies FPS: switched the project to Linear colour space (Project Settings > Player > Other Settings), which the graphics are tuned for.");
        }
    }
}
