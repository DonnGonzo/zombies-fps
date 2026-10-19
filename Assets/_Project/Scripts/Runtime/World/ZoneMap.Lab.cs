using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Doctor Morrow's touches on the Lantern Core lab, for the easter egg: four tesla coils round the reactor (the
    /// arcs jump to their tops), cables out to them, a crown of conduit over the reactor, his chalkboard, specimen
    /// tanks, the intercom horns he talks through, and scorch marks on the floor. The coils and tanks are solid and
    /// go in before the NavMesh bake; the rest is colliderless dressing.
    /// </summary>
    public partial class ZoneMap
    {
        const float CoilHeight = 3.3f;
        static readonly Color Copper = new Color(0.55f, 0.3f, 0.16f);
        static readonly Color CoilBase = new Color(0.16f, 0.17f, 0.19f);
        static readonly Color Slate = new Color(0.09f, 0.12f, 0.1f);
        static readonly Color Fluid = new Color(0.25f, 0.85f, 0.45f, 0.35f);

        /// <summary>Where the Lantern Core's reactor stands, on the floor at its middle.</summary>
        public static Vector3 ReactorBase => L(LabZone, 0f, 0f, 0f);

        /// <summary>The tops of the tesla coils round the reactor, where its lightning lands.</summary>
        public List<Vector3> CoilTops { get; } = new List<Vector3>();

        void DressLab()
        {
            Physics.SyncTransforms();
            var rng = new System.Random(6100);
            TeslaCoils();
            ReactorCrown();
            SpecimenTanks();
            Physics.SyncTransforms();
            Chalkboard(rng);
            Intercoms();
            foreach (var top in CoilTops)
                Splat(new Vector3(top.x, 0f, top.z) + new Vector3(Range(rng, -0.6f, 0.6f), 0f, Range(rng, -0.6f, 0.6f)), 1.9f, 1.5f, Range(rng, 0f, 180f), 0.004f, Soot, Surface.Concrete);
            for (int k = 0; k < 5; k++)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f), d = Range(rng, 2.6f, 5f);
                Splat(ReactorBase + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d), Range(rng, 0.5f, 1.1f), Range(rng, 0.4f, 0.9f), Range(rng, 0f, 180f), 0.005f, Soot, Surface.Concrete);
            }
        }

        /// <summary>Four coils at the room's diagonals, or the nearest open floor to them.</summary>
        void TeslaCoils()
        {
            CoilTops.Clear();
            var spots = new[]
            {
                new Vector2(-10.5f, -3.5f), new Vector2(10.5f, -3.5f), new Vector2(-10.5f, 3.5f), new Vector2(10.5f, 3.5f),
                new Vector2(-4f, -10f), new Vector2(4f, -10f), new Vector2(-4f, 10f), new Vector2(4f, 10f),
            };
            foreach (var s in spots)
            {
                if (CoilTops.Count >= 4) break;
                var p = L(LabZone, s.x, 0f, s.y);
                if (!FloorFree(p, 1.2f)) continue;
                // Keep clear of the reactor's cables out to the consoles.
                if (Mathf.Abs(s.x) < 1f || Mathf.Abs(s.y) < 1f) continue;
                TeslaCoil(p);
            }
        }

        void TeslaCoil(Vector3 p)
        {
            Greybox.Prim(PrimitiveType.Cylinder, "Tesla Coil Base", Root, p + Vector3.up * 0.25f, new Vector3(1.1f, 0.25f, 1.1f), CoilBase, surface: Surface.Metal);
            Greybox.Prim(PrimitiveType.Cylinder, "Tesla Coil", Root, p + Vector3.up * 1.55f, new Vector3(0.5f, 1.1f, 0.5f), Copper, surface: Surface.Metal);
            // Windings up the column, a collar, the torus and a ball on top.
            for (float y = 0.75f; y < 2.6f; y += 0.12f)
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * y, new Vector3(0.56f, 0.025f, 0.56f), Quaternion.identity, Shade(Copper, 0.8f + 0.2f * Mathf.Sin(y * 40f)), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.55f, new Vector3(0.75f, 0.05f, 0.75f), Quaternion.identity, Shade(CoilBase, 1.4f), Surface.Metal);
            DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 2.7f, new Vector3(0.3f, 0.12f, 0.3f), Quaternion.identity, CoilBase, Surface.Metal);
            DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 2.95f, new Vector3(1.25f, 0.32f, 1.25f), Quaternion.identity, new Color(0.62f, 0.64f, 0.66f), Surface.Metal);
            DecoPrim(PrimitiveType.Sphere, p + Vector3.up * CoilHeight, Vector3.one * 0.4f, Quaternion.identity, new Color(0.7f, 0.72f, 0.74f), Surface.Metal);
            // Warning stripes round the base.
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Deco(p + Vector3.up * 0.3f + dir * 0.56f, new Vector3(0.22f, 0.12f, 0.01f), Quaternion.LookRotation(dir), k % 2 == 0 ? HazardYellow : Soot, Surface.Plain);
            }
            // A cable back to the reactor.
            var toCore = ReactorBase - p;
            toCore.y = 0f;
            var dirIn = toCore.normalized;
            Rod(p + dirIn * 0.6f + Vector3.up * 0.04f, ReactorBase - dirIn * (CoreDiameter / 2f + 0.05f) + Vector3.up * 0.04f, 0.09f, new Color(0.05f, 0.05f, 0.05f), Surface.Rubber);
            CoilTops.Add(p + Vector3.up * CoilHeight);
        }

        /// <summary>Conduit from the reactor's head up into a ring on the ceiling.</summary>
        void ReactorCrown()
        {
            var top = ReactorBase + Vector3.up * (Reactor.Height - 0.25f);
            var ringY = WallHeight - 0.15f;
            const int Spokes = 8;
            const float RingRadius = 3f;
            for (int k = 0; k < Spokes; k++)
            {
                var dir = Quaternion.Euler(0f, k * 360f / Spokes + 22.5f, 0f) * Vector3.forward;
                Rod(top + dir * 0.55f, ReactorBase + dir * RingRadius + Vector3.up * ringY, 0.1f, k % 2 == 0 ? Copper : new Color(0.06f, 0.06f, 0.06f), k % 2 == 0 ? Surface.Metal : Surface.Rubber);
            }
            const int Segments = 16;
            for (int k = 0; k < Segments; k++)
            {
                var a = Quaternion.Euler(0f, k * 360f / Segments, 0f) * Vector3.forward * RingRadius;
                var b = Quaternion.Euler(0f, (k + 1) * 360f / Segments, 0f) * Vector3.forward * RingRadius;
                Rod(ReactorBase + a + Vector3.up * ringY, ReactorBase + b + Vector3.up * ringY, 0.22f, CoilBase, Surface.Metal);
            }
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, k * 90f + 45f, 0f) * Vector3.forward;
                Glow(ReactorBase + dir * RingRadius + Vector3.up * (ringY - 0.14f), new Vector3(0.3f, 0.04f, 0.3f), Quaternion.LookRotation(dir), CoreGlow);
            }
        }

        /// <summary>Glass tanks of green fluid with something floating in them, against the room's walls where there's space.</summary>
        void SpecimenTanks()
        {
            var spots = new[] { new Vector2(13.5f, -5.5f), new Vector2(-13.5f, 10f), new Vector2(-13.5f, -4.5f), new Vector2(13.5f, -8.5f) };
            int placed = 0;
            foreach (var s in spots)
            {
                if (placed >= 3) break;
                var p = L(LabZone, s.x, 0f, s.y);
                if (!FloorFree(p, 1f)) continue;
                placed++;
                Greybox.Prim(PrimitiveType.Cylinder, "Specimen Tank", Root, p + Vector3.up * 1.3f, new Vector3(1.2f, 1.3f, 1.2f), Glass, surface: Surface.Metal)
                    .GetComponent<Renderer>().enabled = false;
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 0.2f, new Vector3(1.35f, 0.2f, 1.35f), Quaternion.identity, CoilBase, Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, p + Vector3.up * 2.5f, new Vector3(1.35f, 0.1f, 1.35f), Quaternion.identity, CoilBase, Surface.Metal);
                Rod(p + Vector3.up * 2.6f, p + Vector3.up * WallHeight, 0.14f, Copper, Surface.Metal);
                // The specimen, curled up, then the fluid and glass around it.
                DecoPrim(PrimitiveType.Capsule, p + Vector3.up * 1.4f, new Vector3(0.4f, 0.5f, 0.3f), Quaternion.Euler(12f, 30f, 8f), new Color(0.32f, 0.36f, 0.3f), Surface.Skin);
                DecoPrim(PrimitiveType.Sphere, p + Vector3.up * 2f, Vector3.one * 0.28f, Quaternion.identity, new Color(0.32f, 0.36f, 0.3f), Surface.Skin);
                Glow(PrimitiveType.Cylinder, p + Vector3.up * 1.35f, new Vector3(1.1f, 1.05f, 1.1f), Quaternion.identity, Fluid);
                Glow(PrimitiveType.Cylinder, p + Vector3.up * 2.42f, new Vector3(1.1f, 0.01f, 1.1f), Quaternion.identity, new Color(0.45f, 1f, 0.6f));
                var light = new GameObject("Tank Light").AddComponent<Light>();
                light.transform.SetParent(Root, false);
                light.transform.localPosition = p + Vector3.up * 1.6f;
                light.type = LightType.Point;
                light.color = new Color(0.35f, 1f, 0.55f);
                light.range = 4.5f;
                light.intensity = 1.1f;
                light.shadows = LightShadows.None;
            }
        }

        /// <summary>Doctor Morrow's chalkboard: sums, a sketch of the Lantern and a note to himself.</summary>
        void Chalkboard(System.Random rng)
        {
            foreach (var f in _faces)
            {
                if (f.Zone != LabZone || f.Length < 5f) continue;
                for (float t = 2.2f; t < f.Length - 2.2f; t += 1f)
                {
                    if (!WallFree(f, t, 2.1f, 1.75f, 1f, 0.12f)) continue;
                    var rot = f.Facing;
                    Deco(f.At(t, 2.1f, 0.04f), new Vector3(3.4f, 1.9f, 0.06f), rot, DarkWood, Surface.Wood);
                    Deco(f.At(t, 2.1f, 0.075f), new Vector3(3.2f, 1.7f, 0.01f), rot, Slate, Surface.Plain);
                    Deco(f.At(t, 1.2f, 0.12f), new Vector3(3.3f, 0.05f, 0.1f), rot, DarkWood, Surface.Wood);
                    // Lines of scribbled working on the left.
                    for (int line = 0; line < 6; line++)
                        for (float x = -1.45f; x < -0.2f;)
                        {
                            float w = Range(rng, 0.06f, 0.24f);
                            Deco(f.At(t + x + w / 2f, 2.75f - line * 0.2f, 0.082f), new Vector3(w, 0.022f, 0.004f), rot, Chalk, Surface.Plain);
                            x += w + Range(rng, 0.04f, 0.09f);
                        }
                    // The Lantern sketched on the right: a tall drum with sparks round it.
                    Deco(f.At(t + 0.95f, 2.15f, 0.082f), new Vector3(0.025f, 0.9f, 0.004f), rot, Chalk, Surface.Plain);
                    Deco(f.At(t + 1.35f, 2.15f, 0.082f), new Vector3(0.025f, 0.9f, 0.004f), rot, Chalk, Surface.Plain);
                    Deco(f.At(t + 1.15f, 2.6f, 0.082f), new Vector3(0.42f, 0.025f, 0.004f), rot, Chalk, Surface.Plain);
                    Deco(f.At(t + 1.15f, 1.7f, 0.082f), new Vector3(0.42f, 0.025f, 0.004f), rot, Chalk, Surface.Plain);
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * 60f + 15f;
                        var spark = Quaternion.AngleAxis(a, f.Inward) * Vector3.up;
                        var at = f.At(t + 1.15f, 2.15f, 0.082f) + spark * 0.62f;
                        Deco(at, new Vector3(0.022f, 0.16f, 0.004f), Quaternion.LookRotation(f.Inward, spark), Chalk, Surface.Plain);
                    }
                    var holder = Greybox.Empty("Chalk", Root, f.At(t - 0.8f, 1.5f, 0.085f));
                    holder.localRotation = rot;
                    Greybox.Label("Text", holder, Vector3.zero, "MORS -> LUX ?\nfeed it. wake it. OPEN IT.", Shade(Chalk, 0.9f), 0.13f);
                    var title = Greybox.Empty("Chalk", Root, f.At(t, 2.88f, 0.085f));
                    title.localRotation = rot;
                    Greybox.Label("Text", title, new Vector3(0.6f, 0f, 0f), "PRIMA CAUSA  - I.M.", Chalk, 0.15f);
                    return;
                }
            }
        }

        /// <summary>Brass intercom horns high on the walls: Doctor Morrow's voice comes from these.</summary>
        void Intercoms()
        {
            int placed = 0;
            var brass = new Color(0.6f, 0.45f, 0.2f);
            for (int i = 0; i < _faces.Count && placed < 4; i++)
            {
                var f = _faces[i];
                if (f.Zone != LabZone || f.Length < 6f) continue;
                float t = f.Length / 2f + (placed % 2 == 0 ? -1.5f : 1.5f);
                if (!WallFree(f, t, 4.6f, 0.45f, 0.45f, 0.6f)) continue;
                placed++;
                var rot = f.Facing;
                Deco(f.At(t, 4.6f, 0.06f), new Vector3(0.5f, 0.5f, 0.12f), rot, CoilBase, Surface.Metal);
                // The horn tips down toward the room.
                var horn = rot * Quaternion.Euler(25f, 0f, 0f);
                DecoPrim(PrimitiveType.Cylinder, f.At(t, 4.45f, 0.35f), new Vector3(0.18f, 0.22f, 0.18f), horn * Quaternion.Euler(90f, 0f, 0f), brass, Surface.Metal);
                DecoPrim(PrimitiveType.Cylinder, f.At(t, 4.35f, 0.58f), new Vector3(0.48f, 0.03f, 0.48f), horn * Quaternion.Euler(90f, 0f, 0f), brass, Surface.Metal);
                Glow(f.At(t + 0.17f, 4.78f, 0.13f), new Vector3(0.05f, 0.05f, 0.01f), rot, new Color(1f, 0.2f, 0.15f));
                Rod(f.At(t, 4.85f, 0.04f), f.At(t, WallHeight, 0.04f), 0.05f, new Color(0.05f, 0.05f, 0.05f), Surface.Rubber);
            }
        }
    }
}
