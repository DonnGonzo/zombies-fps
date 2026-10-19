using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Rooms brought to life with Ivan's two packs: broken furniture (chairs, tables, sofas, benches, cabinets,
    /// bookshelves, crates, barricades) in the barracks, archives, chapel, depot and yards, and the dark magic lab
    /// round the Lantern Core's reactor (altar, sacrifice slab, alchemy desks, jars, bones, candles, torches, a
    /// trophy head, hanging chains and a spell circle turning round the reactor's foot).
    ///
    /// As with the other props, where things stand and their colliders never depend on the models being there,
    /// so the solid map is the same on every machine; without the art, plain boxes stand in for the solid ones.
    /// </summary>
    public partial class ZoneMap
    {
        enum Mount { Floor, Wall, Open }

        /// <summary>A model in a pack: its bounds in Unity axes (front is +z, the side away from the wall), how it's placed.</summary>
        readonly struct PackItem
        {
            public readonly string Path;
            public readonly Vector3 Min, Max;
            public readonly Mount Mount;
            public PackItem(string path, Vector3 min, Vector3 max, Mount mount) { Path = path; Min = min; Max = max; Mount = mount; }
            public Vector3 Size => Max - Min;
        }

        const string Furniture = "BrokenFurniture/";
        const string Lab = "DarkMagicLab/";

        static readonly PackItem Chair = new PackItem(Furniture + "Broken_Dining_Chair", new Vector3(-0.49f, -0.08f, -0.43f), new Vector3(0.47f, 1.12f, 0.25f), Mount.Floor);
        static readonly PackItem Bench = new PackItem(Furniture + "Broken_Waiting_Bench", new Vector3(-1.14f, -0.09f, -0.26f), new Vector3(0.9f, 1.1f, 0.62f), Mount.Floor);
        static readonly PackItem BrokenTable = new PackItem(Furniture + "Collapsed_Table", new Vector3(-1.27f, -0.13f, -0.43f), new Vector3(0.72f, 0.77f, 0.39f), Mount.Floor);
        static readonly PackItem Barricade = new PackItem(Furniture + "Makeshift_Barricade", new Vector3(-1.06f, -0.05f, -0.1f), new Vector3(1.06f, 1.16f, 0.69f), Mount.Floor);
        static readonly PackItem Cabinet = new PackItem(Furniture + "Ransacked_Cabinet", new Vector3(-0.59f, -0.05f, -0.26f), new Vector3(0.66f, 1.36f, 0.98f), Mount.Floor);
        static readonly PackItem Bookshelf = new PackItem(Furniture + "Smashed_Bookshelf", new Vector3(-0.65f, -0.08f, -0.58f), new Vector3(0.96f, 1.88f, 0.2f), Mount.Floor);
        static readonly PackItem Crate = new PackItem(Furniture + "Splintered_Crate", new Vector3(-0.46f, -0.07f, -0.38f), new Vector3(0.6f, 0.93f, 0.62f), Mount.Floor);
        static readonly PackItem Sofa = new PackItem(Furniture + "Torn_Sofa", new Vector3(-1.25f, -0.07f, -0.45f), new Vector3(1f, 1.3f, 0.45f), Mount.Floor);

        static readonly PackItem Desk = new PackItem(Lab + "AlchemyDesk", new Vector3(-1.05f, 0f, -0.45f), new Vector3(1.05f, 1.32f, 0.45f), Mount.Floor);
        static readonly PackItem Bones = new PackItem(Lab + "BonePile_Skulls", new Vector3(-0.51f, 0f, -0.28f), new Vector3(0.52f, 0.32f, 0.31f), Mount.Floor);
        static readonly PackItem Candles = new PackItem(Lab + "CandleCluster_Ritual", new Vector3(-0.46f, 0f, -0.29f), new Vector3(0.43f, 0.72f, 0.37f), Mount.Floor);
        static readonly PackItem Chains = new PackItem(Lab + "ChainCluster_CeilingHooks", new Vector3(-0.76f, -1.82f, -0.39f), new Vector3(0.71f, 0.08f, 0.64f), Mount.Wall);
        static readonly PackItem Jars = new PackItem(Lab + "JarSet_BodyParts", new Vector3(-0.96f, 0f, -0.26f), new Vector3(0.93f, 1.01f, 0.29f), Mount.Floor);
        static readonly PackItem Trophy = new PackItem(Lab + "MonsterHead_Trophy", new Vector3(-0.5f, 0f, -0.38f), new Vector3(0.5f, 1.14f, 0.72f), Mount.Wall);
        static readonly PackItem Altar = new PackItem(Lab + "Ritual_Altar", new Vector3(-1.25f, 0f, -0.85f), new Vector3(1.25f, 2.75f, 0.85f), Mount.Floor);
        static readonly PackItem Slab = new PackItem(Lab + "Sacrifice_Slab", new Vector3(-1.25f, 0f, -0.55f), new Vector3(1.25f, 1.18f, 0.6f), Mount.Floor);
        static readonly PackItem Aura = new PackItem(Lab + "SpellAura_Activated", new Vector3(-2f, -0.01f, -2f), new Vector3(2f, 0.58f, 2f), Mount.Open);
        static readonly PackItem Torch = new PackItem(Lab + "WallTorch_Gothic", new Vector3(-0.37f, 0f, -0.04f), new Vector3(0.11f, 0.66f, 0.33f), Mount.Wall);

        // Ivan's Ten Worlds pack (see ZoneMap.Worlds.cs). Declared here, ahead of the tables that use them, since
        // static fields in different files of a partial class have no set order.
        const string Worlds = "TenWorlds/";
        static readonly PackItem Pew = new PackItem(Worlds + "ChurchPew", new Vector3(-1.55f, 0f, -0.3f), new Vector3(1.55f, 1.6f, 0.3f), Mount.Floor);
        static readonly PackItem AltarDais = new PackItem(Worlds + "AltarDais", new Vector3(-2.7f, 0f, -1.1f), new Vector3(2.7f, 1.7f, 1.1f), Mount.Floor);
        static readonly PackItem Organ = new PackItem(Worlds + "PipeOrgan", new Vector3(-1.06f, 0f, -0.07f), new Vector3(1.06f, 3.82f, 0.07f), Mount.Floor);
        static readonly PackItem Chandelier = new PackItem(Worlds + "LanternSuspension", new Vector3(-1.43f, 0f, -1.43f), new Vector3(1.43f, 2.47f, 1.43f), Mount.Open);
        static readonly PackItem Bookcase = new PackItem(Worlds + "Bookcase", new Vector3(-0.89f, 0f, -0.27f), new Vector3(0.89f, 5.9f, 0.27f), Mount.Floor);
        static readonly PackItem ReadingTable = new PackItem(Worlds + "ReadingTable", new Vector3(-1.12f, 0f, -0.47f), new Vector3(1.12f, 0.82f, 0.47f), Mount.Floor);
        static readonly PackItem Globe = new PackItem(Worlds + "CelestialGlobe", new Vector3(-0.9f, 0f, -0.9f), new Vector3(0.9f, 2.76f, 0.9f), Mount.Floor);
        static readonly PackItem Pylon = new PackItem(Worlds + "ArcanePylon", new Vector3(-0.3f, 0f, -0.3f), new Vector3(0.3f, 1.7f, 0.3f), Mount.Floor);
        static readonly PackItem Tapestry = new PackItem(Worlds + "OccultTapestry", new Vector3(-0.65f, 0f, -0.03f), new Vector3(0.65f, 3.5f, 0.03f), Mount.Wall);
        static readonly PackItem Worktable = new PackItem(Worlds + "Worktable", new Vector3(-1.25f, 0f, -0.68f), new Vector3(1.25f, 1.44f, 0.68f), Mount.Floor);
        static readonly PackItem GardenBench = new PackItem(Worlds + "GardenBench", new Vector3(-1.05f, 0f, -0.29f), new Vector3(1.05f, 1.6f, 0.29f), Mount.Floor);
        static readonly PackItem Fountain = new PackItem(Worlds + "FountainColumn", new Vector3(-0.83f, 0f, -0.83f), new Vector3(0.83f, 1.14f, 0.83f), Mount.Floor);
        static readonly PackItem Planter = new PackItem(Worlds + "RaisedPlanter", new Vector3(-1.23f, 0f, -1.42f), new Vector3(1.23f, 5.33f, 1.42f), Mount.Floor);
        static readonly PackItem TempleChandelier = new PackItem(Worlds + "TempleChandelier", new Vector3(-1.46f, 0f, -1.46f), new Vector3(1.46f, 2.8f, 1.46f), Mount.Open);
        static readonly PackItem LatticeScreen = new PackItem(Worlds + "LatticeScreen", new Vector3(-1.15f, 0f, -0.15f), new Vector3(1.15f, 3.3f, 0.15f), Mount.Floor);
        static readonly PackItem WritingDesk = new PackItem(Worlds + "WritingDesk", new Vector3(-0.62f, 0f, -0.25f), new Vector3(0.62f, 0.79f, 0.25f), Mount.Floor);
        static readonly PackItem Stool = new PackItem(Worlds + "Stool", new Vector3(-0.42f, 0f, -0.29f), new Vector3(0.42f, 0.83f, 0.29f), Mount.Floor);
        static readonly PackItem LecternStage = new PackItem(Worlds + "LecternStage", new Vector3(-5f, 0f, -1.09f), new Vector3(5f, 1.99f, 1.09f), Mount.Floor);
        static readonly PackItem FurnaceCore = new PackItem(Worlds + "FurnaceCore", new Vector3(-2.38f, 0f, -2.38f), new Vector3(2.38f, 6.2f, 2.38f), Mount.Open);
        static readonly PackItem GaolCell = new PackItem(Worlds + "GaolCell", new Vector3(-1.26f, 0f, -1.35f), new Vector3(1.26f, 4.1f, 1.35f), Mount.Floor);
        static readonly PackItem SickBed = new PackItem(Worlds + "SickBed", new Vector3(-1.22f, 0f, -1.59f), new Vector3(1.22f, 2.9f, 1.59f), Mount.Floor);
        static readonly PackItem ApothecaryCabinet = new PackItem(Worlds + "ApothecaryCabinet", new Vector3(-0.57f, 0f, -0.27f), new Vector3(0.57f, 2.9f, 0.27f), Mount.Floor);

        /// <summary>Rooms whose ceiling lamps are chandeliers, and which.</summary>
        static readonly Dictionary<int, PackItem> ChandelierZones = new Dictionary<int, PackItem>
        {
            [3] = Chandelier, [10] = Chandelier, [14] = Chandelier, [17] = TempleChandelier,
        };

        /// <summary>What each room gets along its walls, by weight. Zones not listed keep just the barrels and crates.</summary>
        static readonly Dictionary<int, (PackItem item, int weight)[]> RoomSets = new Dictionary<int, (PackItem, int)[]>
        {
            [0] = new[] { (Barricade, 3), (Crate, 2) },                                                   // Gate Yard
            [1] = new[] { (Crate, 3), (Barricade, 1), (Cabinet, 2) },                                     // Depot
            [2] = new[] { (Worktable, 2), (Crate, 2), (Barricade, 1) },                                   // Garage (smithy)
            [3] = new[] { (Bench, 3), (Chair, 1), (Candles, 2), (Bones, 1) },                             // Chapel
            [5] = new[] { (GardenBench, 3), (Planter, 2), (Barricade, 1) },                               // Courtyard
            [4] = new[] { (Chair, 3), (BrokenTable, 2), (Sofa, 2), (Cabinet, 1), (Bench, 1) },            // Barracks
            [6] = new[] { (Barricade, 2), (Crate, 3) },                                                   // Loading Dock
            [7] = new[] { (Crate, 2), (Barricade, 1), (Cabinet, 1) },                                     // Pump Station
            [8] = new[] { (Barricade, 3), (Crate, 3) },                                                   // Crane Yard
            [9] = new[] { (Desk, 2), (Jars, 2), (Bones, 2), (Candles, 2), (Worktable, 2), (Pylon, 2) },   // Lantern Core
            [10] = new[] { (Bookcase, 4), (ReadingTable, 2), (Bookshelf, 1), (Cabinet, 1), (Chair, 1) },  // Archives
            [11] = new[] { (Planter, 3), (GardenBench, 2), (Crate, 1) },                                  // Greenhouse
            [12] = new[] { (GaolCell, 5), (Bones, 2), (Candles, 1), (Barricade, 1) },                     // Gaol
            [13] = new[] { (SickBed, 5), (ApothecaryCabinet, 2), (Candles, 1), (Crate, 1) },              // Plague Ward
            [14] = new[] { (Bookcase, 3), (ReadingTable, 2), (Candles, 2), (Cabinet, 1) },                // Scriptorium
            [15] = new[] { (Worktable, 2), (Jars, 2), (Crate, 2), (Barricade, 1) },                      // Alchemist's Furnace
            [16] = new[] { (Barricade, 3), (Crate, 2), (Bones, 1) },                                      // Rampart Ruins
            [17] = new[] { (LatticeScreen, 4), (Candles, 2), (Bench, 1) },                                // Sunken Temple
        };

        /// <summary>The big pieces, each against a wall of its room: the lab's altar, slab and desk, the chapel's altar dais and organ, the globe.</summary>
        static readonly (PackItem item, int zone)[] Centrepieces =
        {
            (Altar, LabZone), (Slab, LabZone), (Desk, LabZone), (AltarDais, 3), (Organ, 3), (Globe, 10), (Fountain, 11),
            (LecternStage, 14),
        };

        /// <summary>Rooms with torches on the walls (each a flickering light), and at most how many.</summary>
        static readonly Dictionary<int, int> TorchZones = new Dictionary<int, int>
        {
            [0] = 4, [1] = 3, [2] = 3, [3] = 4, [4] = 4, [5] = 4, [6] = 3, [7] = 3, [8] = 4, [9] = 6, [10] = 3, [11] = 2,
            [12] = 6, [13] = 4, [15] = 3, [16] = 4, [17] = 4,
        };

        const int LabZone = 9;
        static readonly Color TorchLight = new Color(1f, 0.58f, 0.25f);
        static readonly Color SpellLight = new Color(0.62f, 0.3f, 1f);

        void DressRooms()
        {
            // Each room's centrepieces first, so they get the pick of its walls.
            foreach (var (big, zone) in Centrepieces)
                for (int i = 0; i < _faces.Count && !PlaceOnFace(big, _faces[i], zone, new System.Random(4400 + i)); i++) { }

            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Length < 3f || !RoomSets.TryGetValue(f.Zone, out var set)) continue;
                var rng = new System.Random(8300 + i * 4111);
                int total = 0;
                foreach (var (_, w) in set) total += w;
                for (float t = 1.4f; t < f.Length - 1.4f; t += Range(rng, 1.8f, 3f))
                {
                    if (rng.NextDouble() > 0.4) continue;
                    int roll = rng.Next(total), k = 0;
                    while (roll >= set[k].weight) roll -= set[k++].weight;
                    TryPackItem(set[k].item, f, t, rng);
                }
            }

            WallHangings();
            HangTapestries();
            HangBanners();
            HangChains();
            SpellCircle();
            Physics.SyncTransforms();
        }

        bool PlaceOnFace(PackItem item, WallFace f, int zone, System.Random rng)
        {
            if (f.Zone != zone || f.Length < item.Size.x + 2f) return false;
            for (float t = item.Size.x / 2f + 0.8f; t < f.Length - item.Size.x / 2f - 0.8f; t += 1.5f)
                if (TryPackItem(item, f, t, rng)) return true;
            return false;
        }

        /// <summary>
        /// A floor item backed against a wall at t, if there's room for it and a clear lane in front. Low pieces
        /// (bone piles) stay walk-over; the rest is solid.
        /// </summary>
        bool TryPackItem(PackItem item, WallFace f, float t, System.Random rng)
        {
            var size = item.Size;
            var mid = f.At(t, 0f, size.z / 2f + 0.05f);
            foreach (var s in _propSpots)
                if ((s - mid).sqrMagnitude < 2.6f * 2.6f) return false;
            if (!WallFree(f, t, size.y / 2f + 0.06f, size.x / 2f + 0.1f, size.y / 2f, size.z + 0.1f)) return false;
            if (!FloorFree(f.At(t, 0f, size.z + 1.1f), 0.6f)) return false;
            _propSpots.Add(mid);
            // A little askew, as if shoved there in a hurry.
            var rot = Quaternion.LookRotation(f.Inward, Vector3.up) * Quaternion.Euler(0f, Range(rng, -6f, 6f), 0f);
            PlacePackItem(item, mid, rot, size.y > 0.5f);
            return true;
        }

        /// <summary>The item with its footprint centred on p (on the floor), solid if asked, the model or a stand-in.</summary>
        void PlacePackItem(PackItem item, Vector3 p, Quaternion rot, bool solid)
        {
            var size = item.Size;
            if (solid)
            {
                var go = new GameObject("Prop " + item.Path);
                go.transform.SetParent(Root, false);
                go.transform.localPosition = p + Vector3.up * size.y / 2f;
                go.transform.localRotation = rot;
                go.isStatic = true;
                go.AddComponent<BoxCollider>().size = size;
            }
            var model = PropModels.GetPack(item.Path, PackMaterial);
            if (model != null) AddModel(model, p, rot, true);
            else if (solid) Deco(p + Vector3.up * size.y / 2f, size, rot, PropWood, Surface.Wood);
        }

        /// <summary>Torches (with flickering light) and the trophy head on the walls of the lab, chapel and archives.</summary>
        void WallHangings()
        {
            var torch = PropModels.GetPack(Torch.Path, PackMaterial);
            var trophy = PropModels.GetPack(Trophy.Path, PackMaterial);
            var torches = new Dictionary<int, int>();
            bool trophyHung = false;
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (!TorchZones.TryGetValue(f.Zone, out int max) || f.Length < 4f) continue;
                var rng = new System.Random(9700 + i * 389);
                for (float t = 2f; t < f.Length - 2f; t += Range(rng, 5f, 8f))
                {
                    if (f.Zone == LabZone && !trophyHung && WallFree(f, t, 3.1f, 0.55f, 0.6f, 0.8f))
                    {
                        trophyHung = true;
                        if (trophy != null) AddModel(trophy, f.At(t, 2.5f, 0.6f), Quaternion.LookRotation(f.Inward, Vector3.up), true);
                        continue;
                    }
                    torches.TryGetValue(f.Zone, out int n);
                    if (n >= max || !WallFree(f, t, 2.6f, 0.3f, 0.4f, 0.4f)) continue;
                    torches[f.Zone] = n + 1;
                    var rot = Quaternion.LookRotation(f.Inward, Vector3.up);
                    if (torch != null) AddModel(torch, f.At(t, 2.25f, 0.2f), rot, false);
                    else Glow(f.At(t, 2.7f, 0.2f), new Vector3(0.12f, 0.16f, 0.12f), rot, TorchLight);
                    var light = new GameObject("Torch Light").AddComponent<Light>();
                    light.transform.SetParent(Root, false);
                    light.transform.localPosition = f.At(t, 2.85f, 0.45f);
                    light.type = LightType.Point;
                    light.color = TorchLight;
                    light.range = 7f;
                    light.intensity = 1.4f;
                    light.shadows = LightShadows.None;
                    light.gameObject.AddComponent<Flicker>().Seed = i * 13 + n;
                }
            }
        }

        /// <summary>Chains hanging from the lab's and the Gaol's ceilings, well above head height.</summary>
        void HangChains()
        {
            var chains = PropModels.GetPack(Chains.Path, PackMaterial);
            if (chains == null) return;
            var rng = new System.Random(5150);
            foreach (int zone in new[] { LabZone, 12 })
            foreach (var r in ZoneRects(zone))
            {
                if (r.width < 8 || r.height < 8) continue;
                for (int k = 0; k < 4; k++)
                {
                    var p = new Vector3(Range(rng, r.xMin + 2f, r.xMax - 2f), WallHeight - 0.2f, Range(rng, r.yMin + 2f, r.yMax - 2f));
                    if (new Vector2(p.x - ZoneAnchorsCore.x, p.z - ZoneAnchorsCore.z).sqrMagnitude < 3f * 3f) continue;
                    AddModelHanging(chains, p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                }
            }
        }

        static Vector3 ZoneAnchorsCore => L(LabZone, 0f, 0f, 0f);

        /// <summary>A model hung by its top at p.</summary>
        void AddModelHanging(PropModels.Model model, Vector3 p, Quaternion rot)
        {
            var b = model.Bounds;
            var m = Matrix4x4.TRS(p - rot * new Vector3(b.center.x, b.max.y, b.center.z), rot, Vector3.one);
            for (int s = 0; s < model.Materials.Length; s++) _decor.Add(model.Mesh, s, m, model.Materials[s], false);
        }

        /// <summary>The spell circle, turning slowly round the reactor's foot with a violet light.</summary>
        void SpellCircle()
        {
            var aura = PropModels.GetPack(Aura.Path, PackMaterial);
            if (aura == null) return;
            var go = new GameObject("Spell Circle");
            go.transform.SetParent(Root, false);
            go.transform.localPosition = L(LabZone, 0f, 0.02f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = aura.Mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = aura.Materials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Spell Light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = Vector3.up * 0.8f;
            light.type = LightType.Point;
            light.color = SpellLight;
            light.range = 8f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            go.AddComponent<SpellCircle>().Glow = light;
        }

        /// <summary>Materials for the packs' keys: Ten Worlds and lab materials by name, furniture by vertex colour ("#rrggbb").</summary>
        static Material PackMaterial(string key)
        {
            // The Ten Worlds pack names its materials in lower case (wood_dark, brass).
            if (key.Length > 0 && char.IsLower(key[0])) return WorldsMaterial(key);
            if (key.StartsWith("#") && ColorUtility.TryParseHtmlString(key, out var c))
            {
                // The sofa's cushions and foam and the books are cloth; other browns are wood, the greys metal fittings.
                bool cloth = key == "#a68b5f" || key == "#65584e" || key == "#48504c";
                bool brown = c.r > c.b + 0.06f;
                return Greybox.Lit(c, cloth ? Surface.Fabric : brown ? Surface.Wood : Surface.Metal);
            }
            return key switch
            {
                "StoneDark" => Greybox.Lit(new Color(0.22f, 0.21f, 0.23f), Surface.Concrete),
                "StoneMid" => Greybox.Lit(new Color(0.34f, 0.33f, 0.36f), Surface.Concrete),
                "StoneLight" => Greybox.Lit(new Color(0.47f, 0.46f, 0.45f), Surface.Concrete),
                "WoodDark" => Greybox.Lit(new Color(0.25f, 0.16f, 0.1f), Surface.Wood),
                "WoodMid" => Greybox.Lit(new Color(0.38f, 0.25f, 0.17f), Surface.Wood),
                "Bone" => Greybox.Lit(new Color(0.77f, 0.74f, 0.67f), Surface.Plaster),
                "Iron" => Greybox.Lit(new Color(0.23f, 0.24f, 0.26f), Surface.Metal),
                "IronRust" => Greybox.Lit(new Color(0.38f, 0.25f, 0.19f), Surface.Rust),
                "Brass" => Greybox.Lit(new Color(0.63f, 0.48f, 0.21f), Surface.Metal),
                "Flesh" => Greybox.Lit(new Color(0.59f, 0.39f, 0.36f), Surface.Skin),
                "Blood" => Greybox.Lit(new Color(0.32f, 0.03f, 0.05f), Surface.Plain),
                "Wax" => Greybox.Lit(new Color(0.83f, 0.76f, 0.59f), Surface.Plain),
                "Parchment" => Greybox.Lit(new Color(0.73f, 0.66f, 0.47f), Surface.Plaster),
                "ClothDark" => Greybox.Lit(new Color(0.27f, 0.09f, 0.11f), Surface.Fabric),
                "ClothPurple" => Greybox.Lit(new Color(0.32f, 0.16f, 0.41f), Surface.Fabric),
                "Glass" => Greybox.Unlit(new Color(0.33f, 0.63f, 0.57f, 0.3f)),
                "Formaldehyde" => Greybox.Unlit(new Color(0.32f, 0.7f, 0.47f, 0.45f)),
                "Flame" => Greybox.Unlit(new Color(1f, 0.64f, 0.16f)),
                "MagicPurple" => Greybox.Unlit(new Color(0.56f, 0.29f, 0.86f)),
                "MagicCyan" => Greybox.Unlit(new Color(0.3f, 0.82f, 0.93f)),
                "MagicRed" => Greybox.Unlit(new Color(0.82f, 0.18f, 0.28f)),
                _ => Greybox.Lit(new Color(0.4f, 0.4f, 0.4f), Surface.Plain),
            };
        }
    }

    /// <summary>A torch or candle light that wavers.</summary>
    public class Flicker : MonoBehaviour
    {
        public int Seed;
        Light _light;
        float _base;

        void Start()
        {
            _light = GetComponent<Light>();
            if (_light != null) _base = _light.intensity;
        }

        void Update()
        {
            if (_light == null) return;
            float n = Mathf.PerlinNoise(Time.time * 9f, Seed * 0.37f);
            _light.intensity = _base * (0.75f + n * 0.5f);
        }
    }

    /// <summary>The lab's spell circle: turns slowly and breathes, its light pulsing with it.</summary>
    public class SpellCircle : MonoBehaviour
    {
        public Light Glow;
        float _baseGlow;

        void Start()
        {
            if (Glow != null) _baseGlow = Glow.intensity;
        }

        void Update()
        {
            float t = Time.time;
            transform.Rotate(Vector3.up, 18f * Time.deltaTime, Space.Self);
            float pulse = 1f + Mathf.Sin(t * 2.5f) * 0.06f;
            transform.localScale = new Vector3(pulse, 1f, pulse);
            if (Glow != null) Glow.intensity = _baseGlow * (1f + Mathf.Sin(t * 3.2f) * 0.3f);
        }
    }
}
