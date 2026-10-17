using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>What a player keeps between runs, saved on their own PC: total XP (their level comes from it) and cosmetic picks.</summary>
    public static class ProfileSave
    {
        const string XpKey = "ZombiesFPS.TotalXp";

        public static long LoadXp()
        {
            // PlayerPrefs has no 64-bit int, so XP is kept as a string.
            return long.TryParse(PlayerPrefs.GetString(XpKey, "0"), out long xp) ? System.Math.Max(0, xp) : 0;
        }

        public static void SaveXp(long xp)
        {
            PlayerPrefs.SetString(XpKey, System.Math.Max(0, xp).ToString());
            PlayerPrefs.Save();
        }

        public static void Reset() => SaveXp(0);

        /// <summary>Prestige rank, 0 to 10.</summary>
        public static int LoadPrestige() => Mathf.Max(0, PlayerPrefs.GetInt("ZombiesFPS.Prestige", 0));
        public static void SavePrestige(int prestige)
        {
            PlayerPrefs.SetInt("ZombiesFPS.Prestige", Mathf.Max(0, prestige));
            PlayerPrefs.Save();
        }

        /// <summary>The highest level ever reached across prestiges. Camos and titles unlock by this, so prestiging keeps them.</summary>
        public static int LoadBestLevel() => Mathf.Max(1, PlayerPrefs.GetInt("ZombiesFPS.BestLevel", 1));
        public static void SaveBestLevel(int level)
        {
            if (level <= LoadBestLevel()) return;
            PlayerPrefs.SetInt("ZombiesFPS.BestLevel", level);
            PlayerPrefs.Save();
        }

        // Cosmetics are picked in the main menu and kept by name, so adding guns or camos later doesn't shuffle them.
        public static int LoadCamo(string gunName) => PlayerPrefs.GetInt("ZombiesFPS.Camo." + gunName, 0);
        public static void SaveCamo(string gunName, int camo) => PlayerPrefs.SetInt("ZombiesFPS.Camo." + gunName, camo);
        public static int LoadTitle() => PlayerPrefs.GetInt("ZombiesFPS.Title", 0);
        public static void SaveTitle(int title) => PlayerPrefs.SetInt("ZombiesFPS.Title", title);
    }
}
