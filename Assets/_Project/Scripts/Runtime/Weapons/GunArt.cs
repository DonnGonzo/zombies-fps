using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Gun and shield models from Ivan's TenWeapons pack. Each lives in Resources/Guns as slug.bytes (a PRP1 mesh
    /// made by tools/tenweapons/pack_guns.py, already scaled for the view model with its sight line on
    /// <see cref="GunModels.SightY"/>), plus slug_optic.bytes for an optic housing or scope tube that hides while
    /// aiming. guns.txt holds each gun's muzzle, bounds and sight. Submeshes are keyed by the pack's material
    /// names and drawn with the game's own lit, glow and glass materials. When a file is missing the game falls
    /// back to the primitive guns in <see cref="GunModels"/>.
    /// </summary>
    public static class GunArt
    {
        /// <summary>Which model each gun uses, by display name. The pack has nine guns for our ten: the Brennan DMR
        /// borrows the sniper rifle, shorter and in its own colours.</summary>
        static readonly Dictionary<string, (string slug, float scale, bool ownColours)> s_ByGun = new Dictionary<string, (string, float, bool)>
        {
            { "Rook-9 Carbine", ("atlas5", 1f, false) },
            { "Hatchet .44", ("harker9", 1f, false) },
            { "Wren-7 SMG", ("sable9", 1f, false) },
            { "Brennan DMR", ("specter308", 0.88f, true) },
            { "Talon LMG", ("grinder", 1f, false) },
            { "Mauler 12 Shotgun", ("riot12", 1f, false) },
            { "Kestrel AR", ("kodiak7", 1f, false) },
            { "Vanta .50 Sniper", ("specter308", 1f, false) },
            { "Ion Lance", ("helix", 1f, false) },
            { "Scrap Cannon", ("pulse", 1f, false) },
        };

        public const string Shield = "shield";
        /// <summary>The Aegis Shield's four build parts, in part order.</summary>
        public static readonly string[] ShieldParts = { "shield_plate", "shield_view", "shield_core", "shield_brace" };

        sealed class Info
        {
            public string Kind;
            public Vector3 Muzzle, Min, Max;
            public float ScopeFront, ScopeRadius;
        }

        static Dictionary<string, Info> s_Info;
        static readonly Dictionary<string, (Mesh mesh, string[] keys)> s_Meshes = new Dictionary<string, (Mesh, string[])>();

        // The pack's materials: sRGB colour, and what the game draws them as.
        enum Look { Metal, Paint, Polymer, Wood, Rubber, Detail, Lens, Glow, Glass }

        static readonly Dictionary<string, (Color32 color, Look look)> s_Materials = new Dictionary<string, (Color32, Look)>
        {
            { "ParkerizedSteel", (new Color32(64, 69, 72, 255), Look.Metal) },
            { "BlackSteel", (new Color32(29, 33, 37, 255), Look.Metal) },
            { "BrushedMetal", (new Color32(116, 122, 127, 255), Look.Detail) },
            { "BluedSteel", (new Color32(47, 55, 68, 255), Look.Metal) },
            { "Gunmetal", (new Color32(56, 58, 63, 255), Look.Metal) },
            { "Polymer", (new Color32(42, 45, 46, 255), Look.Polymer) },
            { "SlatePolymer", (new Color32(70, 77, 82, 255), Look.Polymer) },
            { "SandCerakote", (new Color32(152, 137, 110, 255), Look.Paint) },
            { "OliveCerakote", (new Color32(79, 87, 67, 255), Look.Paint) },
            { "DarkWalnut", (new Color32(90, 52, 32, 255), Look.Wood) },
            { "BirchWood", (new Color32(146, 109, 68, 255), Look.Wood) },
            { "Rubber", (new Color32(24, 25, 28, 255), Look.Rubber) },
            { "Bronze", (new Color32(145, 103, 61, 255), Look.Detail) },
            { "Brass", (new Color32(199, 164, 80, 255), Look.Detail) },
            { "Copper", (new Color32(175, 92, 60, 255), Look.Detail) },
            { "Warning", (new Color32(221, 165, 52, 255), Look.Detail) },
            { "RedDetail", (new Color32(169, 41, 49, 255), Look.Detail) },
            { "Optics", (new Color32(19, 36, 42, 255), Look.Lens) },
            { "EmissiveCyan", (new Color32(65, 204, 244, 255), Look.Glow) },
            { "EmissiveAmber", (new Color32(251, 150, 62, 255), Look.Glow) },
            { "EmissiveViolet", (new Color32(177, 91, 255, 255), Look.Glow) },
            { "EnergyGlass", (new Color32(59, 102, 154, 150), Look.Glass) },
            { "ShieldGlass", (new Color32(80, 152, 177, 145), Look.Glass) },
            { "WornPaint", (new Color32(74, 91, 95, 255), Look.Paint) },
        };

        /// <summary>
        /// A material for one of the pack's material keys. A camo paints the gun as metal: its paint, polymer and
        /// wood in the camo colour and its main steel a shade darker, leaving small parts, rubber and glow as they
        /// are. A furniture colour (a gun borrowing another's model) repaints just the paint, polymer and wood.
        /// </summary>
        static Material MaterialFor(string key, Color? camo, Color? furniture)
        {
            if (!s_Materials.TryGetValue(key, out var m)) m = (new Color32(60, 62, 66, 255), Look.Metal);
            Color c = m.color;
            switch (m.look)
            {
                case Look.Glow: return Greybox.Unlit(new Color(c.r, c.g, c.b, 1f));
                case Look.Glass: return Greybox.Unlit(new Color(c.r, c.g, c.b, 0.45f));
                case Look.Lens: return Greybox.Lit(c, Surface.Plain, true);
            }
            bool furnitureLike = m.look == Look.Paint || m.look == Look.Polymer || m.look == Look.Wood;
            if (camo.HasValue)
            {
                var k = camo.Value;
                if (furnitureLike) return Greybox.Lit(new Color(k.r, k.g, k.b, 1f), Surface.Metal, true);
                if (m.look == Look.Metal) return Greybox.Lit(new Color(k.r * 0.72f, k.g * 0.72f, k.b * 0.72f, 1f), Surface.Metal, true);
            }
            else if (furniture.HasValue && furnitureLike)
            {
                var f = furniture.Value;
                c = new Color(f.r, f.g, f.b, 1f);
            }
            return m.look switch
            {
                Look.Wood => Greybox.Lit(c, Surface.Wood, true),
                Look.Polymer or Look.Rubber => Greybox.Lit(c, Surface.Rubber, true),
                _ => Greybox.Lit(c, Surface.Metal, true),
            };
        }

        static Dictionary<string, Info> Infos()
        {
            if (s_Info != null) return s_Info;
            s_Info = new Dictionary<string, Info>();
            var text = Resources.Load<TextAsset>("Guns/guns");
            if (text == null) return s_Info;
            foreach (var raw in text.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(' ');
                if (p.Length < 13) continue;
                float F(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
                s_Info[p[0]] = new Info
                {
                    Kind = p[1],
                    Muzzle = new Vector3(F(2), F(3), F(4)),
                    Min = new Vector3(F(5), F(6), F(7)),
                    Max = new Vector3(F(8), F(9), F(10)),
                    ScopeFront = F(11),
                    ScopeRadius = F(12),
                };
            }
            return s_Info;
        }

        static (Mesh mesh, string[] keys) MeshOf(string name)
        {
            if (s_Meshes.TryGetValue(name, out var cached)) return cached;
            (Mesh, string[]) loaded = (null, null);
            try { loaded = PropModels.LoadRaw("Guns/" + name); }
            catch (System.Exception e) { Debug.LogWarning($"Gun model {name} couldn't be loaded: {e.Message}"); }
            s_Meshes[name] = loaded;
            return loaded;
        }

        /// <summary>True when this gun has a pack model in the project.</summary>
        public static bool Has(GunData data) =>
            data != null && s_ByGun.TryGetValue(data.displayName, out var e) && Infos().ContainsKey(e.slug) && MeshOf(e.slug).mesh != null;

        /// <summary>A mesh renderer for one of the pack's meshes, painted.</summary>
        static Renderer AddMesh(Transform parent, string name, Mesh mesh, string[] keys, Color? camo, Color? furniture)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            Paint(r, keys, camo, furniture);
            return r;
        }

        static void Paint(Renderer r, string[] keys, Color? camo, Color? furniture)
        {
            var mats = new Material[keys.Length];
            for (int i = 0; i < keys.Length; i++) mats[i] = MaterialFor(keys[i], camo, furniture);
            r.sharedMaterials = mats;
        }

        /// <summary>
        /// The first-person model for a gun, or null when it has none (the caller builds the primitive gun instead).
        /// Hidden until it's held, like <see cref="GunModels.Build"/>.
        /// </summary>
        public static GunModels.Model BuildView(GunData data, Transform parent)
        {
            if (!Has(data)) return null;
            var (slug, scale, ownColours) = s_ByGun[data.displayName];
            var info = Infos()[slug];
            var (mesh, keys) = MeshOf(slug);
            var shape = GunModels.ShapeOf(data);
            var m = new GunModels.Model { Shape = shape };
            m.Root = Greybox.Empty("Gun " + data.displayName, parent, Vector3.zero).gameObject;
            // Scaled about the back of the sight, so a shortened gun still lines up when aiming.
            var model = Greybox.Empty("Model", m.Root.transform, new Vector3(0f, GunModels.SightY, PackRearZ) * (1f - scale));
            model.localScale = Vector3.one * scale;
            Color? furniture = ownColours ? data.accent : (Color?)null;
            var renderers = new List<(Renderer, string[])> { (AddMesh(model, "Body", mesh, keys, null, furniture), keys) };
            var (optic, opticKeys) = MeshOf(slug + "_optic");
            if (optic != null)
            {
                var r = AddMesh(model, "Optic", optic, opticKeys, null, furniture);
                renderers.Add((r, opticKeys));
                m.HideWhenAimed = r.gameObject;
            }
            if (info.Kind == "scope") Reticle(model, info);
            m.Repaint = camo =>
            {
                foreach (var (r, k) in renderers) Paint(r, k, camo, furniture);
            };

            m.Muzzle = Greybox.Empty("Muzzle", model, info.Muzzle + new Vector3(0f, 0f, 0.005f));
            m.EnergyFlash = shape == GunModels.Shape.Energy || shape == GunModels.Shape.Launcher;
            m.FlashScale = shape switch
            {
                GunModels.Shape.Revolver => 0.95f,
                GunModels.Shape.Smg => 0.85f,
                GunModels.Shape.Shotgun => 1.5f,
                GunModels.Shape.Sniper => 1.6f,
                GunModels.Shape.Dmr => 1.2f,
                GunModels.Shape.Lmg => 1.15f,
                GunModels.Shape.Energy => 0.9f,
                GunModels.Shape.Launcher => 1.7f,
                _ => 1f,
            };

            // Upgrade glow: strips down both sides at the bore's height, a spine along the top, a ring at the muzzle.
            float side = Mathf.Min(-info.Min.x, info.Max.x) * 0.8f + 0.003f;
            float front = info.Muzzle.z;
            GunModels.AddGlow(m, model, side, info.Muzzle.y, front * 0.45f, front * 0.5f,
                new Vector3(0f, GunModels.SightY - 0.012f, front * 0.4f), front * 0.4f,
                new Vector3(0f, info.Muzzle.y, front - 0.03f), 0.045f,
                new Vector3(0f, info.Muzzle.y - 0.06f, 0.02f), new Vector3(0.002f, 0.002f, 0.002f), 0f);

            Greybox.DisableShadows(m.Root);
            m.Root.SetActive(false);
            return m;
        }

        /// <summary>Back of the sight in the converted meshes (tools/tenweapons/pack_guns.py REAR_Z).</summary>
        const float PackRearZ = -0.08f;

        /// <summary>A scope's lens and crosshair on the sight line, left showing while the tube hides.</summary>
        static void Reticle(Transform model, Info info)
        {
            float z = info.ScopeFront, size = info.ScopeRadius * 1.8f;
            var lens = Greybox.Prim(PrimitiveType.Cylinder, "Lens", model, new Vector3(0f, GunModels.SightY, z + 0.004f), new Vector3(size, 0.0005f, size),
                new Color(0.4f, 0.6f, 0.75f, 0.16f), collider: false, unlit: true);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var dark = new Color(0.02f, 0.02f, 0.022f);
            Greybox.Box("ReticleH", model, new Vector3(0f, GunModels.SightY, z), new Vector3(size * 0.92f, 0.0006f, 0.0004f), dark, collider: false, surface: Surface.Rubber, moving: true);
            Greybox.Box("ReticleV", model, new Vector3(0f, GunModels.SightY, z), new Vector3(0.0006f, size * 0.92f, 0.0004f), dark, collider: false, surface: Surface.Rubber, moving: true);
            Greybox.Prim(PrimitiveType.Cube, "ReticleDot", model, new Vector3(0f, GunModels.SightY, z - 0.0005f), new Vector3(0.0012f, 0.0012f, 0.0004f),
                new Color(1f, 0.12f, 0.08f), collider: false, unlit: true);
        }

        /// <summary>
        /// A plain copy of a gun's model (optic included, no glow or muzzle) for a teammate's hands or a wall buy,
        /// painted with the camo if any. Null when the gun has no pack model. The model's origin is where the view
        /// model's is: just behind the sights, with the sight line <see cref="GunModels.SightY"/> above it.
        /// </summary>
        public static GameObject BuildProp(GunData data, Transform parent, Color? camo = null)
        {
            if (!Has(data)) return null;
            var (slug, scale, ownColours) = s_ByGun[data.displayName];
            var (mesh, keys) = MeshOf(slug);
            var root = Greybox.Empty("Gun " + data.displayName, parent, Vector3.zero);
            var model = Greybox.Empty("Model", root, new Vector3(0f, GunModels.SightY, PackRearZ) * (1f - scale));
            model.localScale = Vector3.one * scale;
            Color? furniture = ownColours ? data.accent : (Color?)null;
            AddMesh(model, "Body", mesh, keys, camo, furniture);
            var (optic, opticKeys) = MeshOf(slug + "_optic");
            if (optic != null) AddMesh(model, "Optic", optic, opticKeys, camo, furniture);
            return root.gameObject;
        }

        /// <summary>A gun prop's size along the barrel and its middle (in the prop's own space), for fitting it to a panel.</summary>
        public static bool Extent(GunData data, out float length, out Vector3 middle, out float halfWidth)
        {
            length = 1f;
            middle = Vector3.zero;
            halfWidth = 0.05f;
            if (!Has(data)) return false;
            var (slug, scale, _) = s_ByGun[data.displayName];
            var info = Infos()[slug];
            var pivot = new Vector3(0f, GunModels.SightY, PackRearZ);
            Vector3 lo = pivot + (info.Min - pivot) * scale, hi = pivot + (info.Max - pivot) * scale;
            length = hi.z - lo.z;
            middle = (lo + hi) * 0.5f;
            halfWidth = Mathf.Max(-lo.x, hi.x);
            return true;
        }

        /// <summary>One of the shield's meshes (the assembled shield or a part), at the pack's size, or null.</summary>
        public static GameObject BuildPiece(string name, Transform parent)
        {
            var (mesh, keys) = MeshOf(name);
            if (mesh == null) return null;
            var r = AddMesh(parent, name, mesh, keys, null, null);
            return r.gameObject;
        }

        /// <summary>The bounds of a shield mesh at the pack's size (empty when it's missing).</summary>
        public static Bounds PieceBounds(string name)
        {
            var (mesh, _) = MeshOf(name);
            return mesh != null ? mesh.bounds : new Bounds();
        }
    }
}
