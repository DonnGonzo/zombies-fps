using System;
using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>Everything the player can rebind in Settings. The order is the order the settings list shows.</summary>
    public enum GameAction
    {
        MoveForward,
        MoveBack,
        MoveLeft,
        MoveRight,
        Jump,
        Sprint,
        Fire,
        Aim,
        Reload,
        Interact,
        SwitchWeapon,
        Weapon1,
        Weapon2,
        Melee,
        Grenade,
        ThrowingKnife,
        Concussion,
        Map,
    }

    /// <summary>
    /// Key bindings, saved on the player's PC. Keys are stored as KeyCode so both input backends can read them;
    /// GameInput turns them into Input System controls when that backend is on. Escape is reserved for the menu.
    /// </summary>
    public static class Bindings
    {
        public static readonly int Count = Enum.GetValues(typeof(GameAction)).Length;

        static readonly KeyCode[] Defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
            KeyCode.Space, KeyCode.LeftShift,
            KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.R, KeyCode.E,
            KeyCode.Q, KeyCode.Alpha1, KeyCode.Alpha2,
            KeyCode.V, KeyCode.G, KeyCode.T, KeyCode.C,
            KeyCode.M,
        };

        static readonly string[] Labels =
        {
            "Move forward", "Move back", "Move left", "Move right",
            "Jump", "Sprint",
            "Fire", "Aim down sights", "Reload", "Use / buy / revive",
            "Switch weapon", "Weapon 1", "Weapon 2",
            "Melee", "Throw grenade", "Throw knife", "Throw concussion bang",
            "Show the map",
        };

        /// <summary>Keys the settings menu accepts when rebinding (Escape cancels instead).</summary>
        public static readonly KeyCode[] Capturable = BuildCapturable();

        static KeyCode[] s_Keys;

        static KeyCode[] Keys
        {
            get
            {
                if (s_Keys == null) Load();
                return s_Keys;
            }
        }

        static string PrefKey(GameAction action) => "ZombiesFPS.Bind." + action;

        public static KeyCode Default(GameAction action) => Defaults[(int)action];
        public static KeyCode Get(GameAction action) => Keys[(int)action];
        public static string Label(GameAction action) => Labels[(int)action];

        /// <summary>Binds a key. If another action had it, that action takes this one's old key, so nothing is left unbound.</summary>
        public static void Set(GameAction action, KeyCode key)
        {
            var keys = Keys;
            int i = (int)action;
            KeyCode old = keys[i];
            for (int other = 0; other < keys.Length; other++)
                if (other != i && keys[other] == key) keys[other] = old;
            keys[i] = key;
            Save();
        }

        public static void ResetAll()
        {
            s_Keys = (KeyCode[])Defaults.Clone();
            Save();
        }

        static void Load()
        {
            s_Keys = (KeyCode[])Defaults.Clone();
            for (int i = 0; i < s_Keys.Length; i++)
            {
                int saved = PlayerPrefs.GetInt(PrefKey((GameAction)i), (int)Defaults[i]);
                if (Enum.IsDefined(typeof(KeyCode), saved) && IsCapturable((KeyCode)saved)) s_Keys[i] = (KeyCode)saved;
            }
        }

        static void Save()
        {
            for (int i = 0; i < s_Keys.Length; i++) PlayerPrefs.SetInt(PrefKey((GameAction)i), (int)s_Keys[i]);
            PlayerPrefs.Save();
        }

        public static bool IsBound(KeyCode key) => Array.IndexOf(Keys, key) >= 0;

        public static bool IsCapturable(KeyCode key) => Array.IndexOf(Capturable, key) >= 0;

        static KeyCode[] BuildCapturable()
        {
            var list = new System.Collections.Generic.List<KeyCode>();
            for (var k = KeyCode.A; k <= KeyCode.Z; k++) list.Add(k);
            for (var k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++) list.Add(k);
            for (var k = KeyCode.Keypad0; k <= KeyCode.Keypad9; k++) list.Add(k);
            // F1 (god mode) and F2 (console) stay reserved for testing.
            for (var k = KeyCode.F3; k <= KeyCode.F12; k++) list.Add(k);
            list.AddRange(new[]
            {
                KeyCode.Space, KeyCode.Tab, KeyCode.Backspace, KeyCode.CapsLock,
                KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
                KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.Insert, KeyCode.Delete, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
                KeyCode.Minus, KeyCode.LeftBracket,
                KeyCode.Semicolon, KeyCode.Quote, KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.Backslash,
                KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4,
            });
            return list.ToArray();
        }

        /// <summary>A short readable name for a key, for prompts like "Press E".</summary>
        public static string KeyName(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "Num " + (int)(key - KeyCode.Keypad0);
            switch (key)
            {
                case KeyCode.Mouse0: return "Left mouse";
                case KeyCode.Mouse1: return "Right mouse";
                case KeyCode.Mouse2: return "Middle mouse";
                case KeyCode.Mouse3: return "Mouse 4";
                case KeyCode.Mouse4: return "Mouse 5";
                case KeyCode.LeftShift: return "Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftControl: return "Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftAlt: return "Alt";
                case KeyCode.RightAlt: return "Right Alt";
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
                case KeyCode.CapsLock: return "Caps Lock";
                default: return key.ToString();
            }
        }

        /// <summary>
        /// The key or button for an action, for on-screen prompts: the controller button while the player is on a
        /// pad (movement is the left stick), else the key.
        /// </summary>
        public static string Name(GameAction action)
        {
            if (GameInput.UsingGamepad)
            {
                if (PadBindings.IsFixed(action)) return "Left stick";
                var b = PadBindings.Get(action);
                if (b != PadButton.None) return GameInput.PadName(b);
            }
            return KeyName(Get(action));
        }
    }

    /// <summary>
    /// Controller bindings, saved on the player's PC next to the key bindings. Movement is always the left stick and
    /// look the right stick; Start is reserved for the menu. Any other action can go on any other button, or none.
    /// </summary>
    public static class PadBindings
    {
        static readonly PadButton[] Defaults =
        {
            PadButton.None, PadButton.None, PadButton.None, PadButton.None,
            PadButton.South, PadButton.LeftStick,
            PadButton.RightTrigger, PadButton.LeftTrigger, PadButton.West, PadButton.North,
            PadButton.East, PadButton.None, PadButton.None,
            PadButton.RightStick, PadButton.RightShoulder, PadButton.LeftShoulder, PadButton.DpadRight,
            PadButton.Select,
        };

        /// <summary>Buttons the settings menu accepts when rebinding (Start cancels instead).</summary>
        public static readonly PadButton[] Capturable =
        {
            PadButton.South, PadButton.East, PadButton.West, PadButton.North,
            PadButton.LeftShoulder, PadButton.RightShoulder, PadButton.LeftTrigger, PadButton.RightTrigger,
            PadButton.LeftStick, PadButton.RightStick, PadButton.Select,
            PadButton.DpadUp, PadButton.DpadDown, PadButton.DpadLeft, PadButton.DpadRight,
        };

        static PadButton[] s_Buttons;

        static PadButton[] Buttons
        {
            get
            {
                if (s_Buttons == null) Load();
                return s_Buttons;
            }
        }

        static string PrefKey(GameAction action) => "ZombiesFPS.PadBind." + action;

        /// <summary>Movement is on the left stick and can't be moved to buttons.</summary>
        public static bool IsFixed(GameAction action) => action <= GameAction.MoveRight;

        public static PadButton Default(GameAction action) => Defaults[(int)action];
        public static PadButton Get(GameAction action) => Buttons[(int)action];

        /// <summary>Binds a button. If another action had it, that action takes this one's old button (or none).</summary>
        public static void Set(GameAction action, PadButton button)
        {
            if (IsFixed(action) || (button != PadButton.None && !IsCapturable(button))) return;
            Assign(Buttons, action, button);
            Save();
        }

        /// <summary>The swap rule on its own: the action takes the button, and whoever had it takes the action's old one.</summary>
        public static void Assign(PadButton[] buttons, GameAction action, PadButton button)
        {
            int i = (int)action;
            PadButton old = buttons[i];
            if (button != PadButton.None)
                for (int other = 0; other < buttons.Length; other++)
                    if (other != i && buttons[other] == button) buttons[other] = old;
            buttons[i] = button;
        }

        /// <summary>A copy of the shipped layout.</summary>
        public static PadButton[] DefaultLayout() => (PadButton[])Defaults.Clone();

        public static void ResetAll()
        {
            s_Buttons = (PadButton[])Defaults.Clone();
            Save();
        }

        public static bool IsCapturable(PadButton button) => Array.IndexOf(Capturable, button) >= 0;

        static void Load()
        {
            s_Buttons = (PadButton[])Defaults.Clone();
            for (int i = 0; i < s_Buttons.Length; i++)
            {
                if (IsFixed((GameAction)i)) continue;
                int saved = PlayerPrefs.GetInt(PrefKey((GameAction)i), (int)Defaults[i]);
                var b = (PadButton)saved;
                if (b == PadButton.None || IsCapturable(b)) s_Buttons[i] = b;
            }
        }

        static void Save()
        {
            for (int i = 0; i < s_Buttons.Length; i++) PlayerPrefs.SetInt(PrefKey((GameAction)i), (int)s_Buttons[i]);
            PlayerPrefs.Save();
        }
    }
}
