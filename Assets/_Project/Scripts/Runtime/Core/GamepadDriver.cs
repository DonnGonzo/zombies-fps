using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Runs GameInput's per-frame work before any gameplay script reads input, and stops controller rumble
    /// when the game pauses, loses focus or quits. Made automatically; nothing needs to add it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GamepadDriver : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindAnyObjectByType<GamepadDriver>() != null) return;
            var go = new GameObject("GamepadDriver") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<GamepadDriver>();
        }

        void Update() => GameInput.Tick();

        void OnApplicationFocus(bool focused)
        {
            if (!focused) GameInput.StopRumble();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) GameInput.StopRumble();
        }

        void OnDisable() => GameInput.StopRumble();
    }
}
