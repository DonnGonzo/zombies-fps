using UnityEngine;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Effect sprites from Ivan's Brackeys VFX bundle (Resources/Fx, CC0; tools/fx/pack_fx.py) on the FxSprite
    /// shader. Null wherever the shader or texture isn't in the build, so callers keep their old look.
    /// </summary>
    public static class FxSprites
    {
        public const string Explosion = "explosion_5x5", ExplosionSmoke = "explosion_smoke_5x5", WispySmoke = "wispy_smoke_8x8";
        public const string Smoke = "smoke", MuzzleFlame = "muzzle_flame", MuzzleStar = "muzzle_star";

        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend"), DstBlend = Shader.PropertyToID("_DstBlend");
        static readonly int AdditiveId = Shader.PropertyToID("_Additive"), GridId = Shader.PropertyToID("_Grid");
        static Material s_Base;
        static bool s_Looked;

        /// <summary>A sprite material for <paramref name="texture"/>, adding light or blended over.</summary>
        public static Material Make(string texture, bool additive, Color tint, float intensity = 1f)
        {
            if (!s_Looked)
            {
                s_Looked = true;
                s_Base = Resources.Load<Material>("Materials/FxSprite");
                if (s_Base != null && (s_Base.shader == null || !s_Base.shader.isSupported)) s_Base = null;
            }
            var tex = Resources.Load<Texture2D>("Fx/" + texture);
            if (s_Base == null || tex == null) return null;
            var mat = new Material(s_Base) { name = "Fx " + texture, mainTexture = tex, color = tint };
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat(AdditiveId, additive ? 1f : 0f);
            mat.SetFloat(SrcBlend, additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat(DstBlend, additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            var grid = Grid(texture);
            mat.SetVector(GridId, new Vector4(grid.x, grid.y, 0f, 0f));
            mat.renderQueue = 3000;
            return mat;
        }

        /// <summary>A flipbook's columns and rows, from the "_8x8" at the end of its name (1x1 for a single sprite).</summary>
        public static Vector2Int Grid(string texture)
        {
            int us = texture.LastIndexOf('_');
            if (us >= 0)
            {
                var parts = texture.Substring(us + 1).Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int c) && int.TryParse(parts[1], out int r)) return new Vector2Int(c, r);
            }
            return new Vector2Int(1, 1);
        }
    }
}
