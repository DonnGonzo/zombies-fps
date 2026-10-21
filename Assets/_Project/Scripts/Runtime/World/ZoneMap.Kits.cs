using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Ivan's three Quaternius kits (CC0), converted by tools/quaternius:
    ///   - the Fantasy Props MegaKit furnishes the medieval rooms: market stalls in the Gate Yard, a smithy's anvil,
    ///     grindstone and weapon racks in the Garage, bunks, chests and training dummies in the Barracks, bookcases and
    ///     lecterns in the Archives and Scriptorium, cauldrons and bottle shelves for the alchemist and the plague
    ///     doctors, cages in the Gaol, candle stands in the Chapel and Temple, wall shelves, peg racks, shields, banners
    ///     and lanterns on the walls, candle chandeliers, and cups, plates, books, potions and coins on the tables;
    ///   - the Modular SciFi MegaKit lines the Lantern Core with machine panels, pipe pilasters, deck plates and
    ///     strip lights round the reactor, as if something not of this age was built there;
    ///   - the Sci-Fi Essentials Kit (and the modular kit's props) fill the Lantern Core and the yards beside it with
    ///     what it left behind: consoles, lockers, shelving, sealed crates, barrels and a dish in the Crane Yard.
    ///
    /// Like the other packs, where things stand and their colliders never depend on the models being there.
    /// </summary>
    public partial class ZoneMap
    {
        const string Fantasy = "Fantasy/";
        const string SciFi = "SciFi/";
        const string Modular = "SciFiModular/";

        // The kits' models, their bounds in Unity axes (front +z). Declared in this file ahead of the tables below
        // that use them (static fields in different files of a partial class have no set order).
        static PackItem F(string name, float x0, float y0, float z0, float x1, float y1, float z1, Mount mount = Mount.Floor) =>
            new PackItem(Fantasy + name, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), mount);
        static PackItem S(string path, float x0, float y0, float z0, float x1, float y1, float z1, Mount mount = Mount.Floor) =>
            new PackItem(path, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), mount);

        static readonly PackItem AnvilLog = F("Anvil_Log", -0.52f, 0f, -0.35f, 0.41f, 1.07f, 0.47f);
        static readonly PackItem KitBarrel = F("Barrel", -0.35f, 0f, -0.35f, 0.35f, 0.9f, 0.35f);
        static readonly PackItem AppleBarrel = F("Barrel_Apples", -0.35f, 0f, -0.35f, 0.35f, 0.9f, 0.35f);
        static readonly PackItem BarrelRack = F("Barrel_Holder", -0.68f, 0f, -0.35f, 0.68f, 1.25f, 0.39f);
        static readonly PackItem Bunk1 = F("Bed_Twin1", -0.94f, 0f, -1.21f, 0.94f, 0.81f, 1.21f);
        static readonly PackItem Bunk2 = F("Bed_Twin2", -0.94f, 0f, -1.21f, 0.94f, 0.76f, 1.21f);
        static readonly PackItem LongBench = F("Bench", -1.39f, 0f, -0.26f, 1.39f, 0.53f, 0.27f);
        static readonly PackItem TallBookcase = F("Bookcase_2", -0.73f, 0f, -0.21f, 0.73f, 2.53f, 0.21f);
        static readonly PackItem BookStand = F("BookStand", -0.25f, 0f, -0.26f, 0.25f, 1.44f, 0.26f);
        static readonly PackItem Sideboard = F("Cabinet", -0.68f, 0f, -0.18f, 0.68f, 1f, 0.18f);
        static readonly PackItem Cage = F("Cage_Small", -0.42f, 0f, -0.42f, 0.43f, 0.76f, 0.46f);
        static readonly PackItem CandleStand = F("CandleStick_Stand", -0.37f, 0f, -0.37f, 0.37f, 1.3f, 0.37f);
        static readonly PackItem Cauldron = F("Cauldron", -0.49f, 0f, -0.47f, 0.49f, 0.81f, 0.47f);
        static readonly PackItem KitChair = F("Chair_1", -0.29f, 0f, -0.29f, 0.29f, 1.12f, 0.26f);
        static readonly PackItem WoodChest = F("Chest_Wood", -0.64f, 0f, -0.38f, 0.64f, 0.69f, 0.38f);
        static readonly PackItem IronCrate = F("Crate_Metal", -0.43f, 0f, -0.43f, 0.43f, 0.87f, 0.43f);
        static readonly PackItem WoodCrate = F("Crate_Wooden", -0.42f, 0f, -0.45f, 0.43f, 0.88f, 0.45f);
        static readonly PackItem Dummy = F("Dummy", -0.4f, 0f, -0.29f, 0.4f, 1.86f, 0.29f);
        static readonly PackItem AppleCrate = F("FarmCrate_Apple", -0.36f, 0f, -0.21f, 0.36f, 0.23f, 0.21f);
        static readonly PackItem Nightstand = F("Nightstand_Shelf", -0.35f, 0f, -0.19f, 0.35f, 1.21f, 0.2f);
        static readonly PackItem StallCart = F("Stall_Cart_Empty", -0.91f, 0f, -0.53f, 2.11f, 2.63f, 0.53f);
        static readonly PackItem Stall = F("Stall_Empty", -0.92f, 0f, -0.46f, 0.92f, 2.62f, 0.47f);
        static readonly PackItem KitStool = F("Stool", -0.24f, 0f, -0.24f, 0.24f, 0.58f, 0.24f);
        static readonly PackItem LongTable = F("Table_Large", -1.42f, 0f, -0.54f, 1.42f, 0.81f, 0.55f);
        static readonly PackItem BigVase = F("Vase_2", -0.35f, 0f, -0.35f, 0.35f, 0.52f, 0.35f);
        static readonly PackItem SmallVase = F("Vase_4", -0.18f, 0f, -0.18f, 0.18f, 0.49f, 0.18f);
        static readonly PackItem WeaponRack = F("WeaponStand", -0.69f, 0f, -0.49f, 0.69f, 1.11f, 0.49f);
        static readonly PackItem Grindstone = F("Whetstone", -0.58f, 0f, -0.37f, 0.56f, 1.17f, 0.53f);
        static readonly PackItem Workbench = F("Workbench", -1.01f, 0f, -0.51f, 1.01f, 0.89f, 0.51f);
        static readonly PackItem Sack = F("Bag", -0.33f, 0f, -0.24f, 0.33f, 0.8f, 0.3f);
        static readonly PackItem Pail = F("Bucket_Wooden_1", -0.22f, 0f, -0.2f, 0.22f, 0.29f, 0.18f);
        static readonly PackItem IronPail = F("Bucket_Metal", -0.24f, 0f, -0.21f, 0.24f, 0.36f, 0.24f);
        static readonly PackItem StewPot = F("Pot_1_Lid", -0.27f, 0f, -0.24f, 0.27f, 0.26f, 0.24f);

        // On the walls: y0 is how far below its hanging point each reaches.
        static readonly PackItem ArchShelf = F("Shelf_Arch", -0.62f, -0.61f, 0f, 0.62f, 0.97f, 0.31f, Mount.Wall);
        static readonly PackItem BottleShelf = F("Shelf_Small_Bottles", -0.57f, -0.02f, 0.01f, 0.57f, 0.63f, 0.29f, Mount.Wall);
        static readonly PackItem PlankShelf = F("Shelf_Simple", -0.59f, -0.2f, 0.04f, 0.59f, 0.11f, 0.34f, Mount.Wall);
        static readonly PackItem PegRack = F("Peg_Rack", -0.59f, -0.17f, 0f, 0.59f, 0.17f, 0.1f, Mount.Wall);
        static readonly PackItem WallShield = F("Shield_Wooden", -0.31f, -0.31f, 0f, 0.31f, 0.31f, 0.18f, Mount.Wall);
        static readonly PackItem IronLantern = F("Lantern_Wall", -0.18f, 0.08f, -0.05f, 0.18f, 1.42f, 1.25f, Mount.Wall);
        static readonly PackItem HangingBanner1 = F("Banner_1", -0.09f, -1.55f, 0f, 0.1f, 0.84f, 1.61f, Mount.Wall);
        static readonly PackItem HangingBanner2 = F("Banner_2", -0.09f, -1.23f, 0f, 0.1f, 0.84f, 1.61f, Mount.Wall);
        static readonly PackItem WallTorch = F("Torch_Metal", -0.11f, -0.28f, 0f, 0.11f, 0.37f, 0.39f, Mount.Wall);
        static readonly PackItem CandleChandelier = F("Chandelier", -0.65f, -1.42f, -0.65f, 0.65f, 0.04f, 0.65f, Mount.Open);

        // Small things for table tops and floors (walk-over, no colliders).
        static readonly string[] TavernTop = { "Mug", "Mug", "Table_Plate", "Candle_1", "Candle_2", "CandleStick", "Chalice", "Bottle_1", "Coin_Pile", "Pouch_Large" };
        static readonly string[] LibraryTop = { "Book_5", "Book_7", "Book_Stack_1", "Book_Stack_2", "Scroll_1", "Scroll_2", "Candle_2", "CandleStick", "CandleStick_Triple", "BookGroup_Small_1", "BookGroup_Small_2", "BookGroup_Small_3" };
        static readonly string[] AlchemyTop = { "Potion_1", "Potion_2", "Potion_4", "Bottle_1", "SmallBottles_1", "Book_5", "Candle_1", "CandleStick", "Scroll_2" };
        static readonly string[] HoardTop = { "Coin_Pile", "Coin_Pile_2", "Chalice", "Pouch_Large", "Candle_2", "Book_7" };
        static readonly string[] MachineTop = { SciFi + "Prop_Mug", SciFi + "Prop_HealthPack_Tube", SciFi + "Prop_Grenade", SciFi + "Prop_Ammo_Small" };

        static readonly PackItem SfBarrel = S(SciFi + "Prop_Barrel1", -0.36f, 0f, -0.32f, 0.36f, 1.11f, 0.32f);
        static readonly PackItem SfDrum = S(SciFi + "Prop_Barrel2_Closed", -0.27f, 0f, -0.27f, 0.27f, 0.77f, 0.27f);
        static readonly PackItem SfDrumOpen = S(SciFi + "Prop_Barrel2_Open", -0.27f, 0f, -0.27f, 0.27f, 1.1f, 0.27f);
        static readonly PackItem SfChair = S(SciFi + "Prop_Chair", -0.44f, 0f, -0.49f, 0.44f, 1.63f, 0.63f);
        static readonly PackItem SfChest = S(SciFi + "Prop_Chest", -0.92f, 0f, -0.44f, 0.92f, 0.95f, 0.54f);
        static readonly PackItem SfCrate = S(SciFi + "Prop_Crate", -0.78f, 0f, -0.75f, 0.78f, 1.5f, 0.75f);
        static readonly PackItem SfCrateLong = S(SciFi + "Prop_Crate_Large", -1.73f, 0f, -0.75f, 1.73f, 1.5f, 0.75f);
        static readonly PackItem SfTarp = S(SciFi + "Prop_Crate_Tarp", -0.86f, 0f, -0.86f, 0.86f, 1.56f, 0.86f);
        static readonly PackItem SfTarpLong = S(SciFi + "Prop_Crate_Tarp_Large", -1.61f, 0f, -0.86f, 1.61f, 1.56f, 0.86f);
        static readonly PackItem SfDesk = S(SciFi + "Prop_Desk_Medium", -1.42f, 0f, -0.53f, 1.42f, 0.94f, 0.5f);
        static readonly PackItem SfDeskSmall = S(SciFi + "Prop_Desk_Small", -1.03f, 0f, -0.53f, 1.03f, 0.94f, 0.5f);
        static readonly PackItem SfLocker = S(SciFi + "Prop_Locker", -0.49f, 0f, -0.23f, 0.49f, 2.67f, 0.27f);
        static readonly PackItem SfShelfTall = S(SciFi + "Prop_Shelves_ThinTall", -0.69f, 0f, -0.27f, 0.69f, 2.67f, 0.27f);
        static readonly PackItem SfShelfWide = S(SciFi + "Prop_Shelves_WideTall", -0.99f, 0f, -0.27f, 0.99f, 2.67f, 0.27f);
        static readonly PackItem SfShelfLow = S(SciFi + "Prop_Shelves_WideShort", -0.99f, 0f, -0.27f, 0.99f, 1.21f, 0.27f);
        static readonly PackItem SfDish = S(SciFi + "Prop_SatelliteDish", -0.92f, 0f, -1.29f, 0.92f, 5.76f, 1.49f);
        static readonly PackItem SfAmmo = S(SciFi + "Prop_Ammo_Closed", -0.29f, 0f, -0.36f, 0.29f, 0.42f, 0.36f);
        static readonly PackItem Console = S(Modular + "Prop_Computer", -0.37f, 0f, -0.22f, 0.37f, 1.59f, 0.35f);
        static readonly PackItem TechBarrel = S(Modular + "Prop_Barrel_Large", -0.25f, 0f, -0.27f, 0.25f, 1.11f, 0.27f);
        static readonly PackItem TechChest = S(Modular + "Prop_Chest", -0.75f, 0f, -0.36f, 0.75f, 0.73f, 0.39f);
        static readonly PackItem TechCrate = S(Modular + "Prop_Crate3", -0.5f, 0f, -0.5f, 0.5f, 1f, 0.5f);
        static readonly PackItem TechCrateBig = S(Modular + "Prop_Crate4", -0.56f, 0f, -0.56f, 0.56f, 1.12f, 0.56f);
        static readonly PackItem ItemHolder = S(Modular + "Prop_ItemHolder", -0.41f, 0f, -0.42f, 0.41f, 0.43f, 0.31f);
        static readonly PackItem PipeRack = S(Modular + "Prop_PipeHolder", -2.05f, 0f, -0.58f, 2.06f, 0.94f, 0.58f);
        static readonly PackItem PipeColumn = S(Modular + "Column_Pipes", -0.44f, 0f, -0.44f, 0.44f, 5f, 0.44f);

        static readonly string[] PanelWalls = { "WallBand_Straight", "WallAstra_Straight_Flat", "WallWindow_Straight", "WallAstra_Straight_Flat_Window", "WallBand_Straight" };
        static readonly string[] PanelTops = { "TopAstra_Straight", "TopCables_Straight", "TopPlastic_Straight" };
        static readonly string[] DeckPlates = { "Platform_Metal2", "Platform_Squares", "Platform_DarkPlates" };

        /// <summary>What the kits add along each room's walls, by weight (with the room's own set in ZoneMap.Rooms.cs).</summary>
        static readonly Dictionary<int, (PackItem item, int weight)[]> KitSets = new Dictionary<int, (PackItem, int)[]>
        {
            [0] = new[] { (Stall, 2), (StallCart, 2), (KitBarrel, 2), (WoodCrate, 2), (AppleCrate, 1), (AppleBarrel, 1), (Sack, 1) },           // Gate Yard: a market
            [1] = new[] { (WoodCrate, 3), (IronCrate, 2), (KitBarrel, 2), (BarrelRack, 2), (WoodChest, 1), (Sack, 2) },                           // Depot: stores
            [2] = new[] { (AnvilLog, 2), (Grindstone, 2), (Workbench, 2), (WeaponRack, 1), (KitBarrel, 1), (IronPail, 1) },                      // Garage: the smithy
            [4] = new[] { (Bunk1, 2), (Bunk2, 2), (WoodChest, 2), (WeaponRack, 2), (Dummy, 1), (Nightstand, 1), (LongBench, 1) },               // Barracks
            [5] = new[] { (BigVase, 2), (LongBench, 2), (KitBarrel, 1), (Stall, 1) },                                                            // Courtyard
            [6] = new[] { (SfCrate, 2), (SfCrateLong, 1), (SfTarp, 2), (SfTarpLong, 1), (SfBarrel, 2), (SfAmmo, 1), (WoodCrate, 1) },             // Loading Dock: salvage
            [7] = new[] { (SfDrum, 2), (SfDrumOpen, 1), (SfShelfLow, 1), (SfLocker, 1), (TechBarrel, 2), (IronPail, 1) },                       // Pump Station
            [8] = new[] { (SfTarpLong, 1), (SfCrate, 2), (TechCrateBig, 2), (TechCrate, 1), (BarrelRack, 1), (WoodCrate, 1) },                  // Crane Yard
            [9] = new[] { (Console, 3), (SfChair, 1), (SfDeskSmall, 1), (SfShelfTall, 1), (SfLocker, 1), (TechChest, 1), (TechBarrel, 1), (ItemHolder, 1) },  // Lantern Core
            [10] = new[] { (TallBookcase, 2), (BookStand, 1) },                                                                                         // Archives
            [12] = new[] { (Cage, 2), (Pail, 1), (KitStool, 1) },                                                                                // Gaol
            [13] = new[] { (Bunk2, 2), (Nightstand, 2), (Cauldron, 1), (CandleStand, 1) },                                                       // Plague Ward
            [14] = new[] { (TallBookcase, 2), (BookStand, 2), (CandleStand, 1) },                                                                // Scriptorium
            [15] = new[] { (Cauldron, 2), (Workbench, 1), (KitBarrel, 1), (StewPot, 1), (IronCrate, 1) },                                        // Alchemist's Furnace
            [16] = new[] { (WeaponRack, 2), (KitBarrel, 1), (WoodCrate, 1), (Dummy, 1), (WoodChest, 1) },                                        // Rampart Ruins
        };

        /// <summary>The kits' big pieces, one each against a wall of its room.</summary>
        static readonly (PackItem item, int zone)[] KitCentrepieces =
        {
            (StallCart, 0), (LongTable, 4), (SfDish, 8), (PipeRack, LabZone), (SfDesk, LabZone),
            (Cauldron, 13), (Workbench, 15),
        };

        /// <summary>Tables that get things on top: the height of the top, and which mix.</summary>
        static readonly Dictionary<string, float> TableTops = new Dictionary<string, float>
        {
            [LongTable.Path] = 0.81f, [Workbench.Path] = 0.89f, [Worlds + "ReadingTable"] = 0.82f, [Worlds + "WritingDesk"] = 0.79f,
            [SfDesk.Path] = 0.94f, [SfDeskSmall.Path] = 0.94f,
        };

        static string[] TopMix(int zone, string table) => table.StartsWith(SciFi) ? MachineTop : zone switch
        {
            3 or 10 or 14 => LibraryTop,
            9 or 13 or 15 => AlchemyTop,
            1 or 16 or 17 => HoardTop,
            _ => TavernTop,
        };

        /// <summary>What hangs on each room's walls: the item, the height it hangs from, and at most how many.</summary>
        static readonly Dictionary<int, (PackItem item, float height, int max)[]> WallKit = new Dictionary<int, (PackItem, float, int)[]>
        {
            [0] = new[] { (HangingBanner1, 5.6f, 2), (IronLantern, 2.4f, 3), (WallShield, 2.5f, 2) },
            [1] = new[] { (PegRack, 1.8f, 2), (PlankShelf, 1.6f, 2) },
            [2] = new[] { (PegRack, 1.8f, 3), (WallShield, 2.4f, 2), (PlankShelf, 1.7f, 1) },
            [4] = new[] { (WallShield, 2.4f, 4), (PegRack, 1.8f, 3), (HangingBanner2, 5.6f, 2), (PlankShelf, 1.6f, 2) },
            [5] = new[] { (IronLantern, 2.4f, 3), (HangingBanner1, 5.6f, 2) },
            [8] = new[] { (IronLantern, 2.4f, 2) },
            [10] = new[] { (ArchShelf, 1.6f, 3), (PlankShelf, 1.6f, 3) },
            [12] = new[] { (PegRack, 1.8f, 2), (IronLantern, 2.4f, 2) },
            [13] = new[] { (BottleShelf, 1.3f, 3), (ArchShelf, 1.6f, 2) },
            [14] = new[] { (ArchShelf, 1.6f, 3), (PlankShelf, 1.6f, 2) },
            [15] = new[] { (BottleShelf, 1.3f, 4), (PlankShelf, 1.6f, 2) },
            [16] = new[] { (WallShield, 2.4f, 3), (HangingBanner2, 5.6f, 2), (IronLantern, 2.4f, 2) },
        };

        /// <summary>Walk-over clutter on each room's floor, and how many.</summary>
        static readonly Dictionary<int, (string[] items, int count)> FloorKit = new Dictionary<int, (string[], int)>
        {
            [0] = (new[] { Fantasy + "Rope_1", Fantasy + "Vase_Rubble_Medium", Fantasy + "Bucket_Wooden_1" }, 5),
            [1] = (new[] { Fantasy + "Rope_2", Fantasy + "Rope_3", Fantasy + "Bucket_Wooden_1" }, 5),
            [2] = (new[] { Fantasy + "Chain_Coil", Fantasy + "Bucket_Metal", Fantasy + "Rope_1" }, 4),
            [4] = (new[] { Fantasy + "Book_5", Fantasy + "Mug", Fantasy + "Coin_Pile" }, 4),
            [5] = (new[] { Fantasy + "Vase_Rubble_Medium", Fantasy + "Rope_2" }, 4),
            [6] = (new[] { SciFi + "Prop_Ammo_Small", Modular + "Prop_Cable_1", Fantasy + "Rope_3" }, 5),
            [7] = (new[] { Modular + "Prop_Cable_1", Modular + "Prop_Cable_3", Fantasy + "Bucket_Metal" }, 5),
            [8] = (new[] { Fantasy + "Chain_Coil", Fantasy + "Rope_3", Modular + "Prop_Cable_1" }, 5),
            [9] = (new[] { Modular + "Prop_Cable_1", Modular + "Prop_Cable_3", Modular + "Prop_Light_Floor" }, 7),
            [10] = (new[] { Fantasy + "Book_5", Fantasy + "Book_7", Fantasy + "Scroll_1", Fantasy + "Book_Stack_1", Fantasy + "Scroll_2", Fantasy + "Candle_2" }, 12),
            [12] = (new[] { Fantasy + "Chain_Coil", Fantasy + "Bucket_Wooden_1", Fantasy + "Vase_Rubble_Medium" }, 5),
            [13] = (new[] { Fantasy + "Bucket_Wooden_1", Fantasy + "Bottle_1", Fantasy + "Potion_2" }, 5),
            [14] = (new[] { Fantasy + "Scroll_2", Fantasy + "Book_7", Fantasy + "Book_Stack_2" }, 5),
            [15] = (new[] { Fantasy + "Bucket_Metal", Fantasy + "Pot_1_Lid", Fantasy + "Potion_4" }, 4),
            [16] = (new[] { Fantasy + "Vase_Rubble_Medium", Fantasy + "Rope_1", Fantasy + "Chain_Coil" }, 5),
        };

        /// <summary>Rooms whose ceiling lamps are the kit's candle chandeliers.</summary>
        static readonly Dictionary<int, PackItem> KitChandeliers = new Dictionary<int, PackItem>
        {
            [4] = CandleChandelier, [10] = CandleChandelier, [12] = CandleChandelier, [13] = CandleChandelier, [15] = CandleChandelier,
        };

        static readonly Dictionary<int, (PackItem item, int weight)[]> s_MergedSets = new Dictionary<int, (PackItem, int)[]>();

        /// <summary>A room's wall set: its own (ZoneMap.Rooms.cs) and the kits', or null.</summary>
        static (PackItem item, int weight)[] RoomSet(int zone)
        {
            if (s_MergedSets.TryGetValue(zone, out var set)) return set;
            var list = new List<(PackItem, int)>();
            if (RoomSets.TryGetValue(zone, out var own)) list.AddRange(own);
            if (KitSets.TryGetValue(zone, out var kit)) list.AddRange(kit);
            set = list.Count > 0 ? list.ToArray() : null;
            s_MergedSets[zone] = set;
            return set;
        }

        // ---------------------------------------------------------------- materials

        static readonly Dictionary<string, Material> s_KitMaterials = new Dictionary<string, Material>();

        /// <summary>
        /// A kit material from its key (see tools/quaternius/pack_gltf.py): Q|albedo|normal|rrggbb|metallic|gloss|flags.
        /// </summary>
        static Material KitMaterial(string key)
        {
            if (s_KitMaterials.TryGetValue(key, out var cached) && cached != null) return cached;
            var f = key.Split('|');
            ColorUtility.TryParseHtmlString("#" + f[3], out var tint);
            string flags = f.Length > 6 ? f[6] : "";
            Material mat;
            if (flags.Contains("L")) mat = Greybox.Unlit(tint.maxColorComponent > 0.2f ? tint : new Color(0.82f, 0.93f, 1f));
            else if (flags.Contains("G")) mat = Greybox.Unlit(new Color(0.35f, 0.55f, 0.6f, 0.35f));
            else if (flags.Contains("S")) mat = Greybox.Unlit(new Color(0.25f, 0.75f, 0.85f));
            else if (f[1] == "-") mat = Greybox.Lit(tint, tint.maxColorComponent < 0.15f ? Surface.Metal : Surface.Plain);
            else
            {
                var (metallic, gloss) = KitFinish(f[1]);
                mat = PropModels.Textured("Kit " + f[1],
                    Resources.Load<Texture2D>($"CC0/Quaternius/Textures/{f[1]}_albedo"),
                    f[2] == "-" ? null : Resources.Load<Texture2D>($"CC0/Quaternius/Textures/{f[2]}_normal"), metallic, gloss);
                mat.color = tint;
                // Kenney's trees and iron fence are pictures on crossed planes, cut out by their alpha. The cutout
                // base is a material asset so its shader variant ships in builds.
                var cutout = f[1] == "kr_tree" ? Resources.Load<Material>("Materials/PropCutout") : null;
                if (cutout != null)
                {
                    var tex = mat.mainTexture;
                    mat = new Material(cutout) { name = "Kit " + f[1], mainTexture = tex, color = tint, enableInstancing = true };
                }
            }
            s_KitMaterials[key] = mat;
            return mat;
        }

        /// <summary>
        /// Each texture set's metal and gloss, from the kits' ORM maps but toned down: there are no reflection probes,
        /// so full metal reads black.
        /// </summary>
        static (float metallic, float gloss) KitFinish(string albedo) =>
            albedo.Contains("Trim_Metal") ? (0.7f, 0.38f)
            : albedo.Contains("Cables") ? (0.1f, 0.5f)
            : albedo.Contains("Trim_03") ? (0.05f, 0.45f)
            : albedo.Contains("PaddedWall") ? (0f, 0.35f)
            : albedo.StartsWith("fp_") ? (albedo.Contains("Props") ? 0.1f : 0f, albedo.Contains("Cloth") ? 0.05f : 0.2f)
            : albedo.StartsWith("kr_") ? (0f, 0.15f)
            : (0.45f, 0.48f);

        // ---------------------------------------------------------------- placing

        /// <summary>The kits' centrepieces, wall hangings and floor clutter (after the rooms' own furniture).</summary>
        void DressKits()
        {
            foreach (var (big, zone) in KitCentrepieces)
                for (int i = 0; i < _faces.Count && !PlaceOnFace(big, _faces[i], zone, new System.Random(5200 + i)); i++) { }
            HangWallKit();
            ScatterFloorKit();
        }

        /// <summary>
        /// Stand-in colliders for the kit's wall hangings while the room is dressed, so the torches, tapestries and
        /// banners hung after them keep clear; removed once the dressing is done (they'd only snag players).
        /// </summary>
        Transform _hangers;

        void HangWallKit()
        {
            _hangers = Greybox.Empty("Kit Hangers", Root, Vector3.zero);
            var counts = new Dictionary<(int, string), int>();
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (f.Length < 3f || f.Zone == DepotZone || f.Zone == LoadingDockZone || f.Zone == GarageZone || f.Zone == CourtyardZone || f.Zone == PumpZone || f.Zone == Rampart || !WallKit.TryGetValue(f.Zone, out var set)) continue;
                var rng = new System.Random(3300 + i * 571);
                for (float t = Range(rng, 1.5f, 3f); t < f.Length - 1.5f; t += Range(rng, 2.5f, 4.5f))
                {
                    var (item, height, max) = set[rng.Next(set.Length)];
                    counts.TryGetValue((f.Zone, item.Path), out int n);
                    if (n >= max) continue;
                    var size = item.Size;
                    float bottom = height + item.Min.y;
                    if (!WallFree(f, t, bottom + size.y / 2f, size.x / 2f + 0.15f, size.y / 2f + 0.05f, size.z + 0.05f)) continue;
                    var model = PropModels.GetPack(item.Path, PackMaterial);
                    if (model == null) return;
                    counts[(f.Zone, item.Path)] = n + 1;
                    AddModel(model, f.At(t, bottom, size.z / 2f + 0.01f), f.Facing, false);
                    var stand = new GameObject("Hanger");
                    stand.transform.SetParent(_hangers, false);
                    stand.transform.localPosition = f.At(t, bottom + size.y / 2f, size.z / 2f + 0.01f);
                    stand.transform.localRotation = f.Facing;
                    stand.AddComponent<BoxCollider>().size = size;
                    if (item.Path == PlankShelf.Path) ShelfBooks(f, t, height, rng);
                }
            }
            Physics.SyncTransforms();
        }

        /// <summary>A few books, bottles or scrolls along a wall shelf.</summary>
        void ShelfBooks(WallFace f, float t, float height, System.Random rng)
        {
            float top = height + 0.11f;
            string[] mix = f.Zone == 13 || f.Zone == 15 ? AlchemyTop : LibraryTop;
            for (float x = -0.4f; x <= 0.4f; x += Range(rng, 0.25f, 0.4f))
            {
                if (rng.NextDouble() < 0.3) continue;
                var model = PropModels.GetPack(Fantasy + mix[rng.Next(mix.Length)], PackMaterial);
                if (model == null || model.Bounds.size.z > 0.3f) continue;
                AddModel(model, f.At(t + x, top, 0.2f), f.Facing * Quaternion.Euler(0f, Range(rng, -20f, 20f), 0f), false);
            }
        }

        /// <summary>Things on a table that was just placed at p, facing rot, its top this high.</summary>
        void TableTop(PackItem table, Vector3 p, Quaternion rot, int zone, System.Random rng)
        {
            if (!TableTops.TryGetValue(table.Path, out float top)) return;
            var mix = TopMix(zone, table.Path);
            var size = table.Size;
            float hx = size.x / 2f - 0.18f, hz = size.z / 2f - 0.14f;
            int count = Mathf.Clamp(Mathf.RoundToInt(size.x * 1.5f), 1, 5);
            var used = new List<Vector2>();
            for (int k = 0; k < count * 2 && used.Count < count; k++)
            {
                var spot = new Vector2(Range(rng, -hx, hx), Range(rng, -hz, hz));
                bool clear = true;
                foreach (var u in used) clear &= (u - spot).sqrMagnitude > 0.3f * 0.3f;
                if (!clear) continue;
                string path = mix[rng.Next(mix.Length)];
                var model = PropModels.GetPack(path.Contains("/") ? path : Fantasy + path, PackMaterial);
                if (model == null) return;
                used.Add(spot);
                AddModel(model, p + rot * new Vector3(spot.x, top, spot.y), rot * Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), false);
            }
        }

        void ScatterFloorKit()
        {
            foreach (var kv in FloorKit)
            {
                int zone = kv.Key;
                var (items, count) = kv.Value;
                var rng = new System.Random(6600 + zone * 97);
                var rects = ZoneRects(zone);
                int placed = 0;
                for (int tries = 0; tries < count * 12 && placed < count; tries++)
                {
                    var r = rects[rng.Next(rects.Length)];
                    var p = new Vector3(Range(rng, r.xMin + 1.2f, r.xMax - 1.2f), 0f, Range(rng, r.yMin + 1.2f, r.yMax - 1.2f));
                    if (!FloorFree(p, 0.7f)) continue;
                    var model = PropModels.GetPack(items[rng.Next(items.Length)], PackMaterial);
                    if (model == null) return;
                    placed++;
                    AddModel(model, p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), false);
                }
            }
        }

        // ---------------------------------------------------------------- the Lantern Core's machine shell

        /// <summary>
        /// Lines the Lantern Core with the modular kit: wall panels to 3 m, two rows of upper panels to the ceiling,
        /// pipe pilasters between bays, and deck plates over the floor. Done before the furniture, so it keeps clear
        /// of the pilasters. The lower panel is left off wherever something players use is on the wall.
        /// </summary>
        void LabShell()
        {
            var rng = new System.Random(7300);
            if (PropModels.GetPack(Modular + PanelWalls[0], PackMaterial) == null) return;
            foreach (var f in _faces)
            {
                if (f.Zone != LabZone || f.Length < 2.5f) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(f.Length / 4f));
                float w = f.Length / n;
                for (int k = 0; k < n; k++)
                {
                    float t = (k + 0.5f) * w;
                    if (!WallSpotNear(f.At(t, 0f, 0f), w / 2f + 0.9f)) Panel(Modular + PanelWalls[rng.Next(PanelWalls.Length)], f, t, w, 0f);
                    Panel(Modular + PanelTops[rng.Next(PanelTops.Length)], f, t, w, 0f);
                    Panel(Modular + PanelTops[0], f, t, w, 2f);
                    // A pipe pilaster at the joint before this bay, where nothing needs the wall.
                    if (k == 0) continue;
                    var at = f.At(k * w, 0f, PipeColumn.Size.z / 2f + 0.02f);
                    if (WallSpotNear(at, 1.6f) || !KeptClear(at, 0.6f) || Solid(at + Vector3.up * 1.5f, new Vector3(0.5f, 1.2f, 0.5f), f.Facing)) continue;
                    PlacePackItem(PipeColumn, at, f.Facing, true);
                    _propSpots.Add(at);
                }
            }
            // Deck plates over the floor, a darker ring of them round the edge.
            foreach (var r in ZoneRects(LabZone))
            {
                int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / 4f)), nz = Mathf.Max(1, Mathf.RoundToInt(r.height / 4f));
                for (int a = 0; a < nx; a++)
                    for (int b = 0; b < nz; b++)
                    {
                        bool edge = a == 0 || b == 0 || a == nx - 1 || b == nz - 1;
                        var model = PropModels.GetPack(Modular + (edge ? DeckPlates[2] : DeckPlates[(a + b) % 2]), PackMaterial);
                        if (model == null) continue;
                        var c = new Vector3(r.xMin + (a + 0.5f) * r.width / nx, 0.006f, r.yMin + (b + 0.5f) * r.height / nz);
                        var m = Matrix4x4.TRS(c, Quaternion.identity, new Vector3(r.width / nx / 4f, 1f, r.height / nz / 4f));
                        for (int s = 0; s < model.Materials.Length; s++) _decor.Add(model.Mesh, s, m, model.Materials[s], false);
                    }
            }
        }

        /// <summary>A wall panel stretched to this width, its back on the face at t, raised by lift.</summary>
        void Panel(string path, WallFace f, float t, float width, float lift)
        {
            var model = PropModels.GetPack(path, PackMaterial);
            if (model == null) return;
            var b = model.Bounds;
            var m = Matrix4x4.TRS(f.At(t, lift, 0.004f), f.Facing, new Vector3(width / b.size.x, 1f, 1f)) *
                    Matrix4x4.Translate(new Vector3(-b.center.x, 0f, -b.min.z));
            for (int s = 0; s < model.Materials.Length; s++) _decor.Add(model.Mesh, s, m, model.Materials[s], false);
        }

        /// <summary>True when a window, wall buy, perk, generator or bench is on the wall within reach of p.</summary>
        static bool WallSpotNear(Vector3 p, float reach)
        {
            bool Near(WallSpot s)
            {
                var (face, _) = s.Resolve();
                return new Vector2(face.x - p.x, face.z - p.z).sqrMagnitude < reach * reach;
            }
            foreach (var s in WindowSpots) if (Near(s)) return true;
            foreach (var s in WallBuySpots) if (Near(s)) return true;
            foreach (var s in PerkSpots) if (Near(s)) return true;
            foreach (var s in GeneratorSpots) if (Near(s)) return true;
            return Near(ArmorSpot) || Near(RerollSpot);
        }

        /// <summary>A strip light on the ceiling in place of the lantern, in the Lantern Core.</summary>
        bool CeilingStrip(Vector3 position, int zone)
        {
            if (zone != LabZone) return false;
            var strip = PropModels.GetPack(Modular + "Prop_Light_Wide", PackMaterial);
            if (strip == null) return false;
            AddModelHanging(strip, position + Vector3.up * 0.3f, Quaternion.identity);
            return true;
        }
    }
}
