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
        static readonly Color AmbientSky = new Color(0.2f, 0.23f, 0.32f);
        static readonly Color AmbientEquator = new Color(0.13f, 0.13f, 0.15f);
        static readonly Color AmbientGround = new Color(0.07f, 0.06f, 0.055f);

        static Light s_Moon;
        static Material s_Sky;
        static ReflectionProbe s_Probe;
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
            RenderSettings.fogDensity = 0.026f;

            s_Moon = new GameObject("Moonlight").AddComponent<Light>();
            s_Moon.type = LightType.Directional;
            s_Moon.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            s_Moon.color = new Color(0.62f, 0.72f, 1f);
            s_Moon.intensity = 0.55f;
            s_Moon.shadows = LightShadows.Soft;
            s_Moon.shadowStrength = 0.85f;
            s_Moon.shadowBias = 0.04f;
            s_Moon.shadowNormalBias = 0.3f;
            RenderSettings.sun = s_Moon;

            var skyBase = Resources.Load<Material>("Materials/NightSky");
            if (skyBase != null && skyBase.shader != null && skyBase.shader.isSupported)
            {
                s_Sky = new Material(skyBase);
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

        /// <summary>Shadows, anti-aliasing and reflections for the chosen quality level.</summary>
        public static void ApplyQuality()
        {
            int q = GameSettings.Quality;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowDistance = q == 0 ? 28f : q == 1 ? 50f : 80f;
            QualitySettings.shadowCascades = q == 0 ? 1 : q == 1 ? 2 : 4;
            QualitySettings.shadowResolution = q == 0 ? ShadowResolution.Low : q == 1 ? ShadowResolution.Medium : ShadowResolution.High;
            QualitySettings.antiAliasing = q == 0 ? 0 : q == 1 ? 2 : 4;
            QualitySettings.pixelLightCount = q == 0 ? 3 : q == 1 ? 5 : 8;
            QualitySettings.anisotropicFiltering = q == 0 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;
            QualitySettings.softParticles = false;
            if (s_Moon != null) s_Moon.shadows = q == 0 ? LightShadows.Hard : LightShadows.Soft;
            if (s_Probe != null) s_Probe.enabled = q >= 2;
            foreach (var cam in Camera.allCameras) ConfigureCamera(cam);
        }

        /// <summary>Sky, HDR, anti-aliasing and post effects on a camera. Safe to call again.</summary>
        public static void ConfigureCamera(Camera cam)
        {
            if (cam == null) return;
            int q = GameSettings.Quality;
            cam.clearFlags = s_Sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.allowHDR = q >= 1;
            cam.allowMSAA = q >= 1;
            if (cam.GetComponent<PostFx>() == null) cam.gameObject.AddComponent<PostFx>();
        }

        /// <summary>
        /// One reflection probe over the whole map, rendered once after it's built, so metal and wet floors reflect
        /// the sky and the lamps (High quality only).
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
            s_Probe.enabled = GameSettings.Quality >= 2;
            s_Probe.RenderProbe();
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
