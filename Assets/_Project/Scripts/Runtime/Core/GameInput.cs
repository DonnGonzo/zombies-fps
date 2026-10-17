using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace ZombiesFPS.Core
{
    public enum DebugKey
    {
        NextRound,
        JumpTenRounds,
        GodMode,
        GivePoints,
        Restart,
        ToggleHelp,
        OfferPowerUp,
        SpawnBoss,
        Console,
    }

    /// <summary>
    /// Input facade so gameplay code does not care which input backend is active. Works with the Input System
    /// package or the legacy Input Manager, whichever the project has enabled. Keyboard and mouse actions follow
    /// the player's bindings (Bindings, changed in Settings); the gamepad layout is fixed.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Right-stick look speed, in the same units as mouse pixels per second.</summary>
        public const float GamepadLookSpeed = 1400f;

        /// <summary>While true (typing in the debug console, rebinding a key) gameplay sees no input.</summary>
        public static bool Suspended;

        /// <summary>A bound action held this frame.</summary>
        public static bool Held(GameAction action) => !Suspended && KeyHeld(Bindings.Get(action));
        /// <summary>A bound action pressed this frame.</summary>
        public static bool Pressed(GameAction action) => !Suspended && KeyDown(Bindings.Get(action));

        public static Vector2 Move
        {
            get
            {
                if (Suspended) return Vector2.zero;
                Vector2 v = Vector2.zero;
                if (Held(GameAction.MoveForward)) v.y += 1f;
                if (Held(GameAction.MoveBack)) v.y -= 1f;
                if (Held(GameAction.MoveRight)) v.x += 1f;
                if (Held(GameAction.MoveLeft)) v.x -= 1f;
                v += PadStick(left: true);
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Look delta this frame, in mouse pixels, with the player's sensitivity and invert setting.</summary>
        public static Vector2 Look
        {
            get
            {
                if (Suspended) return Vector2.zero;
                Vector2 v = MouseDelta + PadStick(left: false) * GamepadLookSpeed * Time.deltaTime;
                v *= GameSettings.Sensitivity;
                if (GameSettings.InvertY) v.y = -v.y;
                return v;
            }
        }

        public static bool JumpPressed => Pressed(GameAction.Jump) || Pad(PadButton.South, down: true);
        public static bool SprintHeld => Held(GameAction.Sprint) || Pad(PadButton.LeftStick, down: false);
        public static bool FireHeld => Held(GameAction.Fire) || Pad(PadButton.RightTrigger, down: false);
        public static bool FirePressed => Pressed(GameAction.Fire) || Pad(PadButton.RightTrigger, down: true);
        public static bool AimHeld => Held(GameAction.Aim) || Pad(PadButton.LeftTrigger, down: false);
        public static bool ReloadPressed => Pressed(GameAction.Reload) || Pad(PadButton.West, down: true);
        public static bool InteractHeld => Held(GameAction.Interact) || Pad(PadButton.North, down: false);
        public static bool InteractPressed => Pressed(GameAction.Interact) || Pad(PadButton.North, down: true);
        public static bool MeleePressed => Pressed(GameAction.Melee) || Pad(PadButton.RightStick, down: true);
        public static bool GrenadePressed => Pressed(GameAction.Grenade) || Pad(PadButton.RightShoulder, down: true);
        public static bool KnifePressed => Pressed(GameAction.ThrowingKnife) || Pad(PadButton.LeftShoulder, down: true);
        public static bool ConcussionPressed => Pressed(GameAction.Concussion) || Pad(PadButton.Select, down: true);

        /// <summary>Esc (or Start) is fixed so the menu can always be reached.</summary>
        public static bool PausePressed => KeyDown(KeyCode.Escape) || Pad(PadButton.Start, down: true);

        /// <summary>Swap to the other weapon: the bound key, the mouse wheel or the gamepad's east button.</summary>
        public static bool SwitchWeaponPressed => Pressed(GameAction.SwitchWeapon) || (!Suspended && Mathf.Abs(Scroll) > 0.01f) || Pad(PadButton.East, down: true);

        /// <summary>The weapon slot picked this frame (0 or 1), or -1.</summary>
        public static int SlotPressed => Pressed(GameAction.Weapon1) ? 0 : Pressed(GameAction.Weapon2) ? 1 : -1;

        /// <summary>A number key 1-9 pressed this frame as 0-8 (shop items), else -1. Menus always use the number keys.</summary>
        public static int NumberPressed
        {
            get
            {
                if (Suspended) return -1;
                for (int i = 0; i < 9; i++)
                    if (KeyDown(KeyCode.Alpha1 + i) || KeyDown(KeyCode.Keypad1 + i)) return i;
                return -1;
            }
        }

        /// <summary>Redraw the power-up cards with a reroll: 4 or d-pad down.</summary>
        public static bool RerollPressed => (!Suspended && (KeyDown(KeyCode.Alpha4) || KeyDown(KeyCode.Keypad4))) || Pad(PadButton.DpadDown, down: true);

        /// <summary>A power-up card picked with 1/2/3 (or d-pad left/up/right), else -1.</summary>
        public static int ChoicePressed
        {
            get
            {
                int n = NumberPressed;
                if (n >= 0 && n < 3) return n;
                if (Pad(PadButton.DpadLeft, down: true)) return 0;
                if (Pad(PadButton.DpadUp, down: true)) return 1;
                if (Pad(PadButton.DpadRight, down: true)) return 2;
                return -1;
            }
        }

        /// <summary>Test-mode keys on the host. God mode is F1 because G throws grenades.</summary>
        /// <summary>A fixed test key, unless the player has bound a game action to it.</summary>
        static bool TestKey(KeyCode key) => !Bindings.IsBound(key) && KeyDown(key);

        public static bool DebugPressed(DebugKey key)
        {
            if (key == DebugKey.Console) return KeyDown(KeyCode.BackQuote) || KeyDown(KeyCode.F2);
            if (Suspended) return false;
            switch (key)
            {
                case DebugKey.NextRound: return TestKey(KeyCode.Equals);
                case DebugKey.JumpTenRounds: return TestKey(KeyCode.RightBracket);
                case DebugKey.GodMode: return TestKey(KeyCode.F1);
                case DebugKey.GivePoints: return TestKey(KeyCode.P);
                case DebugKey.Restart: return KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter) || Pad(PadButton.Start, down: true);
                case DebugKey.ToggleHelp: return TestKey(KeyCode.H);
                case DebugKey.OfferPowerUp: return TestKey(KeyCode.O);
                case DebugKey.SpawnBoss: return TestKey(KeyCode.B);
                default: return false;
            }
        }

        /// <summary>For rebinding: the first bindable key or mouse button pressed this frame.</summary>
        public static bool TryGetPressedKey(out KeyCode key)
        {
            foreach (var k in Bindings.Capturable)
            {
                if (KeyDown(k))
                {
                    key = k;
                    return true;
                }
            }
            key = KeyCode.None;
            return false;
        }

        enum PadButton
        {
            South, East, West, North,
            LeftStick, RightStick, LeftShoulder, RightShoulder,
            LeftTrigger, RightTrigger, Start, Select,
            DpadUp, DpadDown, DpadLeft, DpadRight,
        }

#if ENABLE_INPUT_SYSTEM
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        static Vector2 MouseDelta => M != null ? M.delta.ReadValue() : Vector2.zero;
        static float Scroll => M != null ? M.scroll.ReadValue().y : 0f;

        static Vector2 PadStick(bool left) => G == null ? Vector2.zero : (left ? G.leftStick : G.rightStick).ReadValue();

        static bool Pad(PadButton button, bool down)
        {
            if (Suspended || G == null) return false;
            ButtonControl c;
            switch (button)
            {
                case PadButton.South: c = G.buttonSouth; break;
                case PadButton.East: c = G.buttonEast; break;
                case PadButton.West: c = G.buttonWest; break;
                case PadButton.North: c = G.buttonNorth; break;
                case PadButton.LeftStick: c = G.leftStickButton; break;
                case PadButton.RightStick: c = G.rightStickButton; break;
                case PadButton.LeftShoulder: c = G.leftShoulder; break;
                case PadButton.RightShoulder: c = G.rightShoulder; break;
                case PadButton.LeftTrigger: c = G.leftTrigger; break;
                case PadButton.RightTrigger: c = G.rightTrigger; break;
                case PadButton.Start: c = G.startButton; break;
                case PadButton.Select: c = G.selectButton; break;
                case PadButton.DpadUp: c = G.dpad.up; break;
                case PadButton.DpadDown: c = G.dpad.down; break;
                case PadButton.DpadLeft: c = G.dpad.left; break;
                default: c = G.dpad.right; break;
            }
            return c != null && (down ? c.wasPressedThisFrame : c.isPressed);
        }

        static ButtonControl Control(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Mouse0: return M?.leftButton;
                case KeyCode.Mouse1: return M?.rightButton;
                case KeyCode.Mouse2: return M?.middleButton;
                case KeyCode.Mouse3: return M?.backButton;
                case KeyCode.Mouse4: return M?.forwardButton;
            }
            if (K == null) return null;
            Key key = ToKey(code);
            return key == Key.None ? null : K[key];
        }

        public static bool KeyHeld(KeyCode code)
        {
            var c = Control(code);
            return c != null && c.isPressed;
        }

        public static bool KeyDown(KeyCode code)
        {
            var c = Control(code);
            return c != null && c.wasPressedThisFrame;
        }

        /// <summary>The Input System key for a KeyCode, or Key.None.</summary>
        public static Key ToKey(KeyCode code)
        {
            if (code >= KeyCode.A && code <= KeyCode.Z) return Key.A + (code - KeyCode.A);
            if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9) return Key.Digit1 + (code - KeyCode.Alpha1);
            if (code >= KeyCode.Keypad0 && code <= KeyCode.Keypad9) return Key.Numpad0 + (code - KeyCode.Keypad0);
            if (code >= KeyCode.F1 && code <= KeyCode.F12) return Key.F1 + (code - KeyCode.F1);
            switch (code)
            {
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.CapsLock: return Key.CapsLock;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                default: return Key.None;
            }
        }
#else
        // The default Input Manager scales mouse axes by 0.1, so x10 gets back to roughly pixels.
        static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        static float Scroll => Input.mouseScrollDelta.y;

        // The legacy backend has no gamepad layout without Input Manager axes set up for it.
        static Vector2 PadStick(bool left) => Vector2.zero;
        static bool Pad(PadButton button, bool down) => false;

        public static bool KeyHeld(KeyCode code) => code != KeyCode.None && Input.GetKey(code);
        public static bool KeyDown(KeyCode code) => code != KeyCode.None && Input.GetKeyDown(code);
#endif
    }
}
