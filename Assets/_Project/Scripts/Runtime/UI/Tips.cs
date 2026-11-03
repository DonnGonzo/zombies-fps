using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The tutorial: short first-time hints, each shown once per PC the first time it applies (first wall gun,
    /// first power-up, first mission and so on). Turned off, or reset, in Settings.
    /// </summary>
    public static class Tips
    {
        const string Prefix = "ZombiesFPS.Tip.";
        const float ShowTime = 7f;

        static readonly Queue<(string id, string text)> s_Queue = new Queue<(string, string)>();
        static readonly HashSet<string> s_Queued = new HashSet<string>();
        static readonly List<string> s_AllIds = new List<string>();

        public static string Current { get; private set; }
        static string s_CurrentId;
        static float s_CurrentStart;

        public static float CurrentAge => Time.time - s_CurrentStart;
        public static float CurrentFraction => Current == null ? 0f : Mathf.Clamp01(CurrentAge / ShowTime);

        public static bool Seen(string id) => PlayerPrefs.GetInt(Prefix + id, 0) != 0;

        /// <summary>Queues a tip unless it was already shown on this PC (or tips are off).</summary>
        public static void Offer(string id, string text)
        {
            if (!s_AllIds.Contains(id)) s_AllIds.Add(id);
            if (!GameSettings.Tips || Seen(id) || s_Queued.Contains(id) || s_CurrentId == id) return;
            s_Queued.Add(id);
            s_Queue.Enqueue((id, text));
        }

        /// <summary>Call once a frame: moves on to the next tip.</summary>
        public static void Tick()
        {
            if (!GameSettings.Tips)
            {
                Current = null;
                s_CurrentId = null;
                s_Queue.Clear();
                s_Queued.Clear();
                return;
            }
            if (Current != null && CurrentAge < ShowTime) return;
            if (s_CurrentId != null)
            {
                PlayerPrefs.SetInt(Prefix + s_CurrentId, 1);
                PlayerPrefs.Save();
                s_CurrentId = null;
                Current = null;
            }
            if (s_Queue.Count == 0) return;
            var next = s_Queue.Dequeue();
            s_Queued.Remove(next.id);
            s_CurrentId = next.id;
            Current = next.text;
            s_CurrentStart = Time.time;
        }

        /// <summary>Skip the tip on screen (it counts as seen).</summary>
        public static void Dismiss() => s_CurrentStart = -100f;

        public static void ResetSeen()
        {
            foreach (var id in KnownIds) PlayerPrefs.DeleteKey(Prefix + id);
            foreach (var id in s_AllIds) PlayerPrefs.DeleteKey(Prefix + id);
            PlayerPrefs.Save();
        }

        /// <summary>Every tip id the game uses, so Reset works before a tip has been offered this session.</summary>
        static readonly string[] KnownIds =
        {
            "move", "buy", "door", "wallgun", "ammo", "reload", "switch", "powerup", "mission", "generator", "shop",
            "machine", "perk", "armor", "grenade", "knife", "concussion", "melee", "revive", "downed", "boss", "blackout",
            "level", "prestige", "survivor", "reroll", "radio",
        };
    }
}
