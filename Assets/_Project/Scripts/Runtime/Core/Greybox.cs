using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Builds geometry from Unity primitives so the game needs no art assets, with procedurally textured
    /// materials (see <see cref="SurfaceTextures"/>). Materials are cached per colour and surface so hundreds of
    /// zombies share a handful of materials.
    /// </summary>
    public static class Greybox
    {
        static readonly Dictionary<Color, Material> s_Lit = new Dictionary<Color, Material>();
        static readonly Dictionary<(Color, Surface, bool), Material> s_Surfaces = new Dictionary<(Color, Surface, bool), Material>();
        static readonly Dictionary<(Color, Surface), Material> s_Photos = new Dictionary<(Color, Surface), Material>();
        static readonly Dictionary<Color, Material> s_Unlit = new Dictionary<Color, Material>();
        static Material s_DefaultLit, s_SurfaceBase, s_GlowBase;
        static bool s_SurfaceChecked, s_GlowChecked;

        /// <summary>Intensity of opaque glowing materials; above 1 so the bloom effect catches them.</summary>
        public const float GlowIntensity = 1.6f;

        /// <summary>
        /// A lit material with a light all-purpose texture. Projected in the object's own space, so it's safe on
        /// things that move.
        /// </summary>
        public static Material Lit(Color color) => Lit(color, Surface.Plain, true);

        /// <summary>
        /// A lit material made of <paramref name="surface"/> (concrete, brick, metal and so on) in this colour.
        /// Map pieces leave <paramref name="moving"/> false so textures line up across neighbouring pieces;
        /// anything that moves (zombies, guns, players) passes true so its texture moves with it.
        /// </summary>
        public static Material Lit(Color color, Surface surface, bool moving = false)
        {
            var key = (color, surface, moving);
            var baseMat = SurfaceBase();
            if (baseMat == null) return FlatLit(color);
            // The map (never anything that moves) uses scanned textures where it has them.
            if (!moving && SurfaceTextures.TryGetPhoto(surface, out var photo)) return PhotoLit(baseMat, color, surface, photo);
            if (s_Surfaces.TryGetValue(key, out var cached) && cached != null) return cached;
            var (detail, normal) = SurfaceTextures.Get(surface);
            var look = SurfaceTextures.LookOf(surface);
            var mat = new Material(baseMat) { name = surface + " " + ColorUtility.ToHtmlStringRGB(color) };
            mat.SetColor("_Color", color);
            mat.SetTexture("_MainTex", detail);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_Scale", look.Scale);
            mat.SetFloat("_Detail", look.Detail);
            mat.SetFloat("_BumpStrength", look.Bump);
            mat.SetFloat("_Glossiness", look.Gloss);
            mat.SetFloat("_Metallic", look.Metallic);
            mat.SetFloat("_Grime", moving ? look.Grime * 0.7f : look.Grime);
            mat.SetFloat("_ObjectSpace", moving ? 1f : 0f);
            // Hundreds of zombie parts share these; instancing draws each set in one go.
            mat.enableInstancing = true;
            s_Surfaces[key] = mat;
            return mat;
        }

        /// <summary>
        /// A map material from a scanned texture. The texture brings its own colour, so the requested colour only
        /// tints it: its hue a little, its brightness about half way (the shader doubles the texture).
        /// </summary>
        static Material PhotoLit(Material baseMat, Color color, Surface surface, SurfaceTextures.Photo photo)
        {
            var key = (color, surface);
            if (s_Photos.TryGetValue(key, out var cached) && cached != null) return cached;
            var look = SurfaceTextures.LookOf(surface);
            float lum = Mathf.Max(0.02f, color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f);
            var hue = new Color(color.r / lum, color.g / lum, color.b / lum);
            var tint = Color.Lerp(Color.white, hue, 0.3f) * Mathf.Clamp(Mathf.Lerp(1f, lum / 0.38f, 0.5f), 0.55f, 1.35f) * 0.5f;
            tint.a = 1f;
            var mat = new Material(baseMat) { name = surface + " Photo " + ColorUtility.ToHtmlStringRGB(color) };
            mat.SetColor("_Color", tint);
            mat.SetTexture("_MainTex", photo.Albedo);
            mat.SetTexture("_BumpMap", photo.NormalHeight);
            mat.SetFloat("_PackedHeight", 1f);
            mat.SetFloat("_Scale", 1f / photo.Metres);
            mat.SetFloat("_Detail", 1f);
            mat.SetFloat("_BumpStrength", 1f);
            mat.SetFloat("_Glossiness", look.Gloss);
            mat.SetFloat("_Metallic", look.Metallic * 0.6f);
            mat.SetFloat("_Grime", look.Grime * 0.6f);
            mat.SetFloat("_ObjectSpace", 0f);
            mat.enableInstancing = true;
            s_Photos[key] = mat;
            return mat;
        }

        static Material SurfaceBase()
        {
            if (s_SurfaceChecked) return s_SurfaceBase;
            s_SurfaceChecked = true;
            // Loaded from Resources so builds include the shader. If it can't run here, fall back to Standard.
            var m = Resources.Load<Material>("Materials/Surface");
            if (m != null && m.shader != null && m.shader.isSupported) s_SurfaceBase = m;
            return s_SurfaceBase;
        }

        /// <summary>A plain Standard-shader material, the fallback when the surface shader isn't available.</summary>
        static Material FlatLit(Color color)
        {
            if (s_Lit.TryGetValue(color, out var cached) && cached != null) return cached;
            if (s_DefaultLit == null)
            {
                // A real material asset in Resources makes builds include the Standard shader. Primitives'
                // built-in default material alone doesn't, which renders everything magenta in a build.
                s_DefaultLit = Resources.Load<Material>("Materials/GreyboxLit");
                if (s_DefaultLit == null)
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    s_DefaultLit = probe.GetComponent<Renderer>().sharedMaterial;
                    Object.DestroyImmediate(probe);
                }
            }
            var mat = new Material(s_DefaultLit) { name = "Greybox " + ColorUtility.ToHtmlStringRGB(color), color = color };
            s_Lit[color] = mat;
            return mat;
        }

        /// <summary>
        /// An unlit, always-bright material for muzzle flashes, tracers, lamps and zombie eyes. Opaque colours
        /// glow (bright enough to bloom); see-through colours stay plain and blended.
        /// </summary>
        public static Material Unlit(Color color)
        {
            if (s_Unlit.TryGetValue(color, out var cached) && cached != null) return cached;
            Material mat;
            var glow = color.a >= 0.99f ? GlowBase() : null;
            if (glow != null)
            {
                mat = new Material(glow);
                mat.SetFloat("_Intensity", GlowIntensity);
            }
            else
            {
                // Sprites/Default ships with every pipeline and is always included in builds.
                var shader = Shader.Find("Sprites/Default");
                mat = shader != null ? new Material(shader) : new Material(FlatLit(color));
            }
            mat.name = "Unlit " + ColorUtility.ToHtmlStringRGB(color);
            mat.color = color;
            s_Unlit[color] = mat;
            return mat;
        }

        static Material GlowBase()
        {
            if (s_GlowChecked) return s_GlowBase;
            s_GlowChecked = true;
            var m = Resources.Load<Material>("Materials/Glow");
            if (m != null && m.shader != null && m.shader.isSupported) s_GlowBase = m;
            return s_GlowBase;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Color color, bool collider = true, bool unlit = false, Surface surface = Surface.Plain, bool moving = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = unlit ? Unlit(color) : surface == Surface.Plain ? Lit(color) : Lit(color, surface, moving);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Color color, bool collider = true,
            Surface surface = Surface.Plain, bool moving = false)
            => Prim(PrimitiveType.Cube, name, parent, localPos, size, color, collider, false, surface, moving);

        public static Transform Empty(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static Font s_LabelFont;

        /// <summary>World-space text (machine names, signs), facing along the parent's forward.</summary>
        static Material s_LabelMaterial;

        /// <summary>
        /// The font's atlas on a material that walls hide (the font's own draws over everything), or the font's own
        /// material if that one can't be loaded.
        /// </summary>
        static Material LabelMaterial()
        {
            if (s_LabelMaterial != null) return s_LabelMaterial;
            var m = Resources.Load<Material>("Materials/WorldText");
            if (m == null || m.shader == null || !m.shader.isSupported) return s_LabelFont.material;
            s_LabelMaterial = new Material(m) { mainTexture = s_LabelFont.material.mainTexture };
            // The dynamic font rebuilds its atlas as it meets new characters; follow it.
            Font.textureRebuilt += f =>
            {
                if (f == s_LabelFont && s_LabelMaterial != null) s_LabelMaterial.mainTexture = f.material.mainTexture;
            };
            return s_LabelMaterial;
        }

        public static TextMesh Label(string name, Transform parent, Vector3 localPos, string text, Color color, float height = 0.2f)
        {
            if (s_LabelFont == null)
            {
                // Unity 2022.2+ ships LegacyRuntime.ttf; older versions had Arial.ttf.
                try { s_LabelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch (System.Exception) { s_LabelFont = null; }
                if (s_LabelFont == null)
                {
                    try { s_LabelFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch (System.Exception) { s_LabelFont = null; }
                }
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // TextMesh reads from behind by default; turn it to face the parent's forward.
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f;
            if (s_LabelFont != null)
            {
                tm.font = s_LabelFont;
                go.GetComponent<MeshRenderer>().sharedMaterial = LabelMaterial();
            }
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return tm;
        }

        public static void DisableShadows(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
