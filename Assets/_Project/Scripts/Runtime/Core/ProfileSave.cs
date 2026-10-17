using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>What a player keeps between runs, saved on their own PC: total XP (their level comes from it).</summary>
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
    }
}
