using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZombiesFPS.Core
{
    public enum DebugKey
    {
        ZombiesUp,
        ZombiesDown,
        RoundUp,
        RoundDown,
        Restart,
        ToggleHelp,
    }

    /// <summary>
    /// Thin input facade so gameplay code does not care which input backend is active.
    /// Works with the Input System package or the legacy Input Manager, whichever the project has enabled.
    /// Phase 1 swaps the internals for an InputActionAsset with rebinding; call sites stay the same.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Right-stick look speed, in the same units as mouse pixels per second.</summary>
        public const float GamepadLookSpeed = 1400f;

#if ENABLE_INPUT_SYSTEM
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
                if (K != null)
                {
                    if (K.wKey.isPressed) v.y += 1f;
                    if (K.sKey.isPressed) v.y -= 1f;
                    if (K.dKey.isPressed) v.x += 1f;
                    if (K.aKey.isPressed) v.x -= 1f;
                }
                if (G != null) v += G.leftStick.ReadValue();
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Look delta this frame, in mouse pixels.</summary>
        public static Vector2 Look
        {
            get
            {
                Vector2 v = M != null ? M.delta.ReadValue() : Vector2.zero;
                if (G != null) v += G.rightStick.ReadValue() * GamepadLookSpeed * Time.deltaTime;
                return v;
            }
        }

        public static bool JumpPressed => (K != null && K.spaceKey.wasPressedThisFrame) || (G != null && G.buttonSouth.wasPressedThisFrame);
        public static bool SprintHeld => (K != null && K.leftShiftKey.isPressed) || (G != null && G.leftStickButton.isPressed);
        public static bool FireHeld => (M != null && M.leftButton.isPressed) || (G != null && G.rightTrigger.isPressed);
        public static bool FirePressed => (M != null && M.leftButton.wasPressedThisFrame) || (G != null && G.rightTrigger.wasPressedThisFrame);
        public static bool AimHeld => (M != null && M.rightButton.isPressed) || (G != null && G.leftTrigger.isPressed);
        public static bool ReloadPressed => (K != null && K.rKey.wasPressedThisFrame) || (G != null && G.buttonWest.wasPressedThisFrame);
        public static bool PausePressed => (K != null && K.escapeKey.wasPressedThisFrame) || (G != null && G.startButton.wasPressedThisFrame);
        public static bool InteractHeld => (K != null && K.eKey.isPressed) || (G != null && G.buttonNorth.isPressed);

        public static bool DebugPressed(DebugKey key)
        {
            if (K == null) return false;
            switch (key)
            {
                case DebugKey.ZombiesUp: return K.rightBracketKey.wasPressedThisFrame;
                case DebugKey.ZombiesDown: return K.leftBracketKey.wasPressedThisFrame;
                case DebugKey.RoundUp: return K.equalsKey.wasPressedThisFrame;
                case DebugKey.RoundDown: return K.minusKey.wasPressedThisFrame;
                case DebugKey.Restart: return K.enterKey.wasPressedThisFrame || (G != null && G.startButton.wasPressedThisFrame);
                case DebugKey.ToggleHelp: return K.hKey.wasPressedThisFrame;
                default: return false;
            }
        }
#else
        public static Vector2 Move => Vector2.ClampMagnitude(new Vector2(
            (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
            (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f)), 1f);

        // The default Input Manager scales mouse axes by 0.1, so x10 gets back to roughly pixels.
        public static Vector2 Look => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;

        public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
        public static bool SprintHeld => Input.GetKey(KeyCode.LeftShift);
        public static bool FireHeld => Input.GetMouseButton(0);
        public static bool FirePressed => Input.GetMouseButtonDown(0);
        public static bool AimHeld => Input.GetMouseButton(1);
        public static bool ReloadPressed => Input.GetKeyDown(KeyCode.R);
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
        public static bool InteractHeld => Input.GetKey(KeyCode.E);

        public static bool DebugPressed(DebugKey key)
        {
            switch (key)
            {
                case DebugKey.ZombiesUp: return Input.GetKeyDown(KeyCode.RightBracket);
                case DebugKey.ZombiesDown: return Input.GetKeyDown(KeyCode.LeftBracket);
                case DebugKey.RoundUp: return Input.GetKeyDown(KeyCode.Equals);
                case DebugKey.RoundDown: return Input.GetKeyDown(KeyCode.Minus);
                case DebugKey.Restart: return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                case DebugKey.ToggleHelp: return Input.GetKeyDown(KeyCode.H);
                default: return false;
            }
        }
#endif
    }
}
