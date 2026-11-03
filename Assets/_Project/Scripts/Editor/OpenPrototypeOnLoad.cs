using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZombiesFPS.Editor
{
    /// <summary>
    /// When the editor starts on an unsaved, untitled scene (for example a fresh clone), open the
    /// Prototype scene instead so pressing Play runs the game. Never replaces a scene you have open.
    /// </summary>
    [InitializeOnLoad]
    static class OpenPrototypeOnLoad
    {
        const string SessionKey = "ZombiesFPS.OpenPrototypeOnLoad.Done";
        const string ScenePath = "Assets/_Project/Scenes/Prototype.unity";

        static OpenPrototypeOnLoad()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += TryOpen;
        }

        static void TryOpen()
        {
            SessionState.SetBool(SessionKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
