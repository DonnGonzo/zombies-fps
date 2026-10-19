using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Medieval props from the scanned CC0 set (see <see cref="PropModels"/>): barrels, crates, tables with
    /// lanterns, buckets and baskets against the walls, fire pits in the yards and shields hung in the halls.
    ///
    /// Where they stand and the colliders they get never depend on whether the models are there or turned off in
    /// Settings, so every machine has the same solid map (and NavMesh); without the models, plain wooden stand-ins
    /// fill the same spots. The models themselves are merged into the decor batch like the rest of the dressing.
    /// </summary>
    public partial class ZoneMap
    {
        enum PropGroup { Barrels, Crates, Table, Buckets }

        /// <summary>How often each zone gets each group of props, by weight, in PropGroup order; and how busy its walls are.</summary>
        static readonly (int[] mix, float chance)[] PropMix =
        {
            (new[] { 3, 2, 0, 1 }, 0.28f), // Gate Yard
            (new[] { 3, 4, 1, 1 }, 0.4f),  // Depot
            (new[] { 2, 3, 1, 1 }, 0.3f),  // Garage
            (new[] { 1, 0, 3, 1 }, 0.25f), // Chapel
            (new[] { 1, 1, 3, 1 }, 0.35f), // Barracks
            (new[] { 2, 1, 0, 2 }, 0.25f), // Courtyard
            (new[] { 3, 4, 0, 1 }, 0.4f),  // Loading Dock
            (new[] { 2, 1, 0, 3 }, 0.3f),  // Pump Station
            (new[] { 3, 3, 0, 1 }, 0.3f),  // Crane Yard
            (new[] { 1, 2, 1, 0 }, 0.15f), // Lantern Core
            (new[] { 1, 2, 3, 1 }, 0.35f), // Archives
            (new[] { 2, 1, 1, 3 }, 0.3f),  // Greenhouse
            (new[] { 2, 1, 0, 3 }, 0.15f), // Gaol
            (new[] { 1, 2, 2, 3 }, 0.2f),  // Plague Ward
            (new[] { 0, 1, 3, 0 }, 0.15f), // Scriptorium
            (new[] { 3, 3, 1, 1 }, 0.3f),  // Alchemist's Furnace
            (new[] { 3, 2, 0, 1 }, 0.3f),  // Rampart Ruins
            (new[] { 1, 0, 1, 2 }, 0.15f), // Sunken Temple
        };

        /// <summary>Zones whose walls get a shield or two hung up.</summary>
        static readonly int[] ShieldZones = { 0, 1, 3, 4, 10, 12, 16 };
        /// <summary>Open-air zones that get a fire pit.</summary>
        static readonly int[] FirePitZones = { 0, 5, 8, 16 };

        static readonly Color PropWood = new Color(0.36f, 0.25f, 0.15f);
        static readonly Color Ember = new Color(1f, 0.45f, 0.12f);

        readonly List<Vector3> _propSpots = new List<Vector3>();

        void DressProps()
        {
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Length < 3f) continue;
                var rng = new System.Random(6100 + i * 6007);
                var (mix, chance) = PropMix[f.Zone];
                int total = 0;
                foreach (int w in mix) total += w;
                for (float t = 1.5f; t < f.Length - 1.5f; t += Range(rng, 2.2f, 3.4f))
                {
                    if (rng.NextDouble() > chance) continue;
                    int roll = rng.Next(total), k = 0;
                    while (roll >= mix[k]) roll -= mix[k++];
                    TryGroup((PropGroup)k, f, t, rng);
                }
                if (System.Array.IndexOf(ShieldZones, f.Zone) >= 0) Shields(f, rng);
            }
            foreach (int zone in FirePitZones) FirePitIn(zone);
            // Later dressing checks for solid things with physics queries; these colliders are new this frame.
            Physics.SyncTransforms();
        }

        void TryGroup(PropGroup group, WallFace f, float t, System.Random rng)
        {
            float halfWidth, depth, height;
            switch (group)
            {
                case PropGroup.Barrels: halfWidth = 1.25f; depth = 0.8f; height = 0.9f; break;
                case PropGroup.Crates: halfWidth = 1.1f; depth = 0.7f; height = 0.85f; break;
                case PropGroup.Table: halfWidth = 1.05f; depth = 0.75f; height = 1.1f; break;
                default: halfWidth = 0.7f; depth = 0.5f; height = 0.6f; break;
            }
            var mid = f.At(t, 0f, depth / 2f);
            foreach (var s in _propSpots)
                if ((s - mid).sqrMagnitude < 3.5f * 3.5f) return;
            if (!WallFree(f, t, height / 2f + 0.06f, halfWidth, height / 2f, depth)) return;
            // Leave a clear lane in front, so a prop never pinches a corridor.
            if (!FloorFree(f.At(t, 0f, depth + 1.1f), 0.6f)) return;
            _propSpots.Add(mid);

            var along = f.Along;
            var facing = Quaternion.LookRotation(f.Inward, Vector3.up);
            switch (group)
            {
                case PropGroup.Barrels:
                {
                    int n = rng.Next(1, 4);
                    for (int i = 0; i < n; i++)
                    {
                        var p = f.At(t + (i - (n - 1) / 2f) * 0.8f, 0f, 0.42f);
                        Prop(PropModels.Barrel, p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(0.74f, 0.87f, 0.74f), true);
                    }
                    break;
                }
                case PropGroup.Crates:
                {
                    // A long crate along the wall, a small chest on it or beside it.
                    var along90 = Quaternion.LookRotation(along, Vector3.up);
                    var p = f.At(t - 0.25f, 0f, 0.33f);
                    Prop(PropModels.LongCrate, p, along90, new Vector3(0.53f, 0.46f, 1.17f), true);
                    var chestRot = facing * Quaternion.Euler(0f, Range(rng, -20f, 20f), 0f);
                    if (rng.NextDouble() < 0.6) Prop(PropModels.Chest, p + Vector3.up * 0.46f, chestRot, new Vector3(0.83f, 0.35f, 0.41f), false);
                    else Prop(PropModels.Chest, f.At(t + 0.75f, 0f, 0.3f), chestRot * Quaternion.Euler(0f, 90f, 0f), new Vector3(0.83f, 0.35f, 0.41f), true);
                    break;
                }
                case PropGroup.Table:
                {
                    // A low trestle table with a lantern and a bucket or basket on it.
                    var p = f.At(t, 0f, 0.38f);
                    Prop(PropModels.Table, p, facing, new Vector3(1.8f, 0.55f, 0.66f), true);
                    var top = p + Vector3.up * 0.55f;
                    LanternOn(top + along * Range(rng, 0.45f, 0.65f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), true);
                    var other = top - along * Range(rng, 0.35f, 0.6f);
                    if (rng.NextDouble() < 0.5) Prop(PropModels.Bucket, other, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(0.36f, 0.35f, 0.36f), false);
                    else Prop(PropModels.Basket, other, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(0.24f, 0.2f, 0.22f), false);
                    break;
                }
                default:
                {
                    int n = rng.Next(1, 3);
                    for (int i = 0; i < n; i++)
                        Prop(PropModels.Bucket, f.At(t - 0.3f + i * 0.45f, 0f, 0.25f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(0.36f, 0.35f, 0.36f), true);
                    Prop(PropModels.Basket, f.At(t + 0.45f, 0f, 0.22f), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(0.24f, 0.2f, 0.22f), false);
                    break;
                }
            }
        }

        /// <summary>A shield or two hung on a hall wall above head height.</summary>
        void Shields(WallFace f, System.Random rng)
        {
            var model = PropModels.Get(PropModels.Shield);
            for (float t = 2f; t < f.Length - 2f; t += Range(rng, 4f, 7f))
            {
                if (rng.NextDouble() > 0.3 || !WallFree(f, t, 2.4f, 0.32f, 0.75f, 0.16f)) continue;
                if (model == null) continue;
                var rot = Quaternion.LookRotation(f.Inward, Vector3.up) * Quaternion.Euler(0f, 0f, Range(rng, -6f, 6f));
                AddModel(model, f.At(t, 2.4f, 0.06f), rot, false);
            }
        }

        /// <summary>A stone fire pit with glowing embers on open ground near the middle of a yard.</summary>
        void FirePitIn(int zone)
        {
            var rng = new System.Random(9200 + zone * 31);
            var rects = ZoneRects(zone);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                var r = rects[rng.Next(rects.Length)];
                if (r.width < 6 || r.height < 6) continue;
                var p = new Vector3(Range(rng, r.xMin + 2.5f, r.xMax - 2.5f), 0f, Range(rng, r.yMin + 2.5f, r.yMax - 2.5f));
                if (!FloorFree(p, 1.6f)) continue;
                Prop(PropModels.FirePit, p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(1.45f, 0.39f, 1.45f), true);
                for (int i = 0; i < 5; i++)
                {
                    var e = p + new Vector3(Range(rng, -0.35f, 0.35f), 0.08f + i * 0.015f, Range(rng, -0.35f, 0.35f));
                    Glow(PrimitiveType.Sphere, e, new Vector3(Range(rng, 0.2f, 0.35f), 0.06f, Range(rng, 0.2f, 0.35f)), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Ember);
                }
                _propSpots.Add(p);
                return;
            }
        }

        /// <summary>
        /// A prop standing on the floor (or on another prop) at p, its footprint centred there. Solid props get a
        /// box collider the size given, model or not; without the model a plain wooden box or drum stands in.
        /// </summary>
        void Prop(string slug, Vector3 p, Quaternion rot, Vector3 size, bool solid)
        {
            if (solid)
            {
                var go = new GameObject("Prop " + slug);
                go.transform.SetParent(Root, false);
                go.transform.localPosition = p + Vector3.up * size.y / 2f;
                go.transform.localRotation = rot;
                go.isStatic = true;
                go.AddComponent<BoxCollider>().size = size;
            }
            var model = PropModels.Get(slug);
            if (model != null)
            {
                AddModel(model, p, rot, true);
                return;
            }
            if (slug == PropModels.Basket) return;
            bool round = slug == PropModels.Barrel || slug == PropModels.Bucket || slug == PropModels.FirePit;
            var center = p + Vector3.up * size.y / 2f;
            if (slug == PropModels.FirePit)
                DecoPrim(PrimitiveType.Cylinder, center, new Vector3(size.x, size.y / 2f, size.z), rot, new Color(0.34f, 0.32f, 0.3f), Surface.Concrete);
            else if (round)
                DecoPrim(PrimitiveType.Cylinder, center, new Vector3(size.x, size.y / 2f, size.z), rot, PropWood, Surface.Wood);
            else
                Deco(center, size, rot, PropWood, Surface.Wood);
        }

        /// <summary>A wooden lantern standing at p, its glass glowing if lit.</summary>
        void LanternOn(Vector3 p, Quaternion rot, bool lit, float scale = 1f)
        {
            var model = PropModels.Get(PropModels.Lantern);
            if (model == null)
            {
                Deco(p + Vector3.up * 0.18f * scale, new Vector3(0.2f, 0.36f, 0.2f) * scale, rot, PropWood, Surface.Wood);
                if (lit) Glow(p + Vector3.up * 0.18f * scale, new Vector3(0.21f, 0.2f, 0.21f) * scale, rot, WallLampGlow);
                return;
            }
            AddModel(model, p, rot, false, scale, lit);
        }

        /// <summary>The model's base centred on p. Glass glows unless unlit is asked for.</summary>
        void AddModel(PropModels.Model model, Vector3 p, Quaternion rot, bool shadows, float scale = 1f, bool lit = true)
        {
            var b = model.Bounds;
            var offset = new Vector3(b.center.x, b.min.y, b.center.z) * scale;
            var m = Matrix4x4.TRS(p - rot * offset, rot, Vector3.one * scale);
            for (int s = 0; s < model.Materials.Length; s++)
            {
                var mat = model.Materials[s];
                if (!lit && model.Keys[s].EndsWith("_glass")) mat = Greybox.Lit(Glass, Surface.Plain);
                _decor.Add(model.Mesh, s, m, mat, shadows);
            }
        }
    
        /// <summary>A lantern hanging from an iron bracket on a wall, in place of the old bulkhead lamp.</summary>
        void WallLantern(Vector3 at, Vector3 inward, bool lit)
        {
            var iron = new Color(0.12f, 0.11f, 0.1f);
            var root = at + Vector3.up * 0.35f;
            var tip = root + inward * 0.45f;
            Bar(root, tip, 0.035f, 0.035f, Vector3.up, iron, Surface.Metal);
            Bar(at - Vector3.up * 0.1f + inward * 0.01f, root + inward * 0.3f, 0.025f, 0.025f, Vector3.Cross(inward, Vector3.up), iron, Surface.Metal);
            Deco(at + Vector3.up * 0.12f + inward * 0.01f, new Vector3(0.12f, 0.56f, 0.02f), Quaternion.LookRotation(inward, Vector3.up), iron, Surface.Metal);
            Rod(tip, tip - Vector3.up * 0.1f, 0.012f, iron, Surface.Metal);
            LanternOn(tip - Vector3.up * 0.62f, Quaternion.LookRotation(inward, Vector3.up), lit);
        }

        /// <summary>A big lantern hanging on a chain from the beam, in place of the old enamelled shade.</summary>
        void CeilingLantern(Vector3 position)
        {
            Rod(position + Vector3.up * 0.28f, position - Vector3.up * 0.06f, 0.025f, new Color(0.12f, 0.11f, 0.1f), Surface.Metal);
            LanternOn(position - Vector3.up * (0.06f + 0.52f * 1.5f), Quaternion.Euler(0f, 45f, 0f), true, 1.5f);
        }
    }
}
