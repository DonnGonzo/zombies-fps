using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Post effects on a camera (built-in render pipeline): bloom, filmic tone mapping, a cold colour grade,
    /// vignette and film grain, plus the brightness setting. Bloom is skipped on Low quality.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PostFx : MonoBehaviour
    {
        const int PassPrefilter = 0, PassDown = 1, PassUp = 2, PassComposite = 3;
        const int MaxIterations = 6;

        static Material s_Material;
        static bool s_Checked;

        readonly RenderTexture[] _chain = new RenderTexture[MaxIterations];

        /// <summary>Extra exposure for a moment (the blackout flicker, a flashbang). 1 = none.</summary>
        public static float Flash = 1f;

        static Material Material
        {
            get
            {
                if (s_Checked) return s_Material;
                s_Checked = true;
                var m = Resources.Load<Material>("Materials/Post");
                if (m != null && m.shader != null && m.shader.isSupported) s_Material = new Material(m);
                return s_Material;
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var mat = Material;
            if (mat == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            int quality = GameSettings.Quality;
            // Brightness raises exposure and also lifts the shadows (a gamma below 1), since exposure alone
            // barely moves near-black corners.
            float brightness = GameSettings.Brightness;
            mat.SetFloat("_Exposure", 1.12f * brightness * Flash);
            mat.SetFloat("_Lift", 0.88f / Mathf.Pow(brightness, 0.6f));
            mat.SetFloat("_Saturation", 0.88f);
            mat.SetFloat("_Contrast", 1.04f);
            mat.SetFloat("_Vignette", 0.3f);
            mat.SetFloat("_Grain", GameSettings.FilmGrain ? 0.035f : 0f);
            mat.SetColor("_ShadowTint", new Color(0.9f, 0.97f, 1.12f));
            mat.SetColor("_HighlightTint", new Color(1.06f, 1.0f, 0.92f));

            int iterations = quality >= 2 ? 6 : 4;
            if (quality == 0 || !GameSettings.Bloom)
            {
                mat.SetFloat("_BloomIntensity", 0f);
                mat.SetTexture("_BloomTex", Texture2D.blackTexture);
                Graphics.Blit(source, destination, mat, PassComposite);
                return;
            }

            const float threshold = 0.9f, knee = 0.5f;
            mat.SetVector("_Threshold", new Vector4(threshold, threshold - knee, 2f * knee, 0.25f / knee));
            mat.SetFloat("_BloomIntensity", 0.65f);

            var format = source.format;
            int w = source.width / 2, h = source.height / 2;
            var current = _chain[0] = RenderTexture.GetTemporary(w, h, 0, format);
            Graphics.Blit(source, current, mat, PassPrefilter);
            int count = 1;
            for (; count < iterations; count++)
            {
                w /= 2;
                h /= 2;
                if (w < 4 || h < 4) break;
                var next = _chain[count] = RenderTexture.GetTemporary(w, h, 0, format);
                Graphics.Blit(current, next, mat, PassDown);
                current = next;
            }
            for (int i = count - 2; i >= 0; i--)
            {
                // Additive blend: each level adds its blur onto the next bigger one.
                Graphics.Blit(current, _chain[i], mat, PassUp);
                current = _chain[i];
            }

            mat.SetTexture("_BloomTex", current);
            Graphics.Blit(source, destination, mat, PassComposite);
            for (int i = 0; i < count; i++)
            {
                RenderTexture.ReleaseTemporary(_chain[i]);
                _chain[i] = null;
            }
        }
    }
}
