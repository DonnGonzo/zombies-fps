using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>Player settings that aren't key bindings: mouse, view, sound and accessibility. Saved on the player's PC.</summary>
    public static class GameSettings
    {
        const string Prefix = "ZombiesFPS.Setting.";

        public const float MinSensitivity = 0.2f, MaxSensitivity = 3f;
        public const float MinFov = 60f, MaxFov = 100f;
        public const float MinHudScale = 0.75f, MaxHudScale = 1.3f;
        public const float MinBrightness = 0.6f, MaxBrightness = 1.6f;
        public static readonly string[] QualityNames = { "Low", "Medium", "High" };
        public static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High" };
        public static readonly string[] LightDetailNames = { "Low", "Medium", "High" };
        public static readonly string[] AntiAliasingNames = { "Off", "2x", "4x" };
        public static readonly string[] FpsLimitNames = { "Off", "60", "120", "144", "240" };
        static readonly int[] FpsLimits = { -1, 60, 120, 144, 240 };
        public static readonly string[] PerfOverlayNames = { "Off", "FPS", "Details" };

        static bool s_Loaded;
        static float s_Sensitivity, s_Fov, s_Master, s_Effects, s_Voice, s_Music, s_HudScale;
        static bool s_InvertY, s_Subtitles, s_ReduceShake, s_Tips, s_Colorblind;
        static int s_Quality, s_Shadows, s_LightDetail, s_AntiAliasing, s_FpsLimit, s_PerfOverlay;
        static bool s_Reflections, s_VSync;
        static float s_Brightness;
        static bool s_Bloom, s_Grain, s_AnimatedZombies, s_MapArt;
        static float s_StickSensitivity, s_PadAimSensitivity, s_StickDeadzone, s_AimAssistStrength;
        static int s_StickCurve;
        static bool s_LookAcceleration, s_PadInvertY, s_AimAssist, s_Vibration;
        static PadGlyphs s_PadGlyphs;

        public const float MinStickSensitivity = 0.3f, MaxStickSensitivity = 3f;
        public const float MinPadAimSensitivity = 0.3f, MaxPadAimSensitivity = 1.5f;
        public const float MinStickDeadzone = 0.03f, MaxStickDeadzone = 0.3f;
        public const float DefaultStickDeadzone = 0.12f, DefaultAimAssistStrength = 0.6f;

        /// <summary>Raised when a graphics setting changes, so the camera and lights can be set up again.</summary>
        public static event System.Action GraphicsChanged;

        static void EnsureLoaded()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            s_Sensitivity = PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f);
            s_Fov = PlayerPrefs.GetFloat(Prefix + "Fov", 75f);
            s_Master = PlayerPrefs.GetFloat(Prefix + "MasterVolume", 1f);
            s_Effects = PlayerPrefs.GetFloat(Prefix + "EffectsVolume", 1f);
            s_Voice = PlayerPrefs.GetFloat(Prefix + "VoiceVolume", 1f);
            s_Music = PlayerPrefs.GetFloat(Prefix + "MusicVolume", 0.7f);
            s_HudScale = PlayerPrefs.GetFloat(Prefix + "HudScale", 1f);
            s_InvertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) != 0;
            s_Subtitles = PlayerPrefs.GetInt(Prefix + "Subtitles", 1) != 0;
            s_ReduceShake = PlayerPrefs.GetInt(Prefix + "ReduceShake", 0) != 0;
            s_Tips = PlayerPrefs.GetInt(Prefix + "Tips", 1) != 0;
            s_Colorblind = PlayerPrefs.GetInt(Prefix + "Colorblind", 0) != 0;
            s_Quality = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality", 2), 0, QualityNames.Length - 1);
            // The detail settings start from the quality preset for players who saved one before they existed.
            s_Shadows = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Shadows", s_Quality + 1), 0, ShadowNames.Length - 1);
            s_LightDetail = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "LightDetail", s_Quality), 0, LightDetailNames.Length - 1);
            s_AntiAliasing = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "AntiAliasing", s_Quality), 0, AntiAliasingNames.Length - 1);
            s_Reflections = PlayerPrefs.GetInt(Prefix + "Reflections", s_Quality >= 2 ? 1 : 0) != 0;
            s_VSync = PlayerPrefs.GetInt(Prefix + "VSync", 1) != 0;
            s_FpsLimit = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "FpsLimit", 0), 0, FpsLimitNames.Length - 1);
            s_PerfOverlay = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "PerfOverlay", 0), 0, PerfOverlayNames.Length - 1);
            s_Brightness = PlayerPrefs.GetFloat(Prefix + "Brightness", 1f);
            s_Bloom = PlayerPrefs.GetInt(Prefix + "Bloom", 1) != 0;
            s_Grain = PlayerPrefs.GetInt(Prefix + "FilmGrain", 1) != 0;
            s_AnimatedZombies = PlayerPrefs.GetInt(Prefix + "AnimatedZombies", 1) != 0;
            s_MapArt = PlayerPrefs.GetInt(Prefix + "MapArt", 1) != 0;
            s_StickSensitivity = PlayerPrefs.GetFloat(Prefix + "StickSensitivity", 1f);
            s_PadAimSensitivity = PlayerPrefs.GetFloat(Prefix + "PadAimSensitivity", 1f);
            s_StickDeadzone = PlayerPrefs.GetFloat(Prefix + "StickDeadzone", DefaultStickDeadzone);
            s_StickCurve = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "StickCurve", 0), 0, GameInput.CurveNames.Length - 1);
            s_LookAcceleration = PlayerPrefs.GetInt(Prefix + "LookAcceleration", 1) != 0;
            s_PadInvertY = PlayerPrefs.GetInt(Prefix + "PadInvertY", 0) != 0;
            s_AimAssist = PlayerPrefs.GetInt(Prefix + "AimAssist", 1) != 0;
            s_AimAssistStrength = PlayerPrefs.GetFloat(Prefix + "AimAssistStrength", DefaultAimAssistStrength);
            s_Vibration = PlayerPrefs.GetInt(Prefix + "Vibration", 1) != 0;
            s_PadGlyphs = (PadGlyphs)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "PadGlyphs", 0), 0, 3);
            AudioListener.volume = s_Master;
        }

        /// <summary>
        /// Graphics quality preset: 0 Low, 1 Medium, 2 High. Picking one sets shadows, light detail, anti-aliasing and
        /// reflections to match (each can then be changed on its own); the preset itself still decides HDR and bloom.
        /// </summary>
        public static int Quality
        {
            get { EnsureLoaded(); return s_Quality; }
            set
            {
                EnsureLoaded();
                int v = Mathf.Clamp(value, 0, QualityNames.Length - 1);
                if (v == s_Quality && MatchesPreset(v)) return;
                s_Quality = v;
                PlayerPrefs.SetInt(Prefix + "Quality", v);
                SetInt(ref s_Shadows, v + 1, "Shadows");
                SetInt(ref s_LightDetail, v, "LightDetail");
                SetInt(ref s_AntiAliasing, v, "AntiAliasing");
                s_Reflections = v >= 2;
                PlayerPrefs.SetInt(Prefix + "Reflections", s_Reflections ? 1 : 0);
                GraphicsChanged?.Invoke();
            }
        }

        /// <summary>True when the detail settings are exactly what preset <paramref name="q"/> sets.</summary>
        public static bool MatchesPreset(int q)
        {
            EnsureLoaded();
            return s_Shadows == q + 1 && s_LightDetail == q && s_AntiAliasing == q && s_Reflections == (q >= 2);
        }

        static void SetInt(ref int field, int value, string key)
        {
            field = value;
            PlayerPrefs.SetInt(Prefix + key, value);
        }

        static void SetGraphics(ref int field, int value, int count, string key)
        {
            EnsureLoaded();
            int v = Mathf.Clamp(value, 0, count - 1);
            if (v == field) return;
            SetInt(ref field, v, key);
            GraphicsChanged?.Invoke();
        }

        /// <summary>Moonlight shadows: 0 off, 1 short and hard, 2 medium and soft, 3 long and sharp.</summary>
        public static int Shadows
        {
            get { EnsureLoaded(); return s_Shadows; }
            set => SetGraphics(ref s_Shadows, value, ShadowNames.Length, "Shadows");
        }

        /// <summary>How many lamps light each surface per pixel (the rest are blended in more cheaply): 0 Low, 1 Medium, 2 High.</summary>
        public static int LightDetail
        {
            get { EnsureLoaded(); return s_LightDetail; }
            set => SetGraphics(ref s_LightDetail, value, LightDetailNames.Length, "LightDetail");
        }

        /// <summary>Multisample anti-aliasing: 0 off, 1 2x, 2 4x.</summary>
        public static int AntiAliasing
        {
            get { EnsureLoaded(); return s_AntiAliasing; }
            set => SetGraphics(ref s_AntiAliasing, value, AntiAliasingNames.Length, "AntiAliasing");
        }

        /// <summary>Metal and wet floors reflect the sky and lamps.</summary>
        public static bool Reflections
        {
            get { EnsureLoaded(); return s_Reflections; }
            set
            {
                EnsureLoaded();
                if (value == s_Reflections) return;
                s_Reflections = value;
                PlayerPrefs.SetInt(Prefix + "Reflections", value ? 1 : 0);
                GraphicsChanged?.Invoke();
            }
        }

        /// <summary>Waits for the monitor's refresh each frame: no tearing, and the frame rate never passes the refresh rate.</summary>
        public static bool VSync
        {
            get { EnsureLoaded(); return s_VSync; }
            set
            {
                EnsureLoaded();
                if (value == s_VSync) return;
                s_VSync = value;
                PlayerPrefs.SetInt(Prefix + "VSync", value ? 1 : 0);
                GraphicsChanged?.Invoke();
            }
        }

        /// <summary>Frame rate cap with VSync off, an index into <see cref="FpsLimitNames"/> (0 = no cap).</summary>
        public static int FpsLimit
        {
            get { EnsureLoaded(); return s_FpsLimit; }
            set => SetGraphics(ref s_FpsLimit, value, FpsLimitNames.Length, "FpsLimit");
        }

        /// <summary>The frame rate cap in frames per second, or -1 for none.</summary>
        public static int TargetFrameRate => FpsLimits[FpsLimit];

        /// <summary>Corner read-out: 0 off, 1 frame rate, 2 frame rate plus frame times, draw calls and what's on screen.</summary>
        public static int PerfOverlay
        {
            get { EnsureLoaded(); return s_PerfOverlay; }
            set { EnsureLoaded(); s_PerfOverlay = Mathf.Clamp(value, 0, PerfOverlayNames.Length - 1); PlayerPrefs.SetInt(Prefix + "PerfOverlay", s_PerfOverlay); }
        }

        /// <summary>Overall brightness (1 = as designed). The game is dark on purpose; this helps on dim screens.</summary>
        public static float Brightness
        {
            get { EnsureLoaded(); return s_Brightness; }
            set { EnsureLoaded(); s_Brightness = Mathf.Clamp(value, MinBrightness, MaxBrightness); PlayerPrefs.SetFloat(Prefix + "Brightness", s_Brightness); }
        }

        /// <summary>Glow around bright lights (needs Medium or High quality).</summary>
        public static bool Bloom
        {
            get { EnsureLoaded(); return s_Bloom; }
            set { EnsureLoaded(); s_Bloom = value; PlayerPrefs.SetInt(Prefix + "Bloom", value ? 1 : 0); }
        }

        public static bool FilmGrain
        {
            get { EnsureLoaded(); return s_Grain; }
            set { EnsureLoaded(); s_Grain = value; PlayerPrefs.SetInt(Prefix + "FilmGrain", value ? 1 : 0); }
        }

        /// <summary>Zombies use the imported character model and its animations (when one is in the project).</summary>
        public static bool AnimatedZombies
        {
            get { EnsureLoaded(); return s_AnimatedZombies; }
            set { EnsureLoaded(); s_AnimatedZombies = value; PlayerPrefs.SetInt(Prefix + "AnimatedZombies", value ? 1 : 0); }
        }

        /// <summary>The map uses the scanned medieval textures and props (when they're in the project). Applies from the next match.</summary>
        public static bool MapArt
        {
            get { EnsureLoaded(); return s_MapArt; }
            set { EnsureLoaded(); s_MapArt = value; PlayerPrefs.SetInt(Prefix + "MapArt", value ? 1 : 0); }
        }

        /// <summary>Mouse sensitivity multiplier (1 = default).</summary>
        public static float Sensitivity
        {
            get { EnsureLoaded(); return s_Sensitivity; }
            set { EnsureLoaded(); s_Sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); PlayerPrefs.SetFloat(Prefix + "Sensitivity", s_Sensitivity); }
        }

        /// <summary>Hip-fire field of view in degrees.</summary>
        public static float Fov
        {
            get { EnsureLoaded(); return s_Fov; }
            set { EnsureLoaded(); s_Fov = Mathf.Clamp(Mathf.Round(value), MinFov, MaxFov); PlayerPrefs.SetFloat(Prefix + "Fov", s_Fov); }
        }

        public static float MasterVolume
        {
            get { EnsureLoaded(); return s_Master; }
            set { EnsureLoaded(); s_Master = Mathf.Clamp01(value); AudioListener.volume = s_Master; PlayerPrefs.SetFloat(Prefix + "MasterVolume", s_Master); }
        }

        /// <summary>Guns, zombies, explosions and UI sounds.</summary>
        public static float EffectsVolume
        {
            get { EnsureLoaded(); return s_Effects; }
            set { EnsureLoaded(); s_Effects = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "EffectsVolume", s_Effects); ApplyVolumes(); }
        }

        static readonly System.Collections.Generic.List<AudioSource> s_EffectSources = new System.Collections.Generic.List<AudioSource>();
        static readonly System.Collections.Generic.List<AudioSource> s_VoiceSources = new System.Collections.Generic.List<AudioSource>();

        /// <summary>Registers a sound-effects source so the effects volume slider controls it.</summary>
        public static AudioSource Effect(AudioSource source)
        {
            EnsureLoaded();
            if (source != null)
            {
                s_EffectSources.Add(source);
                source.volume = s_Effects;
            }
            return source;
        }

        /// <summary>Registers a voice or radio source so the voice volume slider controls it.</summary>
        public static AudioSource Voice(AudioSource source)
        {
            EnsureLoaded();
            if (source != null)
            {
                s_VoiceSources.Add(source);
                source.volume = s_Voice;
            }
            return source;
        }

        static void ApplyVolumes()
        {
            s_EffectSources.RemoveAll(s => s == null);
            s_VoiceSources.RemoveAll(s => s == null);
            foreach (var s in s_EffectSources) s.volume = s_Effects;
            foreach (var s in s_VoiceSources) s.volume = s_Voice;
        }

        /// <summary>Character voices and the radio.</summary>
        public static float VoiceVolume
        {
            get { EnsureLoaded(); return s_Voice; }
            set { EnsureLoaded(); s_Voice = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "VoiceVolume", s_Voice); ApplyVolumes(); }
        }

        /// <summary>The menu theme, the in-match ambience and the easter egg's music. The music player follows it each frame.</summary>
        public static float MusicVolume
        {
            get { EnsureLoaded(); return s_Music; }
            set { EnsureLoaded(); s_Music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "MusicVolume", s_Music); }
        }

        public static float HudScale
        {
            get { EnsureLoaded(); return s_HudScale; }
            set { EnsureLoaded(); s_HudScale = Mathf.Clamp(value, MinHudScale, MaxHudScale); PlayerPrefs.SetFloat(Prefix + "HudScale", s_HudScale); }
        }

        /// <summary>Right-stick look speed multiplier (1 = default).</summary>
        public static float StickSensitivity
        {
            get { EnsureLoaded(); return s_StickSensitivity; }
            set { EnsureLoaded(); s_StickSensitivity = Mathf.Clamp(value, MinStickSensitivity, MaxStickSensitivity); PlayerPrefs.SetFloat(Prefix + "StickSensitivity", s_StickSensitivity); }
        }

        /// <summary>Extra right-stick slow-down while aiming down sights (1 = the same slow-down as the mouse).</summary>
        public static float PadAimSensitivity
        {
            get { EnsureLoaded(); return s_PadAimSensitivity; }
            set { EnsureLoaded(); s_PadAimSensitivity = Mathf.Clamp(value, MinPadAimSensitivity, MaxPadAimSensitivity); PlayerPrefs.SetFloat(Prefix + "PadAimSensitivity", s_PadAimSensitivity); }
        }

        /// <summary>How far the right stick must move before the view turns (0-1 of its travel).</summary>
        public static float StickDeadzone
        {
            get { EnsureLoaded(); return s_StickDeadzone; }
            set { EnsureLoaded(); s_StickDeadzone = Mathf.Clamp(value, MinStickDeadzone, MaxStickDeadzone); PlayerPrefs.SetFloat(Prefix + "StickDeadzone", s_StickDeadzone); }
        }

        /// <summary>Right-stick response curve, an index into GameInput.CurveNames.</summary>
        public static int StickCurve
        {
            get { EnsureLoaded(); return s_StickCurve; }
            set { EnsureLoaded(); s_StickCurve = Mathf.Clamp(value, 0, GameInput.CurveNames.Length - 1); PlayerPrefs.SetInt(Prefix + "StickCurve", s_StickCurve); }
        }

        /// <summary>Holding the right stick all the way sideways turns faster after a moment.</summary>
        public static bool LookAcceleration
        {
            get { EnsureLoaded(); return s_LookAcceleration; }
            set { EnsureLoaded(); s_LookAcceleration = value; PlayerPrefs.SetInt(Prefix + "LookAcceleration", value ? 1 : 0); }
        }

        public static bool PadInvertY
        {
            get { EnsureLoaded(); return s_PadInvertY; }
            set { EnsureLoaded(); s_PadInvertY = value; PlayerPrefs.SetInt(Prefix + "PadInvertY", value ? 1 : 0); }
        }

        /// <summary>Aim assist on a controller: slow-down over zombies, tracking them as they move, and a nudge when you aim.</summary>
        public static bool AimAssist
        {
            get { EnsureLoaded(); return s_AimAssist; }
            set { EnsureLoaded(); s_AimAssist = value; PlayerPrefs.SetInt(Prefix + "AimAssist", value ? 1 : 0); }
        }

        public static float AimAssistStrength
        {
            get { EnsureLoaded(); return s_AimAssistStrength; }
            set { EnsureLoaded(); s_AimAssistStrength = Mathf.Clamp01(value); PlayerPrefs.SetFloat(Prefix + "AimAssistStrength", s_AimAssistStrength); }
        }

        /// <summary>Controller rumble on shots, hits and explosions.</summary>
        public static bool Vibration
        {
            get { EnsureLoaded(); return s_Vibration; }
            set { EnsureLoaded(); s_Vibration = value; PlayerPrefs.SetInt(Prefix + "Vibration", value ? 1 : 0); if (!value) GameInput.StopRumble(); }
        }

        /// <summary>Which button names prompts show on a controller (Auto = the kind of pad plugged in).</summary>
        public static PadGlyphs PadGlyphs
        {
            get { EnsureLoaded(); return s_PadGlyphs; }
            set { EnsureLoaded(); s_PadGlyphs = value; PlayerPrefs.SetInt(Prefix + "PadGlyphs", (int)value); }
        }

        /// <summary>Puts the Controller page back to how it shipped.</summary>
        public static void ResetController()
        {
            StickSensitivity = 1f;
            PadAimSensitivity = 1f;
            StickDeadzone = DefaultStickDeadzone;
            StickCurve = 0;
            LookAcceleration = true;
            PadInvertY = false;
            AimAssist = true;
            AimAssistStrength = DefaultAimAssistStrength;
            Vibration = true;
            PadGlyphs = PadGlyphs.Auto;
        }

        public static bool InvertY
        {
            get { EnsureLoaded(); return s_InvertY; }
            set { EnsureLoaded(); s_InvertY = value; PlayerPrefs.SetInt(Prefix + "InvertY", value ? 1 : 0); }
        }

        /// <summary>Show voice and radio lines as text.</summary>
        public static bool Subtitles
        {
            get { EnsureLoaded(); return s_Subtitles; }
            set { EnsureLoaded(); s_Subtitles = value; PlayerPrefs.SetInt(Prefix + "Subtitles", value ? 1 : 0); }
        }

        /// <summary>Accessibility: much less camera shake from hits and explosions.</summary>
        public static bool ReduceShake
        {
            get { EnsureLoaded(); return s_ReduceShake; }
            set { EnsureLoaded(); s_ReduceShake = value; PlayerPrefs.SetInt(Prefix + "ReduceShake", value ? 1 : 0); }
        }

        /// <summary>First-time tips (the tutorial).</summary>
        public static bool Tips
        {
            get { EnsureLoaded(); return s_Tips; }
            set { EnsureLoaded(); s_Tips = value; PlayerPrefs.SetInt(Prefix + "Tips", value ? 1 : 0); }
        }

        /// <summary>Accessibility: blue and yellow instead of red and green for hits, kills and done states.</summary>
        public static bool Colorblind
        {
            get { EnsureLoaded(); return s_Colorblind; }
            set { EnsureLoaded(); s_Colorblind = value; PlayerPrefs.SetInt(Prefix + "Colorblind", value ? 1 : 0); }
        }

        public static void Save() => PlayerPrefs.Save();

        public static void ResetAll()
        {
            Sensitivity = 1f;
            Fov = 75f;
            MasterVolume = 1f;
            EffectsVolume = 1f;
            VoiceVolume = 1f;
            MusicVolume = 0.7f;
            HudScale = 1f;
            InvertY = false;
            Subtitles = true;
            ReduceShake = false;
            Tips = true;
            Colorblind = false;
            Quality = 2;
            VSync = true;
            FpsLimit = 0;
            PerfOverlay = 0;
            Brightness = 1f;
            Bloom = true;
            FilmGrain = true;
            ResetController();
            Save();
        }
    }
}
