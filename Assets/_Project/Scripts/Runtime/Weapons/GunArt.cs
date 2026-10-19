using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Gun and shield models in the style of Ivan's TenWeapons pack, rebuilt by tools/tenweapons/build_guns.py so
    /// every part sits flush. Each gun lives in Resources/Guns as slug.bytes (a PRP1 mesh already scaled for the
    /// view model with its sight line on <see cref="GunModels.SightY"/>) plus one slug_group.bytes per swappable
    /// part: its iron sights or built-in scope, standard magazine, and a fitted model of every shop attachment
    /// (red dot, scope, extended and fast mags, foregrip, suppressor, heavy barrel, hollow-core band, auto sear).
    /// Bought attachments show on the gun. guns.txt holds each gun's muzzle, bounds and attachment lengths.
    /// Submeshes are keyed by material name and drawn with the game's own lit, glow and glass materials. When a
    /// file is missing the game falls back to the primitive guns in <see cref="GunModels"/>.
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
            { "Brand of Azhrakel", ("helix", 1f, false) },
        };

        public const string Shield = "shield";
        /// <summary>The Aegis Shield's four build parts, in part order.</summary>
        public static readonly string[] ShieldParts = { "shield_plate", "shield_view", "shield_core", "shield_brace" };

        sealed class Info
        {
            public bool BuiltinScope;
            public Vector3 Muzzle, Min, Max;
            /// <summary>How far a suppressor reaches past the muzzle, and how far a heavy barrel moves the muzzle.</summary>
            public float SuppressorLength, HeavyLength;
            public string[] Groups;
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
            { "SightGlow", (new Color32(90, 255, 120, 255), Look.Glow) },
            { "EmissiveRed", (new Color32(255, 40, 30, 255), Look.Glow) },
            { "OpticGlass", (new Color32(150, 200, 220, 38), Look.Glass) },
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
                case Look.Glass: return Greybox.Unlit(new Color(c.r, c.g, c.b, m.color.a < 100 ? m.color.a / 255f : 0.45f));
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
                var groups = new string[p.Length - 13];
                System.Array.Copy(p, 13, groups, 0, groups.Length);
                s_Info[p[0]] = new Info
                {
                    BuiltinScope = p[1] == "scope",
                    Muzzle = new Vector3(F(2), F(3), F(4)),
                    Min = new Vector3(F(5), F(6), F(7)),
                    Max = new Vector3(F(8), F(9), F(10)),
                    SuppressorLength = F(11),
                    HeavyLength = F(12),
                    Groups = groups,
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

        /// <summary>The model part that shows an attachment, or null when it has none.</summary>
        static string GroupFor(Attachment a)
        {
            if (a == null) return null;
            switch (a.slot)
            {
                case AttachmentSlot.Optic: return a.name.Contains("Scope") ? "scope" : "reddot";
                case AttachmentSlot.Magazine: return a.name.Contains("Fast") ? "fastmag" : "extmag";
                case AttachmentSlot.Underbarrel: return "grip";
                case AttachmentSlot.Muzzle: return "supp";
                case AttachmentSlot.Barrel: return "heavy";
                case AttachmentSlot.Ammo: return "ammo";
                case AttachmentSlot.Trigger: return "auto";
                default: return null;
            }
        }

        /// <summary>The parts showing for an attachment mask (bits are shop attachment ids).</summary>
        static HashSet<string> Fitted(int mask)
        {
            var set = new HashSet<string>();
            var shop = GameConfig.Shop;
            if (shop == null) return set;
            for (int id = 0; id < shop.Count; id++)
            {
                if (!ShopData.Has(mask, id)) continue;
                var g = GroupFor(shop.At(id));
                if (g != null) set.Add(g);
            }
            return set;
        }

        /// <summary>A built gun: its model transform and a renderer per part, and how to fit attachments.</summary>
        sealed class Built
        {
            public Transform Root, Model;
            public Info Info;
            public readonly Dictionary<string, Renderer> Parts = new Dictionary<string, Renderer>();
            public readonly List<(Renderer r, string[] keys)> Renderers = new List<(Renderer, string[])>();
            public Color? Furniture;
            public Transform Muzzle;
            public GameObject ScopeShowing;
            public bool Scoped;

            /// <summary>
            /// Shows the parts for an attachment mask: an optic replaces the iron sights (or a sniper's own scope),
            /// a bought magazine replaces the standard one, and a suppressor screws onto the end of a heavy barrel.
            /// The muzzle (and its flash) moves to the end of whatever's fitted.
            /// </summary>
            public void Fit(int mask)
            {
                var on = Fitted(mask);
                bool builtin = Parts.ContainsKey("builtin");
                bool scope = on.Remove("scope"), dot = on.Remove("reddot");
                on.Add(scope ? (builtin ? "builtin" : "scope") : dot ? "reddot" : builtin ? "builtin" : "irons");
                if (!on.Contains("extmag") && !on.Contains("fastmag")) on.Add("mag");
                else if (on.Contains("extmag")) on.Remove("fastmag");
                foreach (var kv in Parts) kv.Value.gameObject.SetActive(on.Contains(kv.Key));

                Scoped = on.Contains("scope") || on.Contains("builtin");
                ScopeShowing = on.Contains("scope") && Parts.TryGetValue("scope", out var sc) ? sc.gameObject
                    : on.Contains("builtin") && Parts.TryGetValue("builtin", out var b) ? b.gameObject : null;
                bool heavy = on.Contains("heavy"), supp = on.Contains("supp");
                if (Parts.TryGetValue("supp", out var s)) s.transform.localPosition = new Vector3(0f, 0f, heavy ? Info.HeavyLength : 0f);
                if (Muzzle != null)
                    Muzzle.localPosition = Info.Muzzle + new Vector3(0f, 0f, 0.005f + (heavy ? Info.HeavyLength : 0f) + (supp ? Info.SuppressorLength : 0f));
            }

            public void Paint(Color? camo)
            {
                foreach (var (r, k) in Renderers) GunArt.Paint(r, k, camo, Furniture);
            }
        }

        static Built Assemble(GunData data, Transform parent, Color? camo)
        {
            var (slug, scale, ownColours) = s_ByGun[data.displayName];
            var b = new Built { Info = Infos()[slug], Furniture = ownColours ? data.accent : (Color?)null };
            b.Root = Greybox.Empty("Gun " + data.displayName, parent, Vector3.zero);
            // Scaled about the back of the sight, so a shortened gun still lines up when aiming.
            b.Model = Greybox.Empty("Model", b.Root, new Vector3(0f, GunModels.SightY, PackRearZ) * (1f - scale));
            b.Model.localScale = Vector3.one * scale;
            var (mesh, keys) = MeshOf(slug);
            b.Renderers.Add((AddMesh(b.Model, "Body", mesh, keys, camo, b.Furniture), keys));
            foreach (var g in b.Info.Groups)
            {
                var (gm, gk) = MeshOf(slug + "_" + g);
                if (gm == null) continue;
                var r = AddMesh(b.Model, g, gm, gk, camo, b.Furniture);
                b.Parts[g] = r;
                b.Renderers.Add((r, gk));
            }
            return b;
        }

        /// <summary>
        /// The first-person model for a gun, or null when it has none (the caller builds the primitive gun instead).
        /// Hidden until it's held, like <see cref="GunModels.Build"/>. Its Fit shows the attachments bought for it.
        /// </summary>
        public static GunModels.Model BuildView(GunData data, Transform parent)
        {
            if (!Has(data)) return null;
            var b = Assemble(data, parent, null);
            var info = b.Info;
            var model = b.Model;
            var shape = GunModels.ShapeOf(data);
            var m = new GunModels.Model { Shape = shape, Root = b.Root.gameObject };
            m.Repaint = b.Paint;
            m.Muzzle = b.Muzzle = Greybox.Empty("Muzzle", model, info.Muzzle + new Vector3(0f, 0f, 0.005f));
            m.Fit = mask =>
            {
                b.Fit(mask);
                m.Scoped = b.Scoped;
                // A scope's tube would fill the view when looking through it; the HUD draws the scope view instead.
                m.HideWhenAimed = b.ScopeShowing;
            };
            m.Fit(0);
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
                new Vector3(0f, GunModels.SightY - 0.034f, front * 0.4f), front * 0.4f,
                new Vector3(0f, info.Muzzle.y, front - 0.03f), 0.045f,
                new Vector3(0f, info.Muzzle.y - 0.06f, 0.02f), new Vector3(0.002f, 0.002f, 0.002f), 0f);

            Greybox.DisableShadows(m.Root);
            m.Root.SetActive(false);
            return m;
        }

        /// <summary>Back of the sight in the built meshes (tools/tenweapons/build_guns.py REAR_Z).</summary>
        const float PackRearZ = -0.08f;

        /// <summary>
        /// A plain copy of a gun's model (no glow or muzzle) for a teammate's hands or a wall buy, painted with the
        /// camo if any and showing the attachments in the mask. Null when the gun has no model. The model's origin is
        /// where the view model's is: just behind the sights, with the sight line <see cref="GunModels.SightY"/> above it.
        /// </summary>
        public static GameObject BuildProp(GunData data, Transform parent, Color? camo = null, int attachments = 0)
        {
            if (!Has(data)) return null;
            var b = Assemble(data, parent, camo);
            b.Fit(attachments);
            return b.Root.gameObject;
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
