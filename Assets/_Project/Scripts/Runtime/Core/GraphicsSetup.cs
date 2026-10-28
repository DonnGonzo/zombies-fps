using UnityEngine;
using UnityEngine.Rendering;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// The look of the game: night sky, moonlight, fog and ambient light, quality levels (shadows,
    /// anti-aliasing, reflections) and the post effects on each camera.
    /// </summary>
    public static class GraphicsSetup
    {
        public static readonly Color FogColor = new Color(0.055f, 0.065f, 0.09f);
        // Moonlit night, but bright enough in the shadows to read the room and see what's coming.
        static readonly Color AmbientSky = new Color(0.3f, 0.33f, 0.43f);
        static readonly Color AmbientEquator = new Color(0.22f, 0.21f, 0.23f);
        static readonly Color AmbientGround = new Color(0.13f, 0.11f, 0.1f);

        static Light s_Moon;
        static Material s_Sky;
        static ReflectionProbe s_Probe;
        static bool s_ProbeRendered;
        static bool s_Hooked;

        /// <summary>Sky, moon, fog and ambient light. Called once at start-up, before the map is built.</summary>
        public static void SetUpEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = 0.019f;

            s_Moon = new GameObject("Moonlight").AddComponent<Light>();
            s_Moon.type = LightType.Directional;
            // High enough that moonlight reaches down past the 7 m walls into the rooms, not just the wall tops.
            s_Moon.transform.rotation = Quaternion.Euler(56f, -35f, 0f);
            s_Moon.color = new Color(0.66f, 0.75f, 1f);
            s_Moon.intensity = 0.8f;
            s_Moon.shadows = LightShadows.Soft;
            s_Moon.shadowStrength = 0.85f;
            s_Moon.shadowBias = 0.04f;
            s_Moon.shadowNormalBias = 0.3f;
            RenderSettings.sun = s_Moon;

            var skyBase = Resources.Load<Material>("Materials/NightSky");
            if (skyBase != null && skyBase.shader != null && skyBase.shader.isSupported)
            {
                if (s_Sky == null) s_Sky = new Material(skyBase);
                s_Sky.SetVector("_MoonDir", -s_Moon.transform.forward);
                RenderSettings.skybox = s_Sky;
            }
            else
            {
                RenderSettings.skybox = null;
            }
            Shader.SetGlobalFloat("_ZFBlackout", 0f);

            ApplyQuality();
            if (!s_Hooked)
            {
                s_Hooked = true;
                GameSettings.GraphicsChanged += ApplyQuality;
            }
        }

        /// <summary>Shadows, light detail, anti-aliasing, reflections and frame pacing from the graphics settings.</summary>
        public static void ApplyQuality()
        {
            int shadows = GameSettings.Shadows;
            QualitySettings.shadows = shadows == 0 ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = shadows <= 1 ? 28f : shadows == 2 ? 50f : 80f;
            QualitySettings.shadowCascades = shadows <= 1 ? 1 : shadows == 2 ? 2 : 4;
            QualitySettings.shadowResolution = shadows <= 1 ? ShadowResolution.Low : shadows == 2 ? ShadowResolution.Medium : ShadowResolution.High;
            QualitySettings.antiAliasing = AntiAliasingSamples;
            // Every extra per-pixel light is another draw for each lit part, and zombies have dozens of parts; the
            // map is cut into small pieces so a few each are enough.
            int lights = GameSettings.LightDetail;
            QualitySettings.pixelLightCount = lights == 0 ? 3 : lights == 1 ? 4 : 6;
            QualitySettings.anisotropicFiltering = GameSettings.Quality == 0 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
            QualitySettings.softParticles = false;
            // VSync paces frames to the monitor; without it the frame rate cap (if any) does.
            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
            Application.targetFrameRate = GameSettings.VSync ? -1 : GameSettings.TargetFrameRate;
            if (s_Moon != null) s_Moon.shadows = shadows == 0 ? LightShadows.None : shadows == 1 ? LightShadows.Hard : LightShadows.Soft;
            if (s_Probe != null)
            {
                bool wasOn = s_Probe.enabled;
                s_Probe.enabled = GameSettings.Reflections;
                // Starting with reflections off never rendered it; render now or metal reflects black.
                if (s_Probe.enabled && (!wasOn || !s_ProbeRendered)) RenderProbe();
            }
            foreach (var cam in Camera.allCameras) ConfigureCamera(cam);
        }

        static int AntiAliasingSamples => GameSettings.AntiAliasing == 0 ? 0 : GameSettings.AntiAliasing == 1 ? 2 : 4;

        /// <summary>Sky, HDR, anti-aliasing and post effects on a camera. Safe to call again.</summary>
        public static void ConfigureCamera(Camera cam)
        {
            if (cam == null) return;
            int q = GameSettings.Quality;
            cam.clearFlags = s_Sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.allowHDR = q >= 1;
            cam.allowMSAA = AntiAliasingSamples > 0;
            if (cam.GetComponent<PostFx>() == null) cam.gameObject.AddComponent<PostFx>();
        }

        /// <summary>
        /// One reflection probe over the whole map, rendered once after it's built, so metal and wet floors reflect
        /// the sky and the lamps (when Reflections is on).
        /// </summary>
        public static void BakeReflections(Bounds bounds)
        {
            if (s_Probe == null)
            {
                var go = new GameObject("Reflections");
                s_Probe = go.AddComponent<ReflectionProbe>();
                s_Probe.mode = ReflectionProbeMode.Realtime;
                s_Probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                s_Probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                s_Probe.resolution = 128;
                s_Probe.hdr = true;
                s_Probe.intensity = 0.8f;
                s_Probe.boxProjection = false;
                s_Probe.importance = 0;
            }
            s_Probe.transform.position = bounds.center + Vector3.up * 3f;
            s_Probe.size = bounds.size + Vector3.up * 20f;
            s_Probe.enabled = GameSettings.Reflections;
            if (s_Probe.enabled) RenderProbe();
        }

        static void RenderProbe()
        {
            s_Probe.RenderProbe();
            s_ProbeRendered = true;
        }

        /// <summary>Darkens the sky and the ambient light during a blackout round (0 normal, 1 full blackout).</summary>
        public static void SetBlackout(float t)
        {
            Shader.SetGlobalFloat("_ZFBlackout", t);
            RenderSettings.ambientEquatorColor = Color.Lerp(AmbientEquator, new Color(0.05f, 0.03f, 0.05f), t);
            RenderSettings.ambientGroundColor = Color.Lerp(AmbientGround, new Color(0.03f, 0.02f, 0.02f), t);
        }
    }
}
