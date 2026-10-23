using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The settings screen, shared by the main menu and the in-game pause menu: key bindings, mouse and view,
    /// sound, and accessibility. Everything saves as soon as it changes. On a controller the bumpers change tab,
    /// the d-pad moves between settings, A changes one and B goes back.
    /// </summary>
    public static class SettingsMenu
    {
        enum Tab { Controls, Controller, MouseAndView, Graphics, Sound, Accessibility }

        static readonly string[] TabNames = { "Controls", "Controller", "Mouse & view", "Graphics", "Sound", "Accessibility" };

        static Tab s_Tab;
        static Vector2 s_Scroll;
        static int s_Capturing = -1;
        static int s_CaptureFrame;
        static bool s_CapturePad;

        public static bool IsCapturing => s_Capturing >= 0;

        /// <summary>Draws the screen in a rect. Returns false once the player presses Back.</summary>
        public static bool Draw(Rect area, float u)
        {
            Ui.EnsureStyles(u);
            Ui.PanelBox(area, Ui.Blood, u);
            float pad = 28 * u;
            var inner = new Rect(area.x + pad, area.y + pad, area.width - pad * 2, area.height - pad * 2);
            Ui.Label(new Rect(inner.x, inner.y, inner.width, 50 * u), "SETTINGS", Ui.Header, Ui.Text, TextAnchor.UpperLeft);

            bool capturing = IsCapturing;
            GUI.enabled = !capturing;
            float tw = (inner.width - (TabNames.Length - 1) * 8 * u) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                var r = new Rect(inner.x + i * (tw + 8 * u), inner.y + 58 * u, tw, 44 * u);
                // Tabs aren't in the d-pad's way; the bumpers flip through them.
                if (Ui.Choice(r, s_Tab == (Tab)i, TabNames[i], focusable: false)) SetTab((Tab)i);
            }
            if (!capturing && Event.current.type == EventType.Repaint)
            {
                int step = GameInput.TabStep;
                if (step != 0) SetTab((Tab)(((int)s_Tab + step + TabNames.Length) % TabNames.Length));
                if (GameInput.UsingGamepad)
                {
                    string lb = GameInput.PadName(PadButton.LeftShoulder), rb = GameInput.PadName(PadButton.RightShoulder);
                    Ui.Label(new Rect(inner.x, inner.y + 104 * u, inner.width, 20 * u), $"{lb} / {rb} change tab", Ui.Tiny, Ui.Muted, TextAnchor.UpperRight);
                }
            }

            var body = new Rect(inner.x, inner.y + 120 * u, inner.width, inner.height - 120 * u - 64 * u);
            switch (s_Tab)
            {
                case Tab.Controls: DrawControls(body, u); break;
                case Tab.Controller: DrawController(body, u); break;
                case Tab.MouseAndView: DrawMouseAndView(body, u); break;
                case Tab.Graphics: DrawGraphics(body, u); break;
                case Tab.Sound: DrawSound(body, u); break;
                default: DrawAccessibility(body, u); break;
            }

            bool open = true;
            float bw = 220 * u;
            var bottom = new Rect(inner.x, inner.yMax - 50 * u, bw, 50 * u);
            if (Ui.Button(bottom, "Back", accent: true, cancel: true))
            {
                GameSettings.Save();
                open = false;
            }
            if (Ui.Button(new Rect(inner.xMax - 260 * u, inner.yMax - 50 * u, 260 * u, 50 * u), "Reset this page"))
            {
                if (s_Tab == Tab.Controls)
                {
                    Bindings.ResetAll();
                    PadBindings.ResetAll();
                }
                else ResetPage(s_Tab);
            }
            GUI.enabled = true;

            if (capturing) DrawCapture(area, u);
            return open;
        }

        static void SetTab(Tab tab)
        {
            if (s_Tab == tab) return;
            s_Tab = tab;
            s_Scroll = Vector2.zero;
        }

        static void ResetPage(Tab tab)
        {
            switch (tab)
            {
                case Tab.Controller:
                    GameSettings.ResetController();
                    break;
                case Tab.MouseAndView:
                    GameSettings.Sensitivity = 1f;
                    GameSettings.InvertY = false;
                    GameSettings.Fov = 75f;
                    break;
                case Tab.Sound:
                    GameSettings.MasterVolume = 1f;
                    GameSettings.EffectsVolume = 1f;
                    GameSettings.VoiceVolume = 1f;
                    GameSettings.MusicVolume = 0.7f;
                    break;
                case Tab.Accessibility:
                    GameSettings.Subtitles = true;
                    GameSettings.ReduceShake = false;
                    GameSettings.Colorblind = false;
                    GameSettings.HudScale = 1f;
                    GameSettings.Tips = true;
                    break;
            }
            GameSettings.Save();
        }

        static void DrawControls(Rect body, float u)
        {
            float rowH = 46 * u;
            Ui.Label(new Rect(body.x, body.y - 14 * u, body.width, 24 * u),
                "Pick a key or button to change it, then press the new one. Esc or Start cancels. One that's already used swaps over.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
            float colW = 240 * u, padX = body.width - 24 * u - colW - 10 * u, keyX = padX - colW - 12 * u;
            Ui.Label(new Rect(body.x + keyX, body.y + 12 * u, colW, 20 * u), "KEYBOARD & MOUSE", Ui.Tiny, Ui.Muted, TextAnchor.UpperCenter);
            Ui.Label(new Rect(body.x + padX, body.y + 12 * u, colW, 20 * u), "CONTROLLER", Ui.Tiny, Ui.Muted, TextAnchor.UpperCenter);
            var view = new Rect(body.x, body.y + 36 * u, body.width, body.height - 36 * u);
            var content = new Rect(0, 0, view.width - 24 * u, rowH * Bindings.Count + 60 * u);
            int mark = PadNav.Mark;
            s_Scroll = GUI.BeginScrollView(view, s_Scroll, content, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);
            for (int i = 0; i < Bindings.Count; i++)
            {
                var action = (GameAction)i;
                var r = new Rect(0, i * rowH, content.width, rowH - 6 * u);
                if (i % 2 == 0) Ui.RoundRect(r, new Color(1f, 1f, 1f, 0.035f), small: true);
                Ui.Label(new Rect(r.x + 14 * u, r.y, keyX - 20 * u, r.height), Bindings.Label(action), Ui.Body, Ui.Text, TextAnchor.MiddleLeft);

                bool changed = Bindings.Get(action) != Bindings.Default(action);
                bool here = s_Capturing == i && !s_CapturePad;
                var keyRect = new Rect(keyX, r.y + 3 * u, colW, r.height - 6 * u);
                string label = here ? "Press a key..." : Bindings.KeyName(Bindings.Get(action)) + (changed ? "  *" : "");
                if (Ui.Button(keyRect, label, here ? Ui.ButtonAccent : Ui.ButtonStyle)) StartCapture(i, pad: false);

                var padRect = new Rect(padX, r.y + 3 * u, colW, r.height - 6 * u);
                if (PadBindings.IsFixed(action))
                {
                    Ui.Label(padRect, "Left stick", Ui.Small, Ui.Muted, TextAnchor.MiddleCenter);
                    continue;
                }
                bool padChanged = PadBindings.Get(action) != PadBindings.Default(action);
                bool padHere = s_Capturing == i && s_CapturePad;
                string padLabel = padHere ? "Press a button..." : GameInput.PadName(PadBindings.Get(action)) + (padChanged ? "  *" : "");
                if (Ui.Button(padRect, padLabel, padHere ? Ui.ButtonAccent : Ui.ButtonStyle, preferred: action == GameAction.Jump)) StartCapture(i, pad: true);
            }
            Ui.Label(new Rect(14 * u, Bindings.Count * rowH + 8 * u, content.width, 40 * u),
                "Fixed: Esc / Start menu, 1-9 shop and power-up picks (d-pad and A on a controller), mouse wheel switches weapon, right stick looks.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
            GUI.EndScrollView();
            s_Scroll = PadNav.KeepVisible(view, s_Scroll, mark);
        }

        static void StartCapture(int action, bool pad)
        {
            s_Capturing = action;
            s_CapturePad = pad;
            s_CaptureFrame = Time.frameCount;
            GameInput.Suspended = true;
        }

        static void DrawCapture(Rect area, float u)
        {
            Ui.Fill(area, new Color(0f, 0f, 0f, 0.55f));
            var box = new Rect(area.center.x - 300 * u, area.center.y - 90 * u, 600 * u, 180 * u);
            Ui.PanelBox(box, Ui.Amber, u);
            Ui.Label(new Rect(box.x, box.y + 30 * u, box.width, 40 * u), Bindings.Label((GameAction)s_Capturing), Ui.Header, Ui.Text, TextAnchor.MiddleCenter);
            string ask = s_CapturePad ? "Press the new controller button  ·  Start or Esc to cancel" : "Press the new key or mouse button  ·  Esc or Start to cancel";
            Ui.Label(new Rect(box.x, box.y + 90 * u, box.width, 30 * u), ask, Ui.Small, Ui.Muted, TextAnchor.MiddleCenter);

            // Poll once per frame, and not on the frame of the click (or A press) that started it.
            if (Event.current.type != EventType.Repaint || Time.frameCount <= s_CaptureFrame) return;
            if (GameInput.CaptureCancelPressed)
            {
                StopCapture();
                return;
            }
            if (s_CapturePad)
            {
                if (GameInput.TryGetPressedPad(out PadButton button))
                {
                    PadBindings.Set((GameAction)s_Capturing, button);
                    StopCapture();
                }
            }
            else if (GameInput.TryGetPressedKey(out KeyCode key))
            {
                Bindings.Set((GameAction)s_Capturing, key);
                StopCapture();
            }
        }

        static void StopCapture()
        {
            s_Capturing = -1;
            GameInput.Suspended = false;
        }

        /// <summary>Called when the screen is closed from outside (the menu went away).</summary>
        public static void Close()
        {
            if (IsCapturing) StopCapture();
            GameSettings.Save();
        }

        static void DrawMouseAndView(Rect body, float u)
        {
            float rowH = 60 * u, y = body.y;
            GameSettings.Sensitivity = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Mouse sensitivity", GameSettings.Sensitivity,
                GameSettings.MinSensitivity, GameSettings.MaxSensitivity, $"{GameSettings.Sensitivity:0.00}x", u);
            y += rowH;
            GameSettings.Fov = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Field of view", GameSettings.Fov,
                GameSettings.MinFov, GameSettings.MaxFov, $"{GameSettings.Fov:0}°", u);
            y += rowH;
            GameSettings.InvertY = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Invert look up/down", GameSettings.InvertY, u);
            y += rowH + 10 * u;
            Ui.Label(new Rect(body.x, y, body.width, 60 * u), "Aiming down sights slows the mouse a little on top of this, and zooms in by the same amount at any field of view.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
        }

        static void DrawController(Rect body, float u)
        {
            float rowH = 46 * u, y = body.y;
            GameSettings.StickSensitivity = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Look sensitivity", GameSettings.StickSensitivity,
                GameSettings.MinStickSensitivity, GameSettings.MaxStickSensitivity, $"{GameSettings.StickSensitivity:0.00}x", u);
            y += rowH;
            GameSettings.PadAimSensitivity = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Aiming sensitivity", GameSettings.PadAimSensitivity,
                GameSettings.MinPadAimSensitivity, GameSettings.MaxPadAimSensitivity, $"{GameSettings.PadAimSensitivity:0.00}x", u);
            y += rowH;
            GameSettings.StickDeadzone = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Stick dead zone", GameSettings.StickDeadzone,
                GameSettings.MinStickDeadzone, GameSettings.MaxStickDeadzone, $"{GameSettings.StickDeadzone * 100f:0}%", u);
            y += rowH;
            ChoiceRow(new Rect(body.x, y, body.width, rowH), "Response curve", GameInput.CurveNames, GameSettings.StickCurve, i => GameSettings.StickCurve = i, u);
            y += rowH;
            GameSettings.LookAcceleration = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Turn boost", GameSettings.LookAcceleration, u, "stick held all the way turns faster");
            y += rowH;
            GameSettings.PadInvertY = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Invert look up/down", GameSettings.PadInvertY, u);
            y += rowH;
            GameSettings.AimAssist = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Aim assist", GameSettings.AimAssist, u, "controller only");
            y += rowH;
            GUI.enabled = GUI.enabled && GameSettings.AimAssist;
            GameSettings.AimAssistStrength = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Aim assist strength", GameSettings.AimAssistStrength,
                0f, 1f, $"{GameSettings.AimAssistStrength * 100f:0}%", u);
            GUI.enabled = !IsCapturing;
            y += rowH;
            GameSettings.Vibration = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Vibration", GameSettings.Vibration, u);
            y += rowH;
            ChoiceRow(new Rect(body.x, y, body.width, rowH), "Button names", new[] { "Auto", "Xbox", "PlayStation", "Nintendo" }, (int)GameSettings.PadGlyphs,
                i => GameSettings.PadGlyphs = (PadGlyphs)i, u);
            y += rowH + 6 * u;
            Ui.Label(new Rect(body.x, y, body.width, 40 * u), "Aim assist slows your aim over a zombie, follows it while you strafe, and nudges you onto it when you aim down sights. It never fires for you.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
        }

        /// <summary>A label and a row of buttons, one of them picked.</summary>
        static void ChoiceRow(Rect r, string label, string[] names, int current, System.Action<int> pick, float u)
        {
            Ui.Label(new Rect(r.x, r.y, r.width * 0.42f, r.height), label, Ui.Body, Ui.Text, TextAnchor.MiddleLeft);
            float x = r.x + r.width * 0.42f, gap = 8 * u, bw = (r.width * 0.58f - gap * (names.Length - 1)) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var b = new Rect(x + i * (bw + gap), r.y + 6 * u, bw, r.height - 12 * u);
                if (Ui.Choice(b, current == i, names[i])) pick(i);
            }
        }

        static void DrawGraphics(Rect body, float u)
        {
            float rowH = 54 * u, y = body.y;
            Ui.Label(new Rect(body.x, y, 260 * u, rowH), "Quality", Ui.Body, Ui.Text, TextAnchor.MiddleLeft);
            float bw = 150 * u;
            for (int i = 0; i < GameSettings.QualityNames.Length; i++)
            {
                var r = new Rect(body.x + 280 * u + i * (bw + 8 * u), y + 8 * u, bw, rowH - 16 * u);
                if (Ui.Choice(r, GameSettings.Quality == i, GameSettings.QualityNames[i])) GameSettings.Quality = i;
            }
            y += rowH;
            GameSettings.Brightness = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Brightness", GameSettings.Brightness,
                GameSettings.MinBrightness, GameSettings.MaxBrightness, $"{GameSettings.Brightness * 100f:0}%", u);
            y += rowH;
            GameSettings.Bloom = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Bloom", GameSettings.Bloom, u, "glow around lamps, muzzle flashes and eyes");
            y += rowH;
            GameSettings.FilmGrain = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Film grain", GameSettings.FilmGrain, u);
            y += rowH;
            GameSettings.AnimatedZombies = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Animated zombie models", GameSettings.AnimatedZombies, u, "off = the old blocky zombies");
            y += rowH;
            GameSettings.MapArt = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Medieval map textures and props", GameSettings.MapArt, u, "off = the old procedural look; next match");
            y += rowH + 10 * u;
            Ui.Label(new Rect(body.x, y, body.width, 80 * u), "Low turns off bloom and shortens shadows for older PCs. Medium adds bloom, soft shadows and smoother edges. High adds sharper, longer shadows, reflections on metal and the smoothest edges.", Ui.Tiny, Ui.Muted, TextAnchor.UpperLeft);
        }

        static void DrawSound(Rect body, float u)
        {
            float rowH = 60 * u, y = body.y;
            GameSettings.MasterVolume = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Master volume", GameSettings.MasterVolume, 0f, 1f, $"{GameSettings.MasterVolume * 100f:0}%", u);
            y += rowH;
            GameSettings.EffectsVolume = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Effects", GameSettings.EffectsVolume, 0f, 1f, $"{GameSettings.EffectsVolume * 100f:0}%", u);
            y += rowH;
            GameSettings.VoiceVolume = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Voices and radio", GameSettings.VoiceVolume, 0f, 1f, $"{GameSettings.VoiceVolume * 100f:0}%", u);
            y += rowH;
            GameSettings.MusicVolume = Ui.Slider(new Rect(body.x, y, body.width, rowH), "Music", GameSettings.MusicVolume, 0f, 1f, $"{GameSettings.MusicVolume * 100f:0}%", u);
        }

        static void DrawAccessibility(Rect body, float u)
        {
            float rowH = 60 * u, y = body.y;
            GameSettings.Subtitles = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Subtitles", GameSettings.Subtitles, u, "voice lines and radio as text");
            y += rowH;
            GameSettings.ReduceShake = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Reduce camera shake", GameSettings.ReduceShake, u);
            y += rowH;
            GameSettings.Colorblind = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Colour-blind friendly colours", GameSettings.Colorblind, u, "blue and orange instead of green and red");
            y += rowH;
            GameSettings.Tips = Ui.Toggle(new Rect(body.x, y, body.width, rowH), "Show tips", GameSettings.Tips, u, "first-time hints while you play");
            y += rowH;
            GameSettings.HudScale = Ui.Slider(new Rect(body.x, y, body.width, rowH), "HUD size", GameSettings.HudScale,
                GameSettings.MinHudScale, GameSettings.MaxHudScale, $"{GameSettings.HudScale * 100f:0}%", u);
            y += rowH + 6 * u;
            if (Ui.Button(new Rect(body.x, y, 300 * u, 46 * u), "Show all tips again")) Tips.ResetSeen();
        }
    }
}
