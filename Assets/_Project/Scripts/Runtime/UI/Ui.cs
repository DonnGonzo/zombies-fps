using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The look shared by the HUD and every menu: palette, a condensed system font, rounded panels and
    /// buttons, bars, key caps and sliders, all drawn with IMGUI and textures made in code.
    /// </summary>
    public static class Ui
    {
        // Palette.
        public static readonly Color Text = new Color(0.93f, 0.92f, 0.9f);
        public static readonly Color Muted = new Color(0.62f, 0.64f, 0.68f);
        public static readonly Color Panel = new Color(0.055f, 0.06f, 0.075f, 0.92f);
        public static readonly Color PanelLight = new Color(0.11f, 0.12f, 0.14f, 0.95f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Blood = new Color(0.86f, 0.16f, 0.12f);
        public static readonly Color Amber = new Color(1f, 0.74f, 0.28f);
        public static readonly Color Violet = new Color(0.72f, 0.58f, 1f);
        public static readonly Color Sky = new Color(0.45f, 0.78f, 1f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.75f);

        /// <summary>"Good" and "bad" colours, swapped for blue and orange in colour-blind mode.</summary>
        public static Color Good => GameSettings.Colorblind ? new Color(0.35f, 0.7f, 1f) : new Color(0.45f, 0.95f, 0.5f);
        public static Color Bad => GameSettings.Colorblind ? new Color(1f, 0.6f, 0.1f) : new Color(1f, 0.38f, 0.3f);

        static Texture2D s_White, s_Round, s_RoundSmall, s_Glow;
        static Font s_Font;
        static int s_StyleSize = -1;
        static float s_StyleScale = -1f;

        public static GUIStyle Body, Small, Tiny, Bold, Header, Title, Huge, ButtonStyle, ButtonAccent, Field, KeyCap, SliderBar, SliderThumb, ToggleStyle;

        /// <summary>Menu scale: 1 at 1080p.</summary>
        public static float MenuU => Screen.height / 1080f;
        /// <summary>HUD scale: 1 at 1080p, times the HUD scale setting.</summary>
        public static float HudU => Screen.height / 1080f * GameSettings.HudScale;

        public static Texture2D White
        {
            get
            {
                if (s_White == null)
                {
                    s_White = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                    s_White.SetPixel(0, 0, Color.white);
                    s_White.Apply();
                }
                return s_White;
            }
        }

        static Texture2D RoundTex(int size, float radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
            }
            t.Apply();
            return t;
        }

        /// <summary>A soft radial spot, for glows behind banners.</summary>
        static Texture2D GlowTex(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            t.Apply();
            return t;
        }

        static Texture2D Round => s_Round != null ? s_Round : (s_Round = RoundTex(32, 10f));
        static Texture2D RoundSmall => s_RoundSmall != null ? s_RoundSmall : (s_RoundSmall = RoundTex(16, 5f));
        public static Texture2D Glow => s_Glow != null ? s_Glow : (s_Glow = GlowTex(64));

        static Font UiFont
        {
            get
            {
                if (s_Font == null)
                {
                    // Bahnschrift ships with Windows 10+ and reads like a game HUD; the others are fallbacks.
                    try { s_Font = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Helvetica Neue", "Arial" }, 32); }
                    catch (System.Exception) { s_Font = null; }
                }
                return s_Font;
            }
        }

        /// <summary>Builds the styles for a scale. Call at the top of OnGUI.</summary>
        public static void EnsureStyles(float u)
        {
            int size = Mathf.Max(11, Mathf.RoundToInt(22 * u));
            if (Body != null && size == s_StyleSize && Mathf.Approximately(u, s_StyleScale)) return;
            s_StyleSize = size;
            s_StyleScale = u;
            var font = UiFont;

            Body = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = false, richText = true, clipping = TextClipping.Overflow };
            if (font != null) Body.font = font;
            Body.normal.textColor = Text;
            Body.padding = new RectOffset(0, 0, 0, 0);
            Small = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(size * 0.8f) };
            Tiny = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(size * 0.66f) };
            Bold = new GUIStyle(Body) { fontStyle = FontStyle.Bold };
            Header = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(size * 1.35f), fontStyle = FontStyle.Bold };
            Title = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(size * 2.6f), fontStyle = FontStyle.Bold };
            Huge = new GUIStyle(Body) { fontSize = Mathf.RoundToInt(size * 3.4f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(size * 0.95f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(10, 10, 10, 10),
                padding = new RectOffset(12, 12, 6, 6),
                margin = new RectOffset(0, 0, Mathf.RoundToInt(4 * u), Mathf.RoundToInt(4 * u)),
                fixedHeight = Mathf.Round(48 * u),
                richText = true,
            };
            if (font != null) ButtonStyle.font = font;
            SetBackgrounds(ButtonStyle, new Color(0.16f, 0.17f, 0.2f, 0.95f), new Color(0.24f, 0.25f, 0.29f, 1f), Blood * 0.9f, Text, Color.white);
            ButtonAccent = new GUIStyle(ButtonStyle);
            SetBackgrounds(ButtonAccent, new Color(0.62f, 0.1f, 0.08f, 0.95f), new Color(0.78f, 0.15f, 0.11f, 1f), new Color(0.95f, 0.25f, 0.18f, 1f), Color.white, Color.white);

            Field = new GUIStyle(GUI.skin.textField)
            {
                fontSize = size,
                border = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(10, 10, 6, 6),
                fixedHeight = Mathf.Round(42 * u),
                alignment = TextAnchor.MiddleLeft,
            };
            if (font != null) Field.font = font;
            SetBackgrounds(Field, new Color(0.02f, 0.02f, 0.03f, 0.9f), new Color(0.05f, 0.05f, 0.07f, 0.95f), new Color(0.05f, 0.05f, 0.07f, 0.95f), Text, Color.white, small: true);
            Field.focused.background = Field.hover.background;
            Field.focused.textColor = Color.white;

            KeyCap = new GUIStyle(Body)
            {
                fontSize = Mathf.RoundToInt(size * 0.72f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(Mathf.RoundToInt(7 * u), Mathf.RoundToInt(7 * u), 0, 0),
            };
            KeyCap.normal.background = TintTex(RoundSmall, new Color(0.92f, 0.92f, 0.9f, 0.95f));
            KeyCap.normal.textColor = new Color(0.06f, 0.06f, 0.07f);

            SliderBar = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = Mathf.Round(8 * u), border = new RectOffset(4, 4, 4, 4), margin = new RectOffset(0, 0, Mathf.RoundToInt(16 * u), 0) };
            SliderBar.normal.background = TintTex(RoundSmall, new Color(1f, 1f, 1f, 0.18f));
            SliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = Mathf.Round(22 * u), fixedWidth = Mathf.Round(22 * u), border = new RectOffset(5, 5, 5, 5), margin = new RectOffset(0, 0, Mathf.RoundToInt(-7 * u), 0) };
            SliderThumb.normal.background = TintTex(RoundSmall, Blood);
            SliderThumb.hover.background = TintTex(RoundSmall, new Color(1f, 0.35f, 0.28f));
            SliderThumb.active.background = TintTex(RoundSmall, Color.white);

            ToggleStyle = new GUIStyle(ButtonStyle) { fixedWidth = Mathf.Round(92 * u) };
        }

        static void SetBackgrounds(GUIStyle style, Color normal, Color hover, Color active, Color text, Color hoverText, bool small = false)
        {
            var src = small ? RoundSmall : Round;
            style.normal.background = TintTex(src, normal);
            style.hover.background = TintTex(src, hover);
            style.active.background = TintTex(src, active);
            style.focused.background = style.normal.background;
            style.onNormal.background = TintTex(src, active);
            style.onHover.background = TintTex(src, active);
            style.normal.textColor = text;
            style.hover.textColor = hoverText;
            style.active.textColor = Color.white;
            style.focused.textColor = text;
            style.onNormal.textColor = Color.white;
            style.onHover.textColor = Color.white;
        }

        static readonly System.Collections.Generic.Dictionary<(Texture2D, Color), Texture2D> s_Tinted = new System.Collections.Generic.Dictionary<(Texture2D, Color), Texture2D>();

        /// <summary>A tinted copy of a texture, made once per colour (styles rebuild when the scale changes).</summary>
        static Texture2D TintTex(Texture2D src, Color c)
        {
            if (s_Tinted.TryGetValue((src, c), out var cached) && cached != null) return cached;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = src.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(c.r, c.g, c.b, c.a * px[i].a);
            t.SetPixels(px);
            t.Apply();
            s_Tinted[(src, c)] = t;
            return t;
        }

        // ---------------------------------------------------------------- drawing

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = old;
        }

        static GUIStyle s_RoundStyle, s_RoundSmallStyle;

        /// <summary>A rounded panel, tinted with GUI.color so it works inside scroll views and groups.</summary>
        public static void RoundRect(Rect r, Color c, bool small = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (s_RoundStyle == null || s_RoundStyle.normal.background == null)
            {
                s_RoundStyle = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
                s_RoundStyle.normal.background = Round;
                s_RoundSmallStyle = new GUIStyle { border = new RectOffset(5, 5, 5, 5) };
                s_RoundSmallStyle.normal.background = RoundSmall;
            }
            var old = GUI.color;
            GUI.color = c;
            (small ? s_RoundSmallStyle : s_RoundStyle).Draw(r, GUIContent.none, false, false, false, false);
            GUI.color = old;
        }

        /// <summary>A dark rounded panel with a thin accent strip along the top.</summary>
        public static void PanelBox(Rect r, Color accent, float u, float alpha = 1f)
        {
            RoundRect(new Rect(r.x + 3 * u, r.y + 5 * u, r.width, r.height), new Color(0f, 0f, 0f, 0.35f * alpha));
            RoundRect(r, new Color(Panel.r, Panel.g, Panel.b, Panel.a * alpha));
            if (accent.a > 0f) Fill(new Rect(r.x + 10 * u, r.y, r.width - 20 * u, Mathf.Max(2f, 3 * u)), new Color(accent.r, accent.g, accent.b, accent.a * alpha));
        }

        public static void GlowSpot(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Glow);
            GUI.color = old;
        }

        /// <summary>A progress bar with a dark rounded track.</summary>
        public static void Bar(Rect r, float fraction, Color fill, Color? track = null)
        {
            RoundRect(r, track ?? new Color(0f, 0f, 0f, 0.55f), small: true);
            float f = Mathf.Clamp01(fraction);
            if (f <= 0f) return;
            var inner = new Rect(r.x, r.y, Mathf.Max(r.height, r.width * f), r.height);
            if (f < 1f && inner.width > r.width * f + 0.5f)
            {
                // Very small fractions: a plain sliver instead of a rounded blob wider than the value.
                Fill(new Rect(r.x, r.y, r.width * f, r.height), fill);
                return;
            }
            RoundRect(inner, fill, small: true);
        }

        /// <summary>Text with a soft drop shadow.</summary>
        public static void Label(Rect r, string text, GUIStyle style, Color color, TextAnchor? align = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            var oldColor = style.normal.textColor;
            var oldAlign = style.alignment;
            if (align.HasValue) style.alignment = align.Value;
            float o = Mathf.Max(1f, style.fontSize / 14f);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * color.a);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
            style.normal.textColor = oldColor;
            style.alignment = oldAlign;
        }

        public static void Label(Rect r, string text, GUIStyle style, TextAnchor? align = null) => Label(r, text, style, style.normal.textColor, align);

        public static float Width(string text, GUIStyle style) => style.CalcSize(new GUIContent(text)).x;

        /// <summary>How wide Key draws a cap.</summary>
        public static float KeyWidth(string key, float height) => Mathf.Max(height, Width(key, KeyCap) + KeyCap.padding.horizontal);

        /// <summary>A key cap like [E]. Returns its width.</summary>
        public static float Key(Vector2 at, string key, float height)
        {
            float w = KeyWidth(key, height);
            if (Event.current.type == EventType.Repaint)
            {
                var r = new Rect(at.x, at.y, w, height);
                RoundRect(new Rect(at.x + 1, at.y + 2, w, height), new Color(0f, 0f, 0f, 0.5f), small: true);
                if (GameInput.FaceColor(key, out Color face))
                {
                    // A controller face button in its own colour, like the pad in the player's hands.
                    RoundRect(r, face, small: true);
                    var old = KeyCap.normal.textColor;
                    KeyCap.normal.textColor = Color.white;
                    GUI.Label(r, key, KeyCap);
                    KeyCap.normal.textColor = old;
                }
                else GUI.Label(r, key, KeyCap);
            }
            return w;
        }

        /// <summary>"Press [E] to buy the gun": a key cap followed by text, centred on cx.</summary>
        public static void Prompt(float cx, float y, string key, string text, GUIStyle style, Color color, float u)
        {
            float h = style.fontSize * 1.35f;
            float kw = Mathf.Max(h, Width(key, KeyCap) + KeyCap.padding.horizontal);
            float tw = Width(text, style);
            float gap = 10 * u;
            float x = cx - (kw + gap + tw) / 2f;
            Key(new Vector2(x, y), key, h);
            Label(new Rect(x + kw + gap, y, tw + 4, h), text, style, color, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// A menu button. On a controller it can take the focus (preferred: where focus starts on this screen) and
        /// A presses it; a cancel button is also pressed by B, so mark each screen's Back or Cancel button with it.
        /// </summary>
        public static bool Button(Rect r, string text, bool accent = false, bool cancel = false, bool preferred = false, bool focusable = true) =>
            Button(r, text, accent ? ButtonAccent : ButtonStyle, cancel, preferred, focusable);

        public static bool Button(Rect r, string text, GUIStyle style, bool cancel = false, bool preferred = false, bool focusable = true)
        {
            bool clicked = GUI.Button(r, text, style);
            if (focusable && PadNav.Register(r, PadNav.Kind.Button, preferred) && PadNav.TakeSubmit()) clicked = true;
            if (cancel && PadNav.TakeCancel()) clicked = true;
            return clicked;
        }

        /// <summary>One of a row of choices drawn as buttons (tabs, quality levels). Returns true when picked.</summary>
        public static bool Choice(Rect r, bool on, string text, bool focusable = true)
        {
            bool picked = GUI.Toggle(r, on, text, ButtonStyle) && !on;
            if (focusable && PadNav.Register(r, PadNav.Kind.Button) && PadNav.TakeSubmit()) picked = true;
            return picked;
        }

        /// <summary>A labelled slider row. Returns the new value.</summary>
        public static float Slider(Rect r, string label, float value, float min, float max, string valueText, float u)
        {
            float lw = r.width * 0.42f, vw = 90 * u;
            Label(new Rect(r.x, r.y, lw, r.height), label, Body, Text, TextAnchor.MiddleLeft);
            float v = GUI.HorizontalSlider(new Rect(r.x + lw, r.y + r.height / 2f - 12 * u, r.width - lw - vw - 12 * u, 24 * u), value, min, max, SliderBar, SliderThumb);
            Label(new Rect(r.xMax - vw, r.y, vw, r.height), valueText, Body, Muted, TextAnchor.MiddleRight);
            // On a controller, left and right move a focused slider a twentieth of its range at a time.
            if (PadNav.Register(r, PadNav.Kind.Slider) && PadNav.Adjust != 0) v = Mathf.Clamp(v + PadNav.Adjust * (max - min) / 20f, min, max);
            return v;
        }

        /// <summary>A labelled On/Off row. Returns the new value.</summary>
        public static bool Toggle(Rect r, string label, bool value, float u, string hint = null)
        {
            Label(new Rect(r.x, r.y, r.width - 110 * u, r.height), label, Body, Text, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(hint))
                Label(new Rect(r.x + Width(label, Body) + 14 * u, r.y, r.width, r.height), hint, Tiny, Muted, TextAnchor.MiddleLeft);
            var b = new Rect(r.xMax - 96 * u, r.y + (r.height - 40 * u) / 2f, 96 * u, 40 * u);
            if (GUI.Button(b, value ? "ON" : "OFF", value ? ButtonAccent : ButtonStyle)) value = !value;
            else if (PadNav.Register(r, PadNav.Kind.Toggle) && PadNav.TakeSubmit()) value = !value;
            return value;
        }

        /// <summary>Colour with a different alpha.</summary>
        public static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
