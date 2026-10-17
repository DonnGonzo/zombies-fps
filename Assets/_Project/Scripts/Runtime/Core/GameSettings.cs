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

        static bool s_Loaded;
        static float s_Sensitivity, s_Fov, s_Master, s_Effects, s_Voice, s_HudScale;
        static bool s_InvertY, s_Subtitles, s_ReduceShake, s_Tips, s_Colorblind;

        static void EnsureLoaded()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            s_Sensitivity = PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f);
            s_Fov = PlayerPrefs.GetFloat(Prefix + "Fov", 75f);
            s_Master = PlayerPrefs.GetFloat(Prefix + "MasterVolume", 1f);
            s_Effects = PlayerPrefs.GetFloat(Prefix + "EffectsVolume", 1f);
            s_Voice = PlayerPrefs.GetFloat(Prefix + "VoiceVolume", 1f);
            s_HudScale = PlayerPrefs.GetFloat(Prefix + "HudScale", 1f);
            s_InvertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) != 0;
            s_Subtitles = PlayerPrefs.GetInt(Prefix + "Subtitles", 1) != 0;
            s_ReduceShake = PlayerPrefs.GetInt(Prefix + "ReduceShake", 0) != 0;
            s_Tips = PlayerPrefs.GetInt(Prefix + "Tips", 1) != 0;
            s_Colorblind = PlayerPrefs.GetInt(Prefix + "Colorblind", 0) != 0;
            AudioListener.volume = s_Master;
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

        public static float HudScale
        {
            get { EnsureLoaded(); return s_HudScale; }
            set { EnsureLoaded(); s_HudScale = Mathf.Clamp(value, MinHudScale, MaxHudScale); PlayerPrefs.SetFloat(Prefix + "HudScale", s_HudScale); }
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
            HudScale = 1f;
            InvertY = false;
            Subtitles = true;
            ReduceShake = false;
            Tips = true;
            Colorblind = false;
            Save();
        }
    }
}
