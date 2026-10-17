using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Shared data every machine loads identically at startup (balance assets, network prefabs,
    /// the locally built arena). Host and clients read the same values, so only state is networked.
    /// </summary>
    public static class GameConfig
    {
        public const int MaxPlayers = 4;
        public const ushort DefaultPort = 7777;

        public static GunData Gun;
        public static ZombieData Zombie;
        public static RoundData Round;

        public static GameObject PlayerPrefab;
        public static GameObject ZombiePrefab;
        public static GameObject DirectorPrefab;

        public static Transform[] ZombieSpawnPoints;
        public static Vector3 PlayerStart;

        /// <summary>Where player slot 0-3 starts: side by side at the arena's player start.</summary>
        public static Vector3 PlayerSpawn(int slot) => PlayerStart + new Vector3((slot % MaxPlayers - 1.5f) * 1.6f, 0f, 0f);
    }
}
