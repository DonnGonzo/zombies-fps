using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Builds greybox geometry from Unity primitives so the prototype needs no art assets.
    /// Materials are cached per colour so hundreds of zombies share a handful of materials.
    /// </summary>
    public static class Greybox
    {
        static readonly Dictionary<Color, Material> s_Lit = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> s_Unlit = new Dictionary<Color, Material>();
        static Material s_DefaultLit;

        /// <summary>A lit material in the active render pipeline's default shader.</summary>
        public static Material Lit(Color color)
        {
            if (s_Lit.TryGetValue(color, out var cached) && cached != null) return cached;
            if (s_DefaultLit == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s_DefaultLit = probe.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(probe);
            }
            var mat = new Material(s_DefaultLit) { name = "Greybox " + ColorUtility.ToHtmlStringRGB(color), color = color };
            s_Lit[color] = mat;
            return mat;
        }

        /// <summary>An unlit, always-bright material for muzzle flashes, tracers and zombie eyes.</summary>
        public static Material Unlit(Color color)
        {
            if (s_Unlit.TryGetValue(color, out var cached) && cached != null) return cached;
            // Sprites/Default ships with every pipeline and is always included in builds.
            var shader = Shader.Find("Sprites/Default");
            var mat = shader != null ? new Material(shader) : new Material(Lit(color));
            mat.name = "Unlit " + ColorUtility.ToHtmlStringRGB(color);
            mat.color = color;
            s_Unlit[color] = mat;
            return mat;
        }

        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Color color, bool collider = true, bool unlit = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = unlit ? Unlit(color) : Lit(color);
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Color color, bool collider = true)
            => Prim(PrimitiveType.Cube, name, parent, localPos, size, color, collider);

        public static Transform Empty(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
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
