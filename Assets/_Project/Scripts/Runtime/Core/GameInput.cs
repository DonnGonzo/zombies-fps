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

    /// <summary>Gamepad buttons by position (south = A on Xbox, Cross on PlayStation). Saved by number, so only add to the end.</summary>
    public enum PadButton
    {
        None,
        South, East, West, North,
        LeftStick, RightStick, LeftShoulder, RightShoulder,
        LeftTrigger, RightTrigger, Start, Select,
        DpadUp, DpadDown, DpadLeft, DpadRight,
    }

    /// <summary>Which face-button names the prompts use.</summary>
    public enum PadGlyphs { Auto, Xbox, PlayStation, Nintendo }

    /// <summary>
    /// Input facade so gameplay code does not care which input backend is active. Works with the Input System
    /// package or the legacy Input Manager, whichever the project has enabled. Every action follows the player's
    /// keyboard and mouse bindings (Bindings) and controller bindings (PadBindings), both changed in Settings.
    /// It also tracks which of the two was used last, so prompts can show the right buttons.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Right-stick look speed at full tilt, in the same units as mouse pixels per second.</summary>
        public const float GamepadLookSpeed = 1400f;
        /// <summary>The right stick turns a little slower up and down than sideways, as in most shooters.</summary>
        const float VerticalLookScale = 0.8f;
        /// <summary>Holding the right stick fully over speeds sideways turning up to this much, so you can turn round quickly.</summary>
        const float MaxLookBoost = 0.75f;
        const float BoostDelay = 0.18f, BoostRamp = 0.35f;
        /// <summary>Left-stick dead zone; the right stick's is a setting.</summary>
        const float MoveDeadzone = 0.15f;
        const float NavFirstRepeat = 0.38f, NavRepeat = 0.11f;

        /// <summary>While true (typing in the debug console, rebinding a key) gameplay sees no input.</summary>
        public static bool Suspended;

        /// <summary>The player last touched a gamepad (not the mouse or keyboard). Prompts and menu focus follow this.</summary>
        public static bool UsingGamepad { get; private set; }

        static int s_MenuFrame = -10, s_DpadFrame = -10;

        /// <summary>A pad menu (the shop, the casino) is open this frame: A and the d-pad drive it, not their game actions.</summary>
        public static void ClaimPadMenu() => s_MenuFrame = Time.frameCount;
        /// <summary>The d-pad drives an open screen (the map, power-up cards) this frame, not its game actions.</summary>
        public static void ClaimDpad() => s_DpadFrame = Time.frameCount;
        static bool MenuClaimed => Time.frameCount - s_MenuFrame <= 1;
        static bool DpadClaimed => MenuClaimed || Time.frameCount - s_DpadFrame <= 1;

        static bool PadFree(PadButton b)
        {
            if (b == PadButton.None) return false;
            bool dpad = b >= PadButton.DpadUp && b <= PadButton.DpadRight;
            if (dpad && DpadClaimed) return false;
            if (b == PadButton.South && MenuClaimed) return false;
            return true;
        }

        /// <summary>A bound action held this frame.</summary>
        public static bool Held(GameAction action)
        {
            if (Suspended) return false;
            if (KeyHeld(Bindings.Get(action))) return true;
            var b = PadBindings.Get(action);
            return PadFree(b) && Pad(b, down: false);
        }

        /// <summary>A bound action pressed this frame.</summary>
        public static bool Pressed(GameAction action)
        {
            if (Suspended) return false;
            if (KeyDown(Bindings.Get(action))) return true;
            var b = PadBindings.Get(action);
            return PadFree(b) && Pad(b, down: true);
        }

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
                v += ShapeStick(RawStick(left: true), MoveDeadzone, 1f);
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Mouse look this frame, in mouse pixels, with the mouse sensitivity and invert setting.</summary>
        public static Vector2 MouseLook
        {
            get
            {
                if (Suspended) return Vector2.zero;
                Vector2 v = MouseDelta * GameSettings.Sensitivity;
                if (GameSettings.InvertY) v.y = -v.y;
                return v;
            }
        }

        static Vector2 s_PadLook;

        /// <summary>Right-stick look this frame, in mouse pixels, with the stick's own sensitivity, curve, boost and invert.</summary>
        public static Vector2 PadLook => Suspended ? Vector2.zero : s_PadLook;

        /// <summary>Mouse and stick look together.</summary>
        public static Vector2 Look => MouseLook + PadLook;

        /// <summary>How far the right stick is pushed (0-1 after its dead zone), for aim assist.</summary>
        public static float LookStickAmount { get; private set; }

        /// <summary>Response curves for the right stick: names for Settings and their exponents.</summary>
        public static readonly string[] CurveNames = { "Standard", "Linear", "Precise" };
        static readonly float[] CurveExponents = { 1.6f, 1f, 2.2f };

        /// <summary>
        /// A raw stick reading after a round dead zone and a response curve: nothing inside the dead zone,
        /// full tilt from 95% (worn sticks rarely reach 100%), the same direction, and length^exponent in between.
        /// </summary>
        public static Vector2 ShapeStick(Vector2 raw, float deadzone, float exponent)
        {
            float m = raw.magnitude;
            if (m <= deadzone || m <= 0.0001f) return Vector2.zero;
            float n = Mathf.Clamp01((m - deadzone) / Mathf.Max(0.01f, 0.95f - deadzone));
            n = Mathf.Pow(n, Mathf.Max(0.1f, exponent));
            return raw / m * n;
        }

        public static bool JumpPressed => Pressed(GameAction.Jump);
        public static bool SprintHeld => Held(GameAction.Sprint) || s_SprintLatched;
        public static bool FireHeld => Held(GameAction.Fire);
        public static bool FirePressed => Pressed(GameAction.Fire);
        public static bool AimHeld => Held(GameAction.Aim);
        public static bool ReloadPressed => Pressed(GameAction.Reload);
        public static bool InteractHeld => Held(GameAction.Interact);
        public static bool InteractPressed => Pressed(GameAction.Interact);
        public static bool MeleePressed => Pressed(GameAction.Melee);
        public static bool GrenadePressed => Pressed(GameAction.Grenade);
        public static bool KnifePressed => Pressed(GameAction.ThrowingKnife);
        public static bool MapPressed => Pressed(GameAction.Map);
        public static bool ConcussionPressed => Pressed(GameAction.Concussion);

        /// <summary>Esc (or Start) is fixed so the menu can always be reached.</summary>
        public static bool PausePressed => KeyDown(KeyCode.Escape) || (!Suspended && Pad(PadButton.Start, down: true));

        /// <summary>Swap to the other weapon: the bound key or button, or the mouse wheel.</summary>
        public static bool SwitchWeaponPressed => Pressed(GameAction.SwitchWeapon) || (!Suspended && Mathf.Abs(Scroll) > 0.01f);

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

        /// <summary>A digit key 0-9 (top row or numpad) pressed this frame, for the generator's keypad, else -1.</summary>
        public static int DigitPressed
        {
            get
            {
                if (Suspended) return -1;
                for (int i = 0; i < 10; i++)
                    if (KeyDown(KeyCode.Alpha0 + i) || KeyDown(KeyCode.Keypad0 + i)) return i;
                return -1;
            }
        }

        /// <summary>Rubs out the last keypad digit: Backspace or Delete.</summary>
        public static bool BackspacePressed => !Suspended && (KeyDown(KeyCode.Backspace) || KeyDown(KeyCode.Delete));

        /// <summary>A pad button pressed this frame for an open screen, whatever game action it is bound to.</summary>
        public static bool MenuPad(PadButton button) => !Suspended && Pad(button, down: true);

        /// <summary>The keypad on a gamepad: up and down turn the digit, right takes it, left steps back.</summary>
        public static bool KeypadUpPressed => MenuPad(PadButton.DpadUp);
        public static bool KeypadDownPressed => MenuPad(PadButton.DpadDown);
        public static bool KeypadNextPressed => MenuPad(PadButton.DpadRight);
        public static bool KeypadBackPressed => MenuPad(PadButton.DpadLeft);

        /// <summary>Switches the map screen between the map and the story: Tab (unless bound to something else), or the d-pad left or right.</summary>
        public static bool MapTabPressed => !Suspended && ((!Bindings.IsBound(KeyCode.Tab) && KeyDown(KeyCode.Tab)) || MenuPad(PadButton.DpadLeft) || MenuPad(PadButton.DpadRight));

        /// <summary>Redraw the power-up cards with a reroll: 4 or d-pad down.</summary>
        public static bool RerollPressed => (!Suspended && (KeyDown(KeyCode.Alpha4) || KeyDown(KeyCode.Keypad4))) || MenuPad(PadButton.DpadDown);

        /// <summary>A power-up card picked with 1/2/3 (or d-pad left/up/right), else -1.</summary>
        public static int ChoicePressed
        {
            get
            {
                int n = NumberPressed;
                if (n >= 0 && n < 3) return n;
                if (MenuPad(PadButton.DpadLeft)) return 0;
                if (MenuPad(PadButton.DpadUp)) return 1;
                if (MenuPad(PadButton.DpadRight)) return 2;
                return -1;
            }
        }

        // ---------------------------------------------------------------- menus

        static Vector2Int s_Nav, s_Dpad;

        /// <summary>Menu focus moves this frame: d-pad or left stick, repeating while held. Up is +y.</summary>
        public static Vector2Int NavStep => Suspended ? Vector2Int.zero : s_Nav;
        /// <summary>The d-pad alone, repeating while held, for in-game panels (the stick still walks).</summary>
        public static Vector2Int DpadStep => Suspended ? Vector2Int.zero : s_Dpad;
        /// <summary>A (Cross) in menus.</summary>
        public static bool SubmitPressed => MenuPad(PadButton.South);
        /// <summary>B (Circle) in menus.</summary>
        public static bool CancelPressed => MenuPad(PadButton.East);
        /// <summary>The bumpers in menus: -1 left, +1 right, else 0. Settings uses them for its tabs.</summary>
        public static int TabStep => MenuPad(PadButton.LeftShoulder) ? -1 : MenuPad(PadButton.RightShoulder) ? 1 : 0;

        // ---------------------------------------------------------------- debug

        /// <summary>A fixed test key, unless the player has bound a game action to it. God mode is F1 because G throws grenades.</summary>
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

        // ---------------------------------------------------------------- rebinding

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

        /// <summary>For rebinding: the first bindable controller button pressed this frame (ignores Suspended).</summary>
        public static bool TryGetPressedPad(out PadButton button)
        {
            foreach (var b in PadBindings.Capturable)
            {
                if (Pad(b, down: true))
                {
                    button = b;
                    return true;
                }
            }
            button = PadButton.None;
            return false;
        }

        /// <summary>Cancels rebinding: Esc or Start, read even while input is suspended.</summary>
        public static bool CaptureCancelPressed => KeyDown(KeyCode.Escape) || Pad(PadButton.Start, down: true);

        // ---------------------------------------------------------------- prompts

        /// <summary>The face-button names in use: the setting, or (on Auto) whatever kind of pad is plugged in.</summary>
        public static PadGlyphs Glyphs => GameSettings.PadGlyphs != PadGlyphs.Auto ? GameSettings.PadGlyphs : DetectedGlyphs;

        static readonly string[][] FaceNames =
        {
            new[] { "A", "B", "X", "Y" },
            new[] { "A", "B", "X", "Y" },
            new[] { "Cross", "Circle", "Square", "Triangle" },
            // Nintendo pads swap the letters: the south button is B.
            new[] { "B", "A", "Y", "X" },
        };
        static readonly string[][] ShoulderNames =
        {
            new[] { "LS", "RS", "LB", "RB", "LT", "RT", "Menu", "View" },
            new[] { "LS", "RS", "LB", "RB", "LT", "RT", "Menu", "View" },
            new[] { "L3", "R3", "L1", "R1", "L2", "R2", "Options", "Share" },
            new[] { "LS", "RS", "L", "R", "ZL", "ZR", "+", "−" },
        };

        /// <summary>A button's name as printed on the player's pad, for prompts like "Press Y".</summary>
        public static string PadName(PadButton b) => PadName(b, Glyphs);

        public static string PadName(PadButton b, PadGlyphs style)
        {
            int s = (int)style;
            switch (b)
            {
                case PadButton.None: return "—";
                case PadButton.South: return FaceNames[s][0];
                case PadButton.East: return FaceNames[s][1];
                case PadButton.West: return FaceNames[s][2];
                case PadButton.North: return FaceNames[s][3];
                case PadButton.DpadUp: return "D-pad ↑";
                case PadButton.DpadDown: return "D-pad ↓";
                case PadButton.DpadLeft: return "D-pad ←";
                case PadButton.DpadRight: return "D-pad →";
                default: return ShoulderNames[s][(int)b - (int)PadButton.LeftStick];
            }
        }

        /// <summary>The colour of a face button on the player's pad (Xbox and PlayStation), for tinting key caps.</summary>
        public static bool FaceColor(string label, out Color color)
        {
            color = Color.clear;
            if (!UsingGamepad) return false;
            var g = Glyphs;
            if (g == PadGlyphs.PlayStation)
            {
                switch (label)
                {
                    case "Cross": color = new Color(0.36f, 0.55f, 0.95f); return true;
                    case "Circle": color = new Color(0.9f, 0.3f, 0.3f); return true;
                    case "Square": color = new Color(0.85f, 0.45f, 0.75f); return true;
                    case "Triangle": color = new Color(0.25f, 0.75f, 0.6f); return true;
                }
                return false;
            }
            if (g == PadGlyphs.Nintendo) return false;
            switch (label)
            {
                case "A": color = new Color(0.36f, 0.72f, 0.25f); return true;
                case "B": color = new Color(0.85f, 0.26f, 0.22f); return true;
                case "X": color = new Color(0.24f, 0.5f, 0.92f); return true;
                case "Y": color = new Color(0.9f, 0.68f, 0.12f); return true;
            }
            return false;
        }

        /// <summary>A fixed key's name, or its controller button's name while the player uses a pad.</summary>
        public static string FixedName(string key, PadButton pad) => UsingGamepad ? PadName(pad) : key;

        // ---------------------------------------------------------------- rumble

        static float s_RumbleLow, s_RumbleHigh, s_RumbleUntil;

        /// <summary>Shakes the pad (if the player is on one and has vibration on): low is the heavy motor, high the light one.</summary>
        public static void Rumble(float low, float high, float seconds)
        {
            if (!UsingGamepad || !GameSettings.Vibration) return;
            float now = Time.unscaledTime;
            if (now >= s_RumbleUntil)
            {
                s_RumbleLow = s_RumbleHigh = 0f;
            }
            s_RumbleLow = Mathf.Clamp01(Mathf.Max(s_RumbleLow, low));
            s_RumbleHigh = Mathf.Clamp01(Mathf.Max(s_RumbleHigh, high));
            s_RumbleUntil = Mathf.Max(s_RumbleUntil, now + seconds);
        }

        // ---------------------------------------------------------------- per frame

        static int s_TickFrame = -1;
        static float s_BoostTime, s_NavNext, s_DpadNext;
        static Vector2Int s_NavHeld, s_DpadHeld;
        static bool s_SprintLatched;

        /// <summary>
        /// Once a frame, before gameplay (GamepadDriver): which device is in use, the stick look, menu repeat,
        /// controller sprint and rumble.
        /// </summary>
        public static void Tick()
        {
            if (s_TickFrame == Time.frameCount) return;
            s_TickFrame = Time.frameCount;
            float now = Time.unscaledTime, dt = Time.deltaTime;
            TrackDevice();

            // Stick look: dead zone and curve, then a boost for fast turns while the stick is held right over.
            Vector2 stick = ShapeStick(RawStick(left: false), GameSettings.StickDeadzone, CurveExponents[Mathf.Clamp(GameSettings.StickCurve, 0, CurveExponents.Length - 1)]);
            LookStickAmount = stick.magnitude;
            s_BoostTime = LookStickAmount > 0.97f && Mathf.Abs(stick.x) > 0.7f ? s_BoostTime + dt : 0f;
            float boost = GameSettings.LookAcceleration ? 1f + MaxLookBoost * Mathf.Clamp01((s_BoostTime - BoostDelay) / BoostRamp) : 1f;
            Vector2 look = new Vector2(stick.x * boost, stick.y * VerticalLookScale) * GamepadLookSpeed * GameSettings.StickSensitivity * dt;
            if (GameSettings.PadInvertY) look.y = -look.y;
            s_PadLook = look;

            // Menu steps: the first press moves at once, holding repeats.
            Vector2 left = RawStick(left: true);
            var dpad = DpadDirection();
            var nav = dpad != Vector2Int.zero ? dpad : StickDirection(left);
            s_Nav = Repeat(nav, ref s_NavHeld, ref s_NavNext, now);
            s_Dpad = Repeat(dpad, ref s_DpadHeld, ref s_DpadNext, now);

            // Clicking the sprint button on a pad keeps you sprinting until you slow down, as holding a stick in is awkward.
            if (!Suspended && PadFree(PadBindings.Get(GameAction.Sprint)) && Pad(PadBindings.Get(GameAction.Sprint), down: true)) s_SprintLatched = true;
            if (left.y < 0.35f || Held(GameAction.Aim) || Suspended) s_SprintLatched = false;

            ApplyRumble(now);
        }

        static Vector2Int Repeat(Vector2Int dir, ref Vector2Int held, ref float next, float now)
        {
            if (dir == Vector2Int.zero)
            {
                held = Vector2Int.zero;
                return Vector2Int.zero;
            }
            if (dir != held)
            {
                held = dir;
                next = now + NavFirstRepeat;
                return dir;
            }
            if (now < next) return Vector2Int.zero;
            next = now + NavRepeat;
            return dir;
        }

        static Vector2Int StickDirection(Vector2 v)
        {
            if (v.magnitude < 0.55f) return Vector2Int.zero;
            return Mathf.Abs(v.x) > Mathf.Abs(v.y) ? new Vector2Int(v.x > 0 ? 1 : -1, 0) : new Vector2Int(0, v.y > 0 ? 1 : -1);
        }

        static Vector2Int DpadDirection()
        {
            if (Pad(PadButton.DpadUp, down: false)) return new Vector2Int(0, 1);
            if (Pad(PadButton.DpadDown, down: false)) return new Vector2Int(0, -1);
            if (Pad(PadButton.DpadLeft, down: false)) return new Vector2Int(-1, 0);
            if (Pad(PadButton.DpadRight, down: false)) return new Vector2Int(1, 0);
            return Vector2Int.zero;
        }

