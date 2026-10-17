using UnityEngine;
using ZombiesFPS.Data;
using ZombiesFPS.World;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Shared data every machine loads identically at startup (balance assets, network prefabs,
    /// the locally built map). Host and clients read the same values, so only state is networked.
    /// </summary>
    public static class GameConfig
    {
        public const int MaxPlayers = 4;
        public const ushort DefaultPort = 7777;

        /// <summary>The gun everyone starts with. Always gun id 0.</summary>
        public static GunData Gun;
        /// <summary>The walker, also ZombieTypes[0].</summary>
        public static ZombieData Zombie;
        /// <summary>Every zombie type, indexed by ZombieKind.</summary>
        public static ZombieData[] ZombieTypes;
        public static RoundData Round;
        public static PriceTable Prices;
        public static UpgradeData Upgrades;
        public static MissionData Missions;
        /// <summary>Every gun in the game; a gun's index is its network id.</summary>
        public static GunData[] Guns;

        public static GameObject PlayerPrefab;
        public static GameObject ZombiePrefab;
        public static GameObject DirectorPrefab;

        public static ZoneMap Map;
        public static Vector3 PlayerStart;

        /// <summary>Where player slot 0-3 starts: side by side at the map's player start.</summary>
        public static Vector3 PlayerSpawn(int slot) => PlayerStart + new Vector3((slot % MaxPlayers - 1.5f) * 1.6f, 0f, 0f);

        public static GunData GunById(int id) => Guns != null && id >= 0 && id < Guns.Length ? Guns[id] : null;

        public static ZombieData ZombieType(ZombieKind kind)
        {
            int i = (int)kind;
            return ZombieTypes != null && i < ZombieTypes.Length && ZombieTypes[i] != null ? ZombieTypes[i] : Zombie;
        }

        public static int GunId(GunData gun) => Guns == null ? -1 : System.Array.IndexOf(Guns, gun);
    }
}
