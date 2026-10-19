using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Builds the first-person model for each kind of gun from primitives: gunmetal receivers and barrels,
    /// polymer or wooden furniture, sights, magazines and the small parts (rails, bolts, ejection ports) that
    /// make it read as a real gun. The kind comes from the gun's data, so new guns get a sensible look.
    /// Every model keeps its sights' tops at <see cref="SightY"/> so aiming lines them up with the screen centre.
    /// </summary>
    public static class GunModels
    {
        /// <summary>Height of the sight line in model space (WeaponView's aim pose puts it on the camera's axis).</summary>
        public const float SightY = 0.071f;

        public enum Shape
        {
            Rifle,
            Smg,
            Revolver,
            Shotgun,
            Dmr,
            Sniper,
            Lmg,
            Energy,
            Launcher,
        }

        /// <summary>Which painted or fixed material a part gets.</summary>
        enum Part
        {
            /// <summary>Receiver and other main metal shells; tinted by camos.</summary>
            Body,
            /// <summary>Grips, stocks and handguards in the gun's accent colour; tinted by camos.</summary>
            Furniture,
            /// <summary>Glows in the gun's accent colour (energy cells).</summary>
            Core,
            Steel,
            Blued,
            Bright,
            Polymer,
            Dark,
            Brass,
            Shell,
            Glass,
            Dot,
            Reticle,
        }

        public sealed class Model
        {
            public GameObject Root;
            public Transform Muzzle;
            public Shape Shape;
            public float FlashScale = 1f;
            public bool EnergyFlash;
            public Color BodyColor = SteelColor;
            public Surface BodySurface = Surface.Metal;
            public Surface FurnitureSurface = Surface.Rubber;
            public readonly List<Renderer> Body = new List<Renderer>();
            public readonly List<Renderer> Furniture = new List<Renderer>();
            public readonly List<Renderer> Cores = new List<Renderer>();
            public GameObject Glow;
            public Renderer[] GlowParts;
            public Light GlowLight;
            /// <summary>Parts hidden while aiming (a scope's tube), or null.</summary>
            public GameObject HideWhenAimed;
            /// <summary>Paints a pack model (see <see cref="GunArt"/>) with a camo, or its own colours for null. Null for primitive guns.</summary>
            public System.Action<Color?> Repaint;
            /// <summary>Shows the attachments in a mask on a pack model (see <see cref="GunArt"/>). Null for primitive guns.</summary>
            public System.Action<int> Fit;
            /// <summary>True when the fitted sight is a scope: the HUD draws the scope view while aiming.</summary>
            public bool Scoped;

            /// <summary>The furniture's colour for a gun's accent. Bright accents (energy, scrap) are toned down.</summary>
            public Color FurnitureColor(Color accent)
            {
                var c = Shape == Shape.Energy || Shape == Shape.Launcher ? Color.Lerp(accent, new Color(0.17f, 0.18f, 0.19f), 0.6f) : accent;
                c.a = 1f;
                return c;
            }
        }

        static readonly Color SteelColor = new Color(0.2f, 0.21f, 0.22f);

        public static Shape ShapeOf(GunData d)
        {
            if (d.pellets > 1) return Shape.Shotgun;
            if (d.explosiveRounds) return d.automatic ? Shape.Energy : Shape.Launcher;
            if (d.magazineSize >= 60) return Shape.Lmg;
            if (d.modelLength < 0.7f) return Shape.Revolver;
            if (d.automatic && d.modelLength < 0.95f) return Shape.Smg;
            if (!d.automatic && d.modelLength >= 1.4f) return Shape.Sniper;
            if (!d.automatic && d.modelLength >= 1.15f) return Shape.Dmr;
            return Shape.Rifle;
        }

        /// <summary>Brownish, muted accents are wood; everything else is polymer.</summary>
        static bool IsWoody(Color c) => c.r > c.g && c.g > c.b && c.r - c.b > 0.12f && c.r < 0.6f;

        /// <summary>Builds a gun's model under the view-model root, hidden until it's held: the pack model when there is one.</summary>
        public static Model Build(GunData data, Transform parent)
        {
            var art = GunArt.BuildView(data, parent);
            if (art != null) return art;
            var m = new Model { Shape = ShapeOf(data) };
            m.Root = Greybox.Empty("Gun " + data.displayName, parent, Vector3.zero).gameObject;
            m.FurnitureSurface = IsWoody(data.accent) ? Surface.Wood : Surface.Rubber;
            var b = new Builder(m);
            float length = Mathf.Clamp(data.modelLength, 0.3f, 2f);
            switch (m.Shape)
            {
                case Shape.Smg: Smg(b); break;
                case Shape.Revolver: Revolver(b); break;
                case Shape.Shotgun: Shotgun(b, length); break;
                case Shape.Sniper: Sniper(b, length); break;
                case Shape.Energy: Energy(b, length); break;
                case Shape.Launcher: Launcher(b, length); break;
                default: Rifle(b, length, m.Shape); break;
            }
            m.Root.SetActive(false);
            return m;
        }

        // ---------------------------------------------------------------- building blocks

        sealed class Builder
        {
            public readonly Model M;
            public readonly Transform Root;

            public Builder(Model m)
            {
                M = m;
                Root = m.Root.transform;
            }

            public GameObject Add(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Part part, Quaternion rot, Transform parent = null)
            {
                var go = Greybox.Prim(type, name, parent != null ? parent : Root, pos, scale, Color.white, collider: false);
                go.transform.localRotation = rot;
                var r = go.GetComponent<Renderer>();
                switch (part)
                {
                    case Part.Body: M.Body.Add(r); break;
                    case Part.Furniture: M.Furniture.Add(r); break;
                    case Part.Core: M.Cores.Add(r); break;
                    default: r.sharedMaterial = MaterialOf(part); break;
                }
                return go;
            }

            /// <summary>A box; pitch tilts it about x (positive swings its bottom backwards).</summary>
            public GameObject Box(string name, Vector3 pos, Vector3 size, Part part, float pitch = 0f, Transform parent = null)
                => Add(PrimitiveType.Cube, name, pos, size, part, Quaternion.Euler(pitch, 0f, 0f), parent);

            /// <summary>A cylinder lying along the barrel.</summary>
            public GameObject Tube(string name, Vector3 center, float diameter, float length, Part part, Transform parent = null)
                => Add(PrimitiveType.Cylinder, name, center, new Vector3(diameter, length * 0.5f, diameter), part, Quaternion.Euler(90f, 0f, 0f), parent);

            public GameObject Ball(string name, Vector3 pos, float diameter, Part part, Transform parent = null)
                => Add(PrimitiveType.Sphere, name, pos, Vector3.one * diameter, part, Quaternion.identity, parent);

            /// <summary>A hollow eight-sided tube along z around (0, y): scope bodies and rings.</summary>
            public void Octagon(string name, float y, float z, float radius, float length, float wall, Part part, Transform parent = null)
            {
                float chord = 2f * radius * Mathf.Tan(22.5f * Mathf.Deg2Rad) * 1.04f;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    float rad = a * Mathf.Deg2Rad;
                    Add(PrimitiveType.Cube, name, new Vector3(Mathf.Cos(rad) * radius, y + Mathf.Sin(rad) * radius, z),
                        new Vector3(wall, chord, length), part, Quaternion.Euler(0f, 0f, a), parent);
                }
            }
        }

        static Material MaterialOf(Part part) => part switch
        {
            Part.Steel => Greybox.Lit(SteelColor, Surface.Metal, true),
            Part.Blued => Greybox.Lit(new Color(0.1f, 0.105f, 0.115f), Surface.Metal, true),
            Part.Bright => Greybox.Lit(new Color(0.48f, 0.48f, 0.5f), Surface.Metal, true),
            Part.Polymer => Greybox.Lit(new Color(0.085f, 0.085f, 0.09f), Surface.Rubber, true),
            Part.Dark => Greybox.Lit(new Color(0.02f, 0.02f, 0.022f), Surface.Rubber, true),
            Part.Brass => Greybox.Lit(new Color(0.72f, 0.53f, 0.22f), Surface.Metal, true),
            Part.Shell => Greybox.Lit(new Color(0.55f, 0.07f, 0.05f), Surface.Rubber, true),
            Part.Glass => Greybox.Unlit(new Color(0.4f, 0.6f, 0.75f, 0.16f)),
            Part.Dot => Greybox.Unlit(new Color(1f, 0.45f, 0.1f)),
            Part.Reticle => Greybox.Unlit(new Color(1f, 0.12f, 0.08f)),
            _ => Greybox.Lit(SteelColor, Surface.Metal, true),
        };

        /// <summary>
        /// Notch rear sight and a hooded front post, both topping out on the sight line, with a glowing dot on the
        /// post. Bases fill down to the given heights.
        /// </summary>
        static void IronSights(Builder b, float rearZ, float rearBaseY, float frontZ, float frontBaseY, bool hood = true)
        {
            float earBottom = SightY - 0.026f;
            if (rearBaseY < earBottom - 0.001f)
                b.Box("RearSightBase", new Vector3(0f, (rearBaseY + earBottom) * 0.5f, rearZ), new Vector3(0.032f, earBottom - rearBaseY, 0.02f), Part.Blued);
            b.Box("RearSightL", new Vector3(-0.012f, SightY - 0.013f, rearZ), new Vector3(0.01f, 0.026f, 0.015f), Part.Blued);
            b.Box("RearSightR", new Vector3(0.012f, SightY - 0.013f, rearZ), new Vector3(0.01f, 0.026f, 0.015f), Part.Blued);
            float postHeight = SightY - frontBaseY;
            b.Box("FrontSight", new Vector3(0f, frontBaseY + postHeight * 0.5f, frontZ), new Vector3(0.005f, postHeight, 0.006f), Part.Blued);
            if (hood && postHeight > 0.008f)
            {
                b.Box("FrontHoodL", new Vector3(-0.011f, frontBaseY + postHeight * 0.45f, frontZ), new Vector3(0.004f, postHeight * 0.9f, 0.012f), Part.Blued);
                b.Box("FrontHoodR", new Vector3(0.011f, frontBaseY + postHeight * 0.45f, frontZ), new Vector3(0.004f, postHeight * 0.9f, 0.012f), Part.Blued);
            }
            b.Box("FrontSightDot", new Vector3(0f, SightY - 0.002f, frontZ - 0.0035f), new Vector3(0.006f, 0.006f, 0.002f), Part.Dot);
        }

        /// <summary>A picatinny rail: a strip with cross slots.</summary>
        static void Rail(Builder b, float y, float z, float length, float width = 0.022f)
        {
            b.Box("Rail", new Vector3(0f, y, z), new Vector3(width, 0.008f, length), Part.Blued);
            int slots = Mathf.Max(2, Mathf.RoundToInt(length / 0.03f));
            for (int i = 0; i < slots; i++)
            {
                float sz = z - length * 0.5f + (i + 0.5f) * length / slots;
                b.Box("RailSlot", new Vector3(0f, y + 0.0038f, sz), new Vector3(width + 0.001f, 0.0015f, 0.006f), Part.Dark);
            }
        }

        static void TriggerGroup(Builder b, float y, float z)
        {
            b.Box("TriggerGuard", new Vector3(0f, y - 0.018f, z), new Vector3(0.008f, 0.005f, 0.065f), Part.Blued);
            b.Box("TriggerGuardFront", new Vector3(0f, y - 0.008f, z + 0.03f), new Vector3(0.008f, 0.022f, 0.005f), Part.Blued);
            b.Box("Trigger", new Vector3(0f, y - 0.002f, z - 0.002f), new Vector3(0.005f, 0.02f, 0.006f), Part.Bright, 15f);
        }

        static void Muzzle(Builder b, Vector3 pos, float flashScale)
        {
            b.M.Muzzle = Greybox.Empty("Muzzle", b.Root, pos);
            b.M.FlashScale = flashScale;
        }

        /// <summary>
        /// The upgrade glow group: strips down both sides, a spine along the top, a ring at the muzzle and a
        /// cell on the magazine, plus a small light. Starts hidden; WeaponView lights it per tier.
        /// </summary>
        static void Glow(Builder b, float stripX, float stripY, float stripZ, float stripLength, Vector3 spine, float spineLength,
            Vector3 ring, float ringSize, Vector3 cell, Vector3 cellSize, float cellPitch)
            => AddGlow(b.M, b.Root, stripX, stripY, stripZ, stripLength, spine, spineLength, ring, ringSize, cell, cellSize, cellPitch);

        /// <summary>The upgrade glow group (see Glow) under any root, for the pack models too.</summary>
        public static void AddGlow(Model m, Transform root, float stripX, float stripY, float stripZ, float stripLength, Vector3 spine, float spineLength,
            Vector3 ring, float ringSize, Vector3 cell, Vector3 cellSize, float cellPitch)
        {
            var glow = Greybox.Empty("Upgrade Glow", root, Vector3.zero).gameObject;
            var t = glow.transform;
            var parts = new[]
            {
                Greybox.Prim(PrimitiveType.Cube, "StripL", t, new Vector3(-stripX, stripY, stripZ), new Vector3(0.004f, 0.012f, stripLength), Color.white, collider: false, unlit: true),
                Greybox.Prim(PrimitiveType.Cube, "StripR", t, new Vector3(stripX, stripY, stripZ), new Vector3(0.004f, 0.012f, stripLength), Color.white, collider: false, unlit: true),
                Greybox.Prim(PrimitiveType.Cube, "Spine", t, spine, new Vector3(0.008f, 0.003f, spineLength), Color.white, collider: false, unlit: true),
                Greybox.Prim(PrimitiveType.Cube, "MuzzleRing", t, ring, new Vector3(ringSize, ringSize, 0.012f), Color.white, collider: false, unlit: true),
                Greybox.Prim(PrimitiveType.Cube, "MagCell", t, cell, cellSize, Color.white, collider: false, unlit: true),
            };
            parts[4].transform.localRotation = Quaternion.Euler(cellPitch, 0f, 0f);
            m.GlowParts = new Renderer[parts.Length];
            for (int i = 0; i < parts.Length; i++) m.GlowParts[i] = parts[i].GetComponent<Renderer>();
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 1.4f;
            light.shadows = LightShadows.None;
            m.Glow = glow;
            m.GlowLight = light;
            glow.SetActive(false);
        }

        // ---------------------------------------------------------------- guns

        /// <summary>Carbines and assault rifles; the DMR swaps irons for a holographic sight, the LMG takes a belt box.</summary>
        static void Rifle(Builder b, float length, Shape shape)
        {
            bool dmr = shape == Shape.Dmr, lmg = shape == Shape.Lmg;
            float hgStart = 0.15f, hgEnd = hgStart + (dmr ? 0.2f : lmg ? 0.15f : 0.17f) * length;
            float hgMid = (hgStart + hgEnd) * 0.5f, hgLength = hgEnd - hgStart;
            float barrelEnd = (dmr ? 0.44f : lmg ? 0.42f : 0.46f) * length;
            const float bore = 0.015f;

            // Receiver: upper and lower halves, rail on top, port and bolt on the right.
            b.Box("Upper", new Vector3(0f, 0.014f, 0f), new Vector3(0.062f, 0.058f, 0.3f), Part.Body);
            b.Box("Lower", new Vector3(0f, -0.032f, -0.02f), new Vector3(0.054f, 0.036f, 0.2f), Part.Body);
            float railTop;
            if (lmg)
            {
                b.Box("FeedCover", new Vector3(0f, 0.05f, -0.01f), new Vector3(0.06f, 0.016f, 0.17f), Part.Body);
                b.Box("FeedLatch", new Vector3(0f, 0.06f, -0.09f), new Vector3(0.03f, 0.006f, 0.014f), Part.Bright);
                railTop = 0.058f;
            }
            else
            {
                Rail(b, 0.047f, -0.01f, 0.26f);
                railTop = 0.051f;
            }
            b.Box("EjectionPort", new Vector3(0.0312f, 0.018f, 0.03f), new Vector3(0.0015f, 0.022f, 0.07f), Part.Dark);
            b.Box("Bolt", new Vector3(0.0322f, 0.018f, 0.04f), new Vector3(0.0015f, 0.012f, 0.03f), Part.Bright);
            b.Box("Deflector", new Vector3(0.033f, 0.026f, -0.015f), new Vector3(0.006f, 0.016f, 0.012f), Part.Body);
            b.Box("ChargingHandle", new Vector3(0f, 0.038f, -0.158f), new Vector3(0.04f, 0.01f, 0.016f), Part.Blued);
            b.Box("Selector", new Vector3(-0.0285f, -0.028f, -0.06f), new Vector3(0.004f, 0.006f, 0.014f), Part.Bright);

            // Handguard with vents, rail underneath.
            b.Box("Handguard", new Vector3(0f, 0.012f, hgMid), new Vector3(0.064f, 0.062f, hgLength), Part.Furniture);
            for (int i = 0; i < 3; i++)
            {
                float z = hgStart + (i + 1) * hgLength / 4f;
                b.Box("Vent", new Vector3(-0.0322f, 0.027f, z), new Vector3(0.0015f, 0.008f, hgLength * 0.16f), Part.Dark);
                b.Box("Vent", new Vector3(0.0322f, 0.027f, z), new Vector3(0.0015f, 0.008f, hgLength * 0.16f), Part.Dark);
            }
            b.Box("RailLow", new Vector3(0f, -0.022f, hgMid), new Vector3(0.02f, 0.006f, hgLength * 0.8f), Part.Blued);

            // Barrel and muzzle device.
            if (lmg)
            {
                float shroudStart = hgEnd - 0.01f, shroudEnd = barrelEnd - 0.06f;
                b.Tube("Shroud", new Vector3(0f, bore, (shroudStart + shroudEnd) * 0.5f), 0.038f, shroudEnd - shroudStart, Part.Blued);
                for (int i = 0; i < 4; i++)
                {
                    float z = shroudStart + (i + 0.5f) * (shroudEnd - shroudStart) / 4f;
                    b.Box("ShroudHole", new Vector3(-0.0192f, bore, z), new Vector3(0.001f, 0.012f, 0.02f), Part.Dark);
                    b.Box("ShroudHole", new Vector3(0.0192f, bore, z), new Vector3(0.001f, 0.012f, 0.02f), Part.Dark);
                }
                b.Tube("Barrel", new Vector3(0f, bore, barrelEnd - 0.04f), 0.024f, 0.06f, Part.Blued);
                // Folded bipod under the handguard.
                b.Tube("BipodL", new Vector3(-0.012f, -0.03f, hgEnd - 0.06f), 0.008f, 0.16f, Part.Blued);
                b.Tube("BipodR", new Vector3(0.012f, -0.03f, hgEnd - 0.06f), 0.008f, 0.16f, Part.Blued);
                b.Box("BipodFoot", new Vector3(0f, -0.03f, hgEnd + 0.02f), new Vector3(0.034f, 0.008f, 0.012f), Part.Polymer);
            }
            else
            {
                b.Tube("Barrel", new Vector3(0f, bore, (hgEnd - 0.02f + barrelEnd - 0.04f) * 0.5f), 0.022f, barrelEnd - 0.02f - hgEnd, Part.Blued);
            }
            if (dmr)
            {
                b.Box("Brake", new Vector3(0f, bore, barrelEnd - 0.02f), new Vector3(0.034f, 0.026f, 0.045f), Part.Blued);
                b.Box("BrakeVent", new Vector3(-0.0172f, bore, barrelEnd - 0.026f), new Vector3(0.001f, 0.016f, 0.008f), Part.Dark);
                b.Box("BrakeVent", new Vector3(0.0172f, bore, barrelEnd - 0.026f), new Vector3(0.001f, 0.016f, 0.008f), Part.Dark);
                b.Box("BrakeVent", new Vector3(-0.0172f, bore, barrelEnd - 0.012f), new Vector3(0.001f, 0.016f, 0.008f), Part.Dark);
                b.Box("BrakeVent", new Vector3(0.0172f, bore, barrelEnd - 0.012f), new Vector3(0.001f, 0.016f, 0.008f), Part.Dark);
            }
            else
            {
                b.Tube("FlashHider", new Vector3(0f, bore, barrelEnd - 0.02f), 0.03f, 0.045f, Part.Blued);
                b.Box("HiderSlot", new Vector3(-0.0152f, bore, barrelEnd - 0.016f), new Vector3(0.001f, 0.006f, 0.03f), Part.Dark);
                b.Box("HiderSlot", new Vector3(0.0152f, bore, barrelEnd - 0.016f), new Vector3(0.001f, 0.006f, 0.03f), Part.Dark);
            }
            b.Tube("Bore", new Vector3(0f, bore, barrelEnd + 0.0015f), 0.013f, 0.002f, Part.Dark);

            // Sights.
            if (dmr)
            {
                HoloSight(b, -0.05f, railTop);
            }
            else
            {
                float frontZ = hgEnd + 0.025f;
                float frontBaseY = lmg ? 0.056f : 0.052f;
                b.Box("FrontSightBase", new Vector3(0f, (bore + frontBaseY) * 0.5f, frontZ), new Vector3(0.014f, frontBaseY - bore, 0.022f), Part.Blued);
                b.Box("RearSightBlock", new Vector3(0f, railTop + 0.004f, -0.12f), new Vector3(0.032f, 0.008f, 0.024f), Part.Blued);
                IronSights(b, -0.12f, railTop + 0.008f, frontZ, frontBaseY);
            }

            // Feed: a curved box magazine, a straight one for the DMR, or the LMG's belt box.
            Vector3 cell;
            Vector3 cellSize;
            float cellPitch;
            if (lmg)
            {
                b.Box("AmmoBox", new Vector3(-0.012f, -0.098f, 0.05f), new Vector3(0.07f, 0.085f, 0.1f), Part.Furniture);
                b.Box("AmmoBoxLid", new Vector3(-0.012f, -0.054f, 0.05f), new Vector3(0.074f, 0.006f, 0.104f), Part.Blued);
                for (int i = 0; i < 4; i++)
                    b.Box("BeltRound", new Vector3(-0.034f, -0.045f + i * 0.016f, 0.03f), new Vector3(0.008f, 0.012f, 0.03f), Part.Brass, 8f * i);
                cell = new Vector3(-0.012f, -0.098f, 0.1015f);
                cellSize = new Vector3(0.06f, 0.06f, 0.004f);
                cellPitch = 0f;
            }
            else if (dmr)
            {
                b.Box("MagWell", new Vector3(0f, -0.058f, 0.06f), new Vector3(0.05f, 0.03f, 0.078f), Part.Body);
                b.Box("Magazine", new Vector3(0f, -0.105f, 0.064f), new Vector3(0.042f, 0.09f, 0.062f), Part.Steel, -4f);
                b.Box("MagBase", new Vector3(0f, -0.152f, 0.067f), new Vector3(0.046f, 0.008f, 0.066f), Part.Polymer, -4f);
                cell = new Vector3(0f, -0.105f, 0.0965f);
                cellSize = new Vector3(0.044f, 0.07f, 0.004f);
                cellPitch = -4f;
            }
            else
            {
                b.Box("MagWell", new Vector3(0f, -0.058f, 0.06f), new Vector3(0.05f, 0.03f, 0.078f), Part.Body);
                b.Box("Magazine", new Vector3(0f, -0.095f, 0.064f), new Vector3(0.042f, 0.07f, 0.064f), Part.Steel, -6f);
                b.Box("MagazineLow", new Vector3(0f, -0.152f, 0.076f), new Vector3(0.04f, 0.06f, 0.06f), Part.Steel, -16f);
                b.Box("MagBase", new Vector3(0f, -0.184f, 0.086f), new Vector3(0.044f, 0.008f, 0.064f), Part.Polymer, -16f);
                cell = new Vector3(0f, -0.098f, 0.0975f);
                cellSize = new Vector3(0.044f, 0.07f, 0.004f);
                cellPitch = -6f;
            }

            // Grip, trigger and stock.
            b.Box("Grip", new Vector3(0f, -0.08f, -0.09f), new Vector3(0.036f, 0.095f, 0.048f), Part.Furniture, 16f);
            TriggerGroup(b, -0.055f, -0.035f);
            float stockBack = dmr || lmg ? -0.38f : -0.35f;
            b.Tube("BufferTube", new Vector3(0f, 0.008f, -0.22f), 0.03f, 0.16f, Part.Blued);
            b.Box("Stock", new Vector3(0f, -0.008f, stockBack + 0.07f), new Vector3(0.046f, 0.078f, 0.13f), Part.Furniture);
            b.Box("Cheek", new Vector3(0f, 0.034f, stockBack + 0.08f), new Vector3(0.038f, 0.012f, 0.1f), Part.Furniture);
            b.Box("ButtPad", new Vector3(0f, -0.01f, stockBack - 0.002f), new Vector3(0.05f, 0.088f, 0.012f), Part.Polymer);

            Muzzle(b, new Vector3(0f, bore, barrelEnd + 0.005f), dmr ? 1.2f : lmg ? 1.15f : 1f);
            Glow(b, 0.0335f, 0.003f, hgMid, hgLength * 0.9f,
                new Vector3(0f, lmg ? 0.0595f : 0.0525f, -0.01f), lmg ? 0.15f : 0.2f,
                new Vector3(0f, bore, barrelEnd - 0.05f), lmg ? 0.042f : 0.034f,
                cell, cellSize, cellPitch);
        }

        /// <summary>A holographic sight on the rail: an open window with a glowing red dot on the sight line.</summary>
        static void HoloSight(Builder b, float z, float railTop)
        {
            b.Box("HoloBase", new Vector3(0f, railTop + 0.005f, z - 0.01f), new Vector3(0.03f, 0.01f, 0.065f), Part.Polymer);
            b.Box("HoloPillarL", new Vector3(-0.0165f, SightY + 0.002f, z), new Vector3(0.004f, 0.032f, 0.03f), Part.Polymer);
            b.Box("HoloPillarR", new Vector3(0.0165f, SightY + 0.002f, z), new Vector3(0.004f, 0.032f, 0.03f), Part.Polymer);
            b.Box("HoloHood", new Vector3(0f, SightY + 0.0185f, z), new Vector3(0.037f, 0.004f, 0.032f), Part.Polymer);
            b.Box("HoloButtons", new Vector3(0.011f, railTop + 0.013f, z - 0.035f), new Vector3(0.01f, 0.006f, 0.014f), Part.Blued);
            b.Box("HoloGlass", new Vector3(0f, SightY + 0.002f, z + 0.006f), new Vector3(0.029f, 0.028f, 0.001f), Part.Glass);
            b.Box("HoloDot", new Vector3(0f, SightY, z + 0.005f), new Vector3(0.0014f, 0.0014f, 0.0006f), Part.Reticle);
            for (int i = 0; i < 4; i++)
            {
                var dir = i switch { 0 => Vector3.up, 1 => Vector3.down, 2 => Vector3.left, _ => Vector3.right };
                var size = i < 2 ? new Vector3(0.0006f, 0.0026f, 0.0006f) : new Vector3(0.0026f, 0.0006f, 0.0006f);
                b.Box("HoloTick", new Vector3(dir.x * 0.0065f, SightY + dir.y * 0.0065f, z + 0.005f), size, Part.Reticle);
            }
        }

        static void Smg(Builder b)
        {
            const float bore = 0.012f;
            b.Box("Receiver", new Vector3(0f, 0.012f, 0.02f), new Vector3(0.058f, 0.064f, 0.25f), Part.Body);
            b.Box("Lower", new Vector3(0f, -0.03f, 0f), new Vector3(0.05f, 0.03f, 0.16f), Part.Body);
            Rail(b, 0.048f, 0.02f, 0.2f, 0.02f);
            b.Box("EjectionPort", new Vector3(0.0292f, 0.02f, 0.06f), new Vector3(0.0015f, 0.02f, 0.05f), Part.Dark);
            b.Box("Bolt", new Vector3(0.0302f, 0.02f, 0.066f), new Vector3(0.0015f, 0.01f, 0.024f), Part.Bright);
            b.Box("ChargingHandle", new Vector3(-0.034f, 0.025f, 0.1f), new Vector3(0.012f, 0.008f, 0.012f), Part.Blued);
            b.Box("Shroud", new Vector3(0f, 0.008f, 0.19f), new Vector3(0.05f, 0.048f, 0.09f), Part.Furniture);
            for (int i = 0; i < 3; i++)
            {
                b.Box("Vent", new Vector3(-0.0252f, 0.012f, 0.165f + i * 0.025f), new Vector3(0.001f, 0.022f, 0.008f), Part.Dark);
                b.Box("Vent", new Vector3(0.0252f, 0.012f, 0.165f + i * 0.025f), new Vector3(0.001f, 0.022f, 0.008f), Part.Dark);
            }
            b.Tube("Barrel", new Vector3(0f, bore, 0.255f), 0.02f, 0.05f, Part.Blued);
            b.Tube("MuzzleNut", new Vector3(0f, bore, 0.285f), 0.028f, 0.02f, Part.Blued);
            b.Tube("Bore", new Vector3(0f, bore, 0.2955f), 0.011f, 0.002f, Part.Dark);
            b.Box("Foregrip", new Vector3(0f, -0.045f, 0.2f), new Vector3(0.026f, 0.06f, 0.028f), Part.Polymer);

            b.Box("FrontSightBase", new Vector3(0f, 0.042f, 0.2f), new Vector3(0.014f, 0.02f, 0.02f), Part.Blued);
            IronSights(b, -0.07f, 0.052f, 0.2f, 0.052f);

            b.Box("MagWell", new Vector3(0f, -0.055f, 0.06f), new Vector3(0.042f, 0.03f, 0.056f), Part.Body);
            b.Box("Magazine", new Vector3(0f, -0.125f, 0.064f), new Vector3(0.032f, 0.15f, 0.046f), Part.Steel, -4f);
            b.Box("MagBase", new Vector3(0f, -0.2f, 0.07f), new Vector3(0.036f, 0.008f, 0.05f), Part.Polymer, -4f);

            b.Box("Grip", new Vector3(0f, -0.075f, -0.06f), new Vector3(0.034f, 0.09f, 0.046f), Part.Furniture, 14f);
            TriggerGroup(b, -0.05f, -0.01f);
            // Folding wire stock.
            b.Box("EndPlate", new Vector3(0f, 0.005f, -0.108f), new Vector3(0.05f, 0.06f, 0.012f), Part.Body);
            b.Tube("StockRodTop", new Vector3(0f, 0.03f, -0.19f), 0.008f, 0.16f, Part.Blued);
            b.Tube("StockRodLow", new Vector3(0f, -0.022f, -0.19f), 0.008f, 0.16f, Part.Blued);
            b.Box("ButtPlate", new Vector3(0f, 0.004f, -0.272f), new Vector3(0.04f, 0.08f, 0.014f), Part.Furniture);

            Muzzle(b, new Vector3(0f, bore, 0.3f), 0.85f);
            Glow(b, 0.0305f, -0.002f, 0.0f, 0.18f, new Vector3(0f, 0.0535f, 0.02f), 0.18f,
                new Vector3(0f, bore, 0.268f), 0.03f, new Vector3(0f, -0.115f, 0.0885f), new Vector3(0.034f, 0.12f, 0.004f), -4f);
        }

        static void Revolver(Builder b)
        {
            const float bore = 0.045f;
            b.Box("Frame", new Vector3(0f, 0.03f, -0.005f), new Vector3(0.032f, 0.05f, 0.09f), Part.Body);
            b.Box("TopStrap", new Vector3(0f, 0.058f, 0f), new Vector3(0.024f, 0.008f, 0.07f), Part.Body);
            b.Tube("Cylinder", new Vector3(0f, 0.032f, 0.005f), 0.052f, 0.05f, Part.Steel);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f, rad = a * Mathf.Deg2Rad;
                b.Add(PrimitiveType.Cube, "Flute", new Vector3(Mathf.Cos(rad) * 0.0255f, 0.032f + Mathf.Sin(rad) * 0.0255f, 0.005f),
                    new Vector3(0.004f, 0.007f, 0.038f), Part.Dark, Quaternion.Euler(0f, 0f, a));
            }
            b.Tube("Barrel", new Vector3(0f, bore, 0.12f), 0.024f, 0.16f, Part.Blued);
            b.Box("Underlug", new Vector3(0f, 0.03f, 0.11f), new Vector3(0.018f, 0.02f, 0.14f), Part.Blued);
            b.Box("Rib", new Vector3(0f, 0.059f, 0.12f), new Vector3(0.012f, 0.006f, 0.16f), Part.Blued);
            b.Tube("Bore", new Vector3(0f, bore, 0.2005f), 0.012f, 0.002f, Part.Dark);
            b.Box("Hammer", new Vector3(0f, 0.062f, -0.05f), new Vector3(0.008f, 0.022f, 0.012f), Part.Bright, -25f);
            b.Box("Grip", new Vector3(0f, -0.03f, -0.055f), new Vector3(0.03f, 0.085f, 0.042f), Part.Furniture, 18f);
            b.Box("GripFrame", new Vector3(0f, -0.03f, -0.053f), new Vector3(0.012f, 0.088f, 0.045f), Part.Body, 18f);
            b.Box("ButtCap", new Vector3(0f, -0.071f, -0.07f), new Vector3(0.032f, 0.008f, 0.042f), Part.Blued, 18f);
            b.Box("TriggerGuard", new Vector3(0f, -0.02f, -0.005f), new Vector3(0.007f, 0.005f, 0.045f), Part.Blued);
            b.Box("TriggerGuardFront", new Vector3(0f, -0.008f, 0.016f), new Vector3(0.007f, 0.025f, 0.005f), Part.Blued);
            b.Box("Trigger", new Vector3(0f, -0.003f, -0.01f), new Vector3(0.005f, 0.018f, 0.005f), Part.Bright, 15f);
            IronSights(b, -0.035f, 0.062f, 0.19f, 0.062f, hood: false);

            Muzzle(b, new Vector3(0f, bore, 0.205f), 0.95f);
            Glow(b, 0.0135f, bore, 0.12f, 0.13f, new Vector3(0f, 0.0635f, 0f), 0.06f,
                new Vector3(0f, bore, 0.185f), 0.03f, new Vector3(-0.0265f, 0.032f, 0.005f), new Vector3(0.003f, 0.03f, 0.04f), 0f);
        }

        static void Shotgun(Builder b, float length)
        {
            const float bore = 0.028f;
            float barrelEnd = 0.5f * length, magEnd = barrelEnd * 0.85f;
            b.Box("Receiver", new Vector3(0f, 0.012f, -0.02f), new Vector3(0.058f, 0.07f, 0.22f), Part.Body);
            b.Box("EjectionPort", new Vector3(0.0295f, 0.02f, 0f), new Vector3(0.0015f, 0.026f, 0.07f), Part.Dark);
            b.Box("Bolt", new Vector3(0.0302f, 0.02f, 0.01f), new Vector3(0.0015f, 0.014f, 0.03f), Part.Bright);
            b.Tube("Barrel", new Vector3(0f, bore, (0.09f + barrelEnd) * 0.5f), 0.026f, barrelEnd - 0.09f, Part.Blued);
            b.Box("Rib", new Vector3(0f, 0.044f, (0.09f + barrelEnd) * 0.5f), new Vector3(0.01f, 0.006f, barrelEnd - 0.1f), Part.Blued);
            b.Tube("Bore", new Vector3(0f, bore, barrelEnd + 0.0015f), 0.018f, 0.002f, Part.Dark);
            b.Tube("MagTube", new Vector3(0f, -0.006f, (0.09f + magEnd) * 0.5f), 0.026f, magEnd - 0.09f, Part.Blued);
            b.Tube("MagCap", new Vector3(0f, -0.006f, magEnd + 0.01f), 0.03f, 0.02f, Part.Steel);
            b.Box("BarrelClamp", new Vector3(0f, 0.011f, magEnd - 0.01f), new Vector3(0.03f, 0.05f, 0.012f), Part.Blued);
            // Pump with grip grooves.
            float pumpZ = 0.12f + 0.11f * length;
            b.Box("Pump", new Vector3(0f, -0.006f, pumpZ), new Vector3(0.046f, 0.044f, 0.14f), Part.Furniture);
            for (int i = 0; i < 5; i++)
                b.Box("Groove", new Vector3(0f, -0.006f, pumpZ - 0.05f + i * 0.025f), new Vector3(0.0475f, 0.0455f, 0.005f), Part.Dark);
            // Bead front sight on a ramp, notch rear on the receiver.
            b.Box("FrontSightBase", new Vector3(0f, 0.056f, barrelEnd - 0.03f), new Vector3(0.012f, 0.018f, 0.018f), Part.Blued);
            b.Box("RearSightBlock", new Vector3(0f, 0.05f, -0.09f), new Vector3(0.03f, 0.008f, 0.024f), Part.Blued);
            IronSights(b, -0.09f, 0.054f, barrelEnd - 0.03f, 0.065f, hood: false);
            // Spare shells in a side saddle.
            b.Box("SideSaddle", new Vector3(-0.031f, 0.012f, -0.03f), new Vector3(0.003f, 0.05f, 0.1f), Part.Polymer);
            for (int i = 0; i < 4; i++)
            {
                float z = -0.065f + i * 0.023f;
                b.Add(PrimitiveType.Cylinder, "Shell", new Vector3(-0.0415f, 0.014f, z), new Vector3(0.018f, 0.022f, 0.018f), Part.Shell, Quaternion.identity);
                b.Add(PrimitiveType.Cylinder, "ShellBase", new Vector3(-0.0415f, -0.01f, z), new Vector3(0.0185f, 0.004f, 0.0185f), Part.Brass, Quaternion.identity);
            }
            TriggerGroup(b, -0.02f, -0.06f);
            b.Box("Wrist", new Vector3(0f, -0.034f, -0.15f), new Vector3(0.04f, 0.055f, 0.07f), Part.Furniture, 20f);
            b.Box("Stock", new Vector3(0f, -0.025f, -0.25f), new Vector3(0.044f, 0.068f, 0.22f), Part.Furniture, -7f);
            b.Box("ButtPad", new Vector3(0f, -0.039f, -0.36f), new Vector3(0.048f, 0.088f, 0.014f), Part.Polymer, -7f);

            Muzzle(b, new Vector3(0f, bore, barrelEnd + 0.005f), 1.5f);
            Glow(b, 0.0245f, -0.006f, pumpZ, 0.12f, new Vector3(0f, 0.0475f, (0.09f + barrelEnd) * 0.5f), barrelEnd - 0.12f,
                new Vector3(0f, bore, barrelEnd - 0.06f), 0.032f, new Vector3(0.0298f, -0.008f, -0.04f), new Vector3(0.003f, 0.016f, 0.08f), 0f);
        }

        static void Sniper(Builder b, float length)
        {
            const float bore = 0.018f;
            float barrelEnd = 0.42f * length;
            // Round bolt action with the bolt handle out to the right.
            b.Tube("Receiver", new Vector3(0f, bore, -0.01f), 0.05f, 0.24f, Part.Body);
            b.Tube("BoltShroud", new Vector3(0f, bore, -0.14f), 0.034f, 0.03f, Part.Blued);
            b.Box("EjectionPort", new Vector3(0.0252f, 0.025f, 0f), new Vector3(0.0015f, 0.014f, 0.06f), Part.Dark);
            b.Box("BoltArm", new Vector3(0.035f, 0.012f, -0.07f), new Vector3(0.03f, 0.008f, 0.008f), Part.Bright);
            b.Ball("BoltKnob", new Vector3(0.052f, 0.008f, -0.07f), 0.02f, Part.Polymer);
            // Heavy fluted barrel and a muzzle brake.
            float barrelStart = 0.11f, brakeStart = barrelEnd - 0.05f, mid = (barrelStart + brakeStart) * 0.5f, len = brakeStart - barrelStart;
            b.Tube("Barrel", new Vector3(0f, bore, mid), 0.028f, len, Part.Blued);
            b.Box("Flute", new Vector3(-0.0142f, bore, mid), new Vector3(0.001f, 0.004f, len * 0.6f), Part.Dark);
            b.Box("Flute", new Vector3(0.0142f, bore, mid), new Vector3(0.001f, 0.004f, len * 0.6f), Part.Dark);
            b.Box("Flute", new Vector3(0f, bore + 0.0142f, mid), new Vector3(0.004f, 0.001f, len * 0.6f), Part.Dark);
            b.Box("Brake", new Vector3(0f, bore, barrelEnd - 0.025f), new Vector3(0.04f, 0.03f, 0.05f), Part.Blued);
            for (int i = 0; i < 3; i++)
            {
                float z = barrelEnd - 0.04f + i * 0.014f;
                b.Box("BrakeVent", new Vector3(-0.0202f, bore, z), new Vector3(0.001f, 0.018f, 0.008f), Part.Dark);
                b.Box("BrakeVent", new Vector3(0.0202f, bore, z), new Vector3(0.001f, 0.018f, 0.008f), Part.Dark);
            }
            b.Tube("Bore", new Vector3(0f, bore, barrelEnd + 0.0015f), 0.014f, 0.002f, Part.Dark);
            // Chassis stock.
            b.Box("Forend", new Vector3(0f, -0.014f, 0.17f), new Vector3(0.056f, 0.05f, 0.28f), Part.Furniture);
            b.Box("Bed", new Vector3(0f, -0.014f, -0.04f), new Vector3(0.056f, 0.05f, 0.18f), Part.Furniture);
            b.Box("Grip", new Vector3(0f, -0.07f, -0.1f), new Vector3(0.036f, 0.09f, 0.048f), Part.Furniture, 16f);
            b.Box("Stock", new Vector3(0f, -0.02f, -0.27f), new Vector3(0.05f, 0.085f, 0.2f), Part.Furniture);
            b.Box("Cheek", new Vector3(0f, 0.03f, -0.27f), new Vector3(0.042f, 0.016f, 0.13f), Part.Polymer);
            b.Box("ButtPad", new Vector3(0f, -0.022f, -0.376f), new Vector3(0.054f, 0.095f, 0.014f), Part.Polymer);
            b.Box("Magazine", new Vector3(0f, -0.06f, 0f), new Vector3(0.04f, 0.045f, 0.07f), Part.Steel);
            TriggerGroup(b, -0.045f, -0.06f);
            b.Tube("BipodL", new Vector3(-0.014f, -0.045f, 0.2f), 0.008f, 0.17f, Part.Blued);
            b.Tube("BipodR", new Vector3(0.014f, -0.045f, 0.2f), 0.008f, 0.17f, Part.Blued);

            // Scope on the sight line. Its tube, eyepiece, turrets and rings hide while looking through it,
            // leaving the objective's ring, the lens and the reticle.
            b.Box("ScopeBase", new Vector3(0f, 0.047f, 0f), new Vector3(0.02f, 0.008f, 0.17f), Part.Blued);
            var tube = Greybox.Empty("ScopeTube", b.Root, Vector3.zero);
            b.Box("RingBase", new Vector3(0f, 0.052f, -0.04f), new Vector3(0.022f, 0.01f, 0.016f), Part.Blued, 0f, tube);
            b.Box("RingBase", new Vector3(0f, 0.052f, 0.06f), new Vector3(0.022f, 0.01f, 0.016f), Part.Blued, 0f, tube);
            b.Octagon("Ring", SightY, -0.04f, 0.0185f, 0.016f, 0.004f, Part.Blued, tube);
            b.Octagon("Ring", SightY, 0.06f, 0.0185f, 0.016f, 0.004f, Part.Blued, tube);
            b.Octagon("ScopeBody", SightY, 0.02f, 0.0165f, 0.18f, 0.003f, Part.Steel, tube);
            b.Octagon("Eyepiece", SightY, -0.09f, 0.021f, 0.04f, 0.004f, Part.Steel, tube);
            b.Add(PrimitiveType.Cylinder, "Turret", new Vector3(0f, SightY + 0.0245f, 0.02f), new Vector3(0.018f, 0.008f, 0.018f), Part.Blued, Quaternion.identity, tube);
            b.Add(PrimitiveType.Cylinder, "Windage", new Vector3(0.0245f, SightY, 0.02f), new Vector3(0.018f, 0.008f, 0.018f), Part.Blued, Quaternion.Euler(0f, 0f, 90f), tube);
            b.M.HideWhenAimed = tube.gameObject;
            b.Octagon("Objective", SightY, 0.135f, 0.026f, 0.05f, 0.004f, Part.Steel);
            b.Add(PrimitiveType.Cylinder, "Lens", new Vector3(0f, SightY, 0.158f), new Vector3(0.05f, 0.0005f, 0.05f), Part.Glass, Quaternion.Euler(90f, 0f, 0f));
            b.Box("ReticleH", new Vector3(0f, SightY, 0.155f), new Vector3(0.046f, 0.0006f, 0.0004f), Part.Dark);
            b.Box("ReticleV", new Vector3(0f, SightY, 0.155f), new Vector3(0.0006f, 0.046f, 0.0004f), Part.Dark);
            b.Box("ReticleDot", new Vector3(0f, SightY, 0.1545f), new Vector3(0.0012f, 0.0012f, 0.0004f), Part.Reticle);

            Muzzle(b, new Vector3(0f, bore, barrelEnd + 0.005f), 1.6f);
            Glow(b, 0.0295f, -0.014f, 0.17f, 0.25f, new Vector3(0f, 0.0518f, 0f), 0.15f,
                new Vector3(0f, bore, brakeStart - 0.01f), 0.036f, new Vector3(0f, -0.06f, 0.0365f), new Vector3(0.04f, 0.035f, 0.004f), 0f);
        }

        static void Energy(Builder b, float length)
        {
            const float bore = 0.015f;
            float barrelEnd = 0.45f * length;
            b.M.EnergyFlash = true;
            b.Box("Shell", new Vector3(0f, 0.012f, 0.02f), new Vector3(0.064f, 0.07f, 0.32f), Part.Body);
            b.Box("Fin", new Vector3(0f, 0.05f, -0.02f), new Vector3(0.016f, 0.008f, 0.22f), Part.Furniture);
            b.Box("PanelL", new Vector3(-0.033f, 0.014f, 0.03f), new Vector3(0.004f, 0.05f, 0.24f), Part.Furniture);
            b.Box("PanelR", new Vector3(0.033f, 0.014f, 0.03f), new Vector3(0.004f, 0.05f, 0.24f), Part.Furniture);
            b.Box("CoreWindowL", new Vector3(-0.0355f, 0.024f, 0.03f), new Vector3(0.002f, 0.012f, 0.16f), Part.Core);
            b.Box("CoreWindowR", new Vector3(0.0355f, 0.024f, 0.03f), new Vector3(0.002f, 0.012f, 0.16f), Part.Core);
            // Emitter rod wrapped in coils, with three prongs at the tip.
            float rodStart = 0.17f;
            b.Tube("Emitter", new Vector3(0f, bore, (rodStart + barrelEnd) * 0.5f), 0.018f, barrelEnd - rodStart, Part.Blued);
            for (int i = 0; i < 5; i++)
            {
                float z = rodStart + 0.04f + i * (barrelEnd - rodStart - 0.09f) / 4f;
                b.Tube("Coil", new Vector3(0f, bore, z), 0.048f, 0.012f, Part.Bright);
                b.Tube("CoilGlow", new Vector3(0f, bore, z + 0.012f), 0.036f, 0.006f, Part.Core);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = 90f + i * 120f, rad = a * Mathf.Deg2Rad;
                b.Add(PrimitiveType.Cube, "Prong", new Vector3(Mathf.Cos(rad) * 0.02f, bore + Mathf.Sin(rad) * 0.02f, barrelEnd - 0.02f),
                    new Vector3(0.006f, 0.006f, 0.05f), Part.Steel, Quaternion.Euler(0f, 0f, a));
            }
            b.Box("FrontSightBase", new Vector3(0f, 0.052f, 0.17f), new Vector3(0.012f, 0.01f, 0.016f), Part.Blued);
            IronSights(b, -0.1f, 0.054f, 0.17f, 0.057f);
            // Power cell instead of a magazine.
            b.Box("Cell", new Vector3(0f, -0.07f, 0.06f), new Vector3(0.04f, 0.08f, 0.06f), Part.Steel);
            b.Box("CellGlow", new Vector3(0.0205f, -0.07f, 0.06f), new Vector3(0.002f, 0.05f, 0.03f), Part.Core);
            b.Box("CellGlowL", new Vector3(-0.0205f, -0.07f, 0.06f), new Vector3(0.002f, 0.05f, 0.03f), Part.Core);
            b.Box("Grip", new Vector3(0f, -0.075f, -0.09f), new Vector3(0.036f, 0.09f, 0.048f), Part.Polymer, 16f);
            TriggerGroup(b, -0.05f, -0.035f);
            b.Box("Stock", new Vector3(0f, -0.005f, -0.24f), new Vector3(0.05f, 0.07f, 0.2f), Part.Furniture);
            b.Box("StockCut", new Vector3(0f, -0.01f, -0.235f), new Vector3(0.052f, 0.026f, 0.1f), Part.Dark);
            b.Box("ButtPad", new Vector3(0f, -0.005f, -0.345f), new Vector3(0.054f, 0.078f, 0.012f), Part.Polymer);

            Muzzle(b, new Vector3(0f, bore, barrelEnd + 0.01f), 0.9f);
            Glow(b, 0.0355f, -0.008f, 0.03f, 0.2f, new Vector3(0f, 0.0555f, -0.02f), 0.2f,
                new Vector3(0f, bore, rodStart + 0.012f), 0.05f, new Vector3(0f, -0.07f, 0.0915f), new Vector3(0.04f, 0.06f, 0.004f), 0f);
        }

        static void Launcher(Builder b, float length)
        {
            float barrelEnd = 0.4f * length, tubeStart = -0.25f;
            // A scrap-built cannon: a rusty tube held together with bands and rivets.
            b.M.BodyColor = new Color(0.42f, 0.27f, 0.17f);
            b.M.BodySurface = Surface.Rust;
            b.M.EnergyFlash = false;
            b.Tube("LaunchTube", new Vector3(0f, 0f, (tubeStart + barrelEnd) * 0.5f), 0.085f, barrelEnd - tubeStart, Part.Body);
            float[] bands = { -0.15f, 0.1f, barrelEnd - 0.03f };
            foreach (float z in bands)
            {
                b.Tube("Band", new Vector3(0f, 0f, z), 0.092f, 0.02f, Part.Blued);
                b.Ball("Rivet", new Vector3(0.046f, 0f, z), 0.012f, Part.Bright);
                b.Ball("Rivet", new Vector3(-0.046f, 0f, z), 0.012f, Part.Bright);
            }
            b.Tube("MuzzleBell", new Vector3(0f, 0f, barrelEnd + 0.01f), 0.1f, 0.03f, Part.Steel);
            b.Tube("Bore", new Vector3(0f, 0f, barrelEnd + 0.026f), 0.075f, 0.002f, Part.Dark);
            for (int i = 0; i < 3; i++)
            {
                b.Box("Vent", new Vector3(-0.0425f, 0f, -0.02f + i * 0.045f), new Vector3(0.003f, 0.03f, 0.02f), Part.Core);
                b.Box("Vent", new Vector3(0.0425f, 0f, -0.02f + i * 0.045f), new Vector3(0.003f, 0.03f, 0.02f), Part.Core);
            }
            b.Box("Hopper", new Vector3(-0.05f, 0.012f, -0.04f), new Vector3(0.03f, 0.05f, 0.08f), Part.Steel);
            b.Box("RearSightBlock", new Vector3(0f, 0.048f, -0.1f), new Vector3(0.03f, 0.012f, 0.02f), Part.Blued);
            b.Box("FrontSightBase", new Vector3(0f, 0.052f, barrelEnd - 0.06f), new Vector3(0.012f, 0.02f, 0.016f), Part.Blued);
            IronSights(b, -0.1f, 0.054f, barrelEnd - 0.06f, 0.062f);
            b.Box("TriggerBox", new Vector3(0f, -0.05f, -0.03f), new Vector3(0.03f, 0.03f, 0.09f), Part.Steel);
            b.Box("Grip", new Vector3(0f, -0.09f, -0.06f), new Vector3(0.036f, 0.09f, 0.048f), Part.Furniture, 16f);
            TriggerGroup(b, -0.066f, -0.02f);
            b.Box("Foregrip", new Vector3(0f, -0.08f, 0.18f), new Vector3(0.03f, 0.08f, 0.032f), Part.Furniture, -10f);
            b.Box("ForegripClamp", new Vector3(0f, -0.043f, 0.18f), new Vector3(0.036f, 0.012f, 0.04f), Part.Blued);
            b.Box("ShoulderPad", new Vector3(0f, -0.005f, tubeStart - 0.015f), new Vector3(0.07f, 0.09f, 0.03f), Part.Polymer);

            Muzzle(b, new Vector3(0f, 0f, barrelEnd + 0.03f), 1.7f);
            Glow(b, 0.037f, -0.024f, 0.1f, 0.3f, new Vector3(0f, 0.0445f, 0.1f), 0.2f,
                new Vector3(0f, 0f, barrelEnd - 0.005f), 0.096f, new Vector3(-0.0655f, 0.012f, -0.04f), new Vector3(0.003f, 0.035f, 0.06f), 0f);
        }
    }
}
