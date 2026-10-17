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

        // Cosmetics are picked in the main menu and kept by name, so adding guns or camos later doesn't shuffle them.
        public static int LoadCamo(string gunName) => PlayerPrefs.GetInt("ZombiesFPS.Camo." + gunName, 0);
        public static void SaveCamo(string gunName, int camo) => PlayerPrefs.SetInt("ZombiesFPS.Camo." + gunName, camo);
        public static int LoadTitle() => PlayerPrefs.GetInt("ZombiesFPS.Title", 0);
        public static void SaveTitle(int title) => PlayerPrefs.SetInt("ZombiesFPS.Title", title);
    }
}
