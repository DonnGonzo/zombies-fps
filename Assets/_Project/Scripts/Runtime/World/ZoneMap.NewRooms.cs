using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The six rooms made from the rest of Ivan's Ten Worlds pack, restyled for the map:
    ///   Gaol (from the quarantine block): a vaulted cell block under the old gate, cells along the walls.
    ///   Plague Ward (from the hospital ward): sickbeds in curtained bays down a timber-posted hall.
    ///   Scriptorium (from the lecture hall): rows of writing desks facing a lectern stage, bookcases round the walls.
    ///   Alchemist's Furnace (from the reactor control room): a great copper furnace with its flue to the roof.
    ///   Rampart Ruins (from the parkour gauntlet): a climb over fallen masonry up onto a broken battlement.
    ///   Sunken Temple (from the prayer hall): a drowned serpent shrine, dressed in ZoneMap.Temple.cs.
    ///
    /// Everything solid here is placed the same with or without the art; the models only change the look.
    /// </summary>
    public partial class ZoneMap
    {
        const int Gaol = 12, PlagueWard = 13, Scriptorium = 14, Furnace = 15, Rampart = 16, Temple = 17;

        static readonly Color Stone = new Color(0.4f, 0.39f, 0.36f);
        static readonly Color Timber = new Color(0.3f, 0.21f, 0.13f);
        static readonly Color FurnaceGlow = new Color(1f, 0.45f, 0.15f);

        void BuildNewRooms()
        {
            BuildGaol();
            BuildPlagueWard();
            BuildScriptorium();
            BuildFurnace();

            BuildRampartRuins();

            // Sunken Temple: the causeway, colonnade, ruins and the stepped dais (ZoneMap.Temple.cs).
            BuildTemple();
        }

        /// <summary>The new rooms' colliderless dressing.</summary>
        void DressNewRooms()
        {
            GaolMakeover();
            PlagueWardMakeover();
            ScriptoriumMakeover();
            FurnaceMakeover();
            RampartMakeover();
        }
    }
}