#if ENABLE_INPUT_SYSTEM
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;
        static Gamepad G => Gamepad.current;

        static Vector2 MouseDelta => M != null ? M.delta.ReadValue() : Vector2.zero;
        static float Scroll => M != null ? M.scroll.ReadValue().y : 0f;

        /// <summary>Unprocessed, so our own dead zone and curve are the only ones applied.</summary>
        static Vector2 RawStick(bool left) => G == null ? Vector2.zero : (left ? G.leftStick : G.rightStick).ReadUnprocessedValue();

        static PadGlyphs DetectedGlyphs
        {
            get
            {
                var g = G;
                if (g == null) return PadGlyphs.Xbox;
                string name = (g.layout + " " + g.description.manufacturer + " " + g.description.product).ToLowerInvariant();
                if (name.Contains("dualshock") || name.Contains("dualsense") || name.Contains("sony") || name.Contains("playstation")) return PadGlyphs.PlayStation;
                if (name.Contains("switch") || name.Contains("nintendo")) return PadGlyphs.Nintendo;
                return PadGlyphs.Xbox;
            }
        }

        static void TrackDevice()
        {
            var g = G;
            if (g == null)
            {
                UsingGamepad = false;
                return;
            }
            bool pad = g.wasUpdatedThisFrame && (AnyPadButton(g) || g.leftStick.ReadValue().sqrMagnitude > 0.16f || g.rightStick.ReadValue().sqrMagnitude > 0.16f);
            if (pad)
            {
                UsingGamepad = true;
                return;
            }
            bool keys = K != null && K.anyKey.wasPressedThisFrame;
            bool mouse = M != null && (M.delta.ReadValue().sqrMagnitude > 9f || M.leftButton.wasPressedThisFrame || M.rightButton.wasPressedThisFrame);
            if (keys || mouse) UsingGamepad = false;
        }

        static bool AnyPadButton(Gamepad g)
        {
            for (var b = PadButton.South; b <= PadButton.DpadRight; b++)
            {
                var c = Control(g, b);
                if (c != null && c.wasPressedThisFrame) return true;
            }
            return false;
        }

        static bool s_Rumbling;

        static void ApplyRumble(float now)
        {
            var g = G;
            bool on = now < s_RumbleUntil && GameSettings.Vibration && UsingGamepad;
            if (g == null) return;
            if (on)
            {
                // Fade the last stretch so pulses don't end with a clunk.
                float fade = Mathf.Clamp01((s_RumbleUntil - now) / 0.06f);
                g.SetMotorSpeeds(s_RumbleLow * fade, s_RumbleHigh * fade);
                s_Rumbling = true;
            }
            else if (s_Rumbling)
            {
                g.SetMotorSpeeds(0f, 0f);
                s_Rumbling = false;
            }
        }

        /// <summary>Stops the motors at once (pause, losing focus, quitting).</summary>
        public static void StopRumble()
        {
            s_RumbleUntil = 0f;
            if (G != null && s_Rumbling) G.SetMotorSpeeds(0f, 0f);
            s_Rumbling = false;
        }

        static ButtonControl Control(Gamepad g, PadButton button)
        {
            switch (button)
            {
                case PadButton.South: return g.buttonSouth;
                case PadButton.East: return g.buttonEast;
                case PadButton.West: return g.buttonWest;
                case PadButton.North: return g.buttonNorth;
                case PadButton.LeftStick: return g.leftStickButton;
                case PadButton.RightStick: return g.rightStickButton;
                case PadButton.LeftShoulder: return g.leftShoulder;
                case PadButton.RightShoulder: return g.rightShoulder;
                case PadButton.LeftTrigger: return g.leftTrigger;
                case PadButton.RightTrigger: return g.rightTrigger;
                case PadButton.Start: return g.startButton;
                case PadButton.Select: return g.selectButton;
                case PadButton.DpadUp: return g.dpad.up;
                case PadButton.DpadDown: return g.dpad.down;
                case PadButton.DpadLeft: return g.dpad.left;
                case PadButton.DpadRight: return g.dpad.right;
                default: return null;
            }
        }

        static bool Pad(PadButton button, bool down)
        {
            if (G == null || button == PadButton.None) return false;
            var c = Control(G, button);
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
        static Vector2 RawStick(bool left) => Vector2.zero;
        static bool Pad(PadButton button, bool down) => false;
        static PadGlyphs DetectedGlyphs => PadGlyphs.Xbox;
        static void TrackDevice() => UsingGamepad = false;
        static void ApplyRumble(float now) { }
        public static void StopRumble() { }

        public static bool KeyHeld(KeyCode code) => code != KeyCode.None && Input.GetKey(code);
        public static bool KeyDown(KeyCode code) => code != KeyCode.None && Input.GetKeyDown(code);
#endif
    }
}
