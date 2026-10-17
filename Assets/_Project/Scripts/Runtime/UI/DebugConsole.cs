using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Player;
using ZombiesFPS.World;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The test console (` or F2): typed commands for trying things out quickly, such as jumping to round 250,
    /// giving points or spawning a boss. Commands only work on the host, which runs the game.
    /// </summary>
    public static class DebugConsole
    {
        const int MaxLog = 14;
        const string FieldName = "ZombiesConsoleField";

        public static bool IsOpen { get; private set; }

        static string s_Input = "";
        static readonly List<string> s_Log = new List<string>();
        static readonly List<string> s_History = new List<string>();
        static int s_HistoryIndex;
        static bool s_Focus;

        static readonly string[] Help =
        {
            "round N        jump to round N (1-500)",
            "next           next round",
            "points N       give everyone N points",
            "xp N           give everyone N XP",
            "god            toggle god mode",
            "spawn TYPE [N] spawn walker, runner, crawler, brute, spitter or boss",
            "boss           spawn a boss",
            "clear          remove every zombie",
            "gun NAME       give yourself a gun (try: gun lance)",
            "guns           list the guns",
            "perks          every perk and full armour for everyone",
            "ammo           full ammo and throwables for everyone",
            "powerup [emp]  offer power-up cards (emp = empowered)",
            "doors          open every door",
            "parts          find every build part",
            "goto ZONE      teleport yourself (try: goto crane, goto high 2)",
            "clearlog       empty this log",
        };

        public static void Toggle()
        {
            IsOpen = !IsOpen;
            GameInput.Suspended = IsOpen;
            if (IsOpen)
            {
                s_Focus = true;
                s_Input = "";
                if (s_Log.Count == 0) Log("Type help for commands. Esc closes.");
                PlayerLook.SetCursorLocked(false);
            }
            else
            {
                PlayerLook.SetCursorLocked(true);
            }
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GameInput.Suspended = false;
        }

        static void Log(string line)
        {
            s_Log.Add(line);
            while (s_Log.Count > MaxLog) s_Log.RemoveAt(0);
        }

        public static void Draw(float w, float h, float u)
        {
            if (!IsOpen) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Run(s_Input);
                    s_Input = "";
                    e.Use();
                }
                else if (e.keyCode == KeyCode.UpArrow && s_History.Count > 0)
                {
                    s_HistoryIndex = Mathf.Clamp(s_HistoryIndex - 1, 0, s_History.Count - 1);
                    s_Input = s_History[s_HistoryIndex];
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow && s_History.Count > 0)
                {
                    s_HistoryIndex = Mathf.Clamp(s_HistoryIndex + 1, 0, s_History.Count);
                    s_Input = s_HistoryIndex < s_History.Count ? s_History[s_HistoryIndex] : "";
                    e.Use();
                }
            }

            float pw = Mathf.Min(w - 40 * u, 980 * u), lineH = 24 * u;
            float ph = 70 * u + lineH * MaxLog + 56 * u;
            var r = new Rect((w - pw) / 2f, 20 * u, pw, ph);
            Ui.PanelBox(r, Ui.Amber, u);
            Ui.Label(new Rect(r.x + 20 * u, r.y + 14 * u, pw, 30 * u), "TEST CONSOLE", Ui.Bold, Ui.Amber, TextAnchor.UpperLeft);
            bool host = RoundManager.Instance != null && RoundManager.Instance.IsServer;
            Ui.Label(new Rect(r.x, r.y + 16 * u, pw - 20 * u, 30 * u), host ? "host" : "only the host can run commands", Ui.Tiny, Ui.Muted, TextAnchor.UpperRight);
            float y = r.y + 54 * u;
            foreach (var line in s_Log)
            {
                Ui.Label(new Rect(r.x + 20 * u, y, pw - 40 * u, lineH), line, Ui.Small, line.StartsWith(">") ? Ui.Muted : Ui.Text, TextAnchor.MiddleLeft);
                y += lineH;
            }
            GUI.SetNextControlName(FieldName);
            s_Input = GUI.TextField(new Rect(r.x + 20 * u, r.yMax - 56 * u, pw - 40 * u, 42 * u), s_Input ?? "", 120, Ui.Field).Replace("`", "");
            if (s_Focus)
            {
                GUI.FocusControl(FieldName);
                s_Focus = false;
            }
        }

        static void Run(string raw)
        {
            string line = (raw ?? "").Trim();
            if (line.Length == 0) return;
            s_History.Add(line);
            s_HistoryIndex = s_History.Count;
            Log("> " + line);
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();
            string arg = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : "";

            if (cmd == "help" || cmd == "?")
            {
                foreach (var h in Help) Log(h);
                return;
            }
            if (cmd == "clearlog")
            {
                s_Log.Clear();
                return;
            }

            var rounds = RoundManager.Instance;
            if (rounds == null || !rounds.IsServer)
            {
                Log("Only the host can run test commands.");
                return;
            }

            switch (cmd)
            {
                case "round":
                    if (int.TryParse(arg, out int round))
                    {
                        rounds.DebugSkipToRound(round);
                        Log($"Jumped to round {Mathf.Clamp(round, 1, RoundData.MaxRound)}.");
                    }
                    else Log("Usage: round N");
                    break;
                case "next":
                    rounds.DebugSkipToRound(rounds.Round.Value + 1);
                    Log("Next round.");
                    break;
                case "points":
                    if (long.TryParse(arg, out long pts) && pts > 0)
                    {
                        foreach (var p in NetPlayer.All) p.Points.Value += pts;
                        Log($"Gave everyone {pts:N0} points.");
                    }
                    else Log("Usage: points N");
                    break;
                case "xp":
                    if (long.TryParse(arg, out long xp) && xp > 0)
                    {
                        foreach (var p in NetPlayer.All) p.ServerAddXp(xp);
                        Log($"Gave everyone {xp:N0} XP.");
                    }
                    else Log("Usage: xp N");
                    break;
                case "god":
                    rounds.DebugToggleGod();
                    Log(rounds.GodMode.Value ? "God mode on." : "God mode off.");
                    break;
                case "boss":
                    Log(rounds.DebugSpawn(ZombieKind.Boss, 1) > 0 ? "Boss spawned." : "Couldn't spawn: a round has to be running.");
                    break;
                case "spawn":
                    var args = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (args.Length == 0 || !Enum.TryParse(args[0], true, out ZombieKind kind))
                    {
                        Log("Usage: spawn walker|runner|crawler|brute|spitter|boss [count]");
                        break;
                    }
                    int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
                    int made = rounds.DebugSpawn(kind, count);
                    Log(made > 0 ? $"Spawned {made} {kind}." : "Couldn't spawn: a round has to be running.");
                    break;
                case "clear":
                    rounds.DebugClearZombies();
                    Log("Cleared the zombies.");
                    break;
                case "guns":
                    if (GameConfig.Guns != null)
                        for (int i = 0; i < GameConfig.Guns.Length; i++)
                            if (GameConfig.Guns[i] != null) Log($"{i}  {GameConfig.Guns[i].displayName}");
                    break;
                case "gun":
                    int id = FindGun(arg);
                    var local = NetPlayer.Local;
                    if (id < 0 || local == null) Log("No gun by that name. Type guns for the list.");
                    else
                    {
                        local.ServerGiveGun(id);
                        Log($"Gave you the {GameConfig.GunById(id).displayName}.");
                    }
                    break;
                case "perks":
                    foreach (var p in NetPlayer.All) p.DebugGiveAllPerks();
                    Log("Everyone has every perk and full armour.");
                    break;
                case "ammo":
                    foreach (var p in NetPlayer.All) p.ServerRefillAllAmmo();
                    Log("Full ammo and throwables for everyone.");
                    break;
                case "powerup":
                    rounds.DebugOfferPowerUps(arg.StartsWith("emp", StringComparison.OrdinalIgnoreCase));
                    Log("Power-up cards offered.");
                    break;
                case "doors":
                    if (MapState.Instance != null)
                        for (int d = 0; d < ZoneMap.DoorLinks.Length; d++) MapState.Instance.ServerOpenDoor(d);
                    Log("Every door is open.");
                    break;
                case "parts":
                    if (BuildManager.Instance != null) BuildManager.Instance.DebugCollectAll();
                    Log("Every part found. Build them at a build station.");
                    break;
                case "goto":
                    var me = NetPlayer.Local;
                    if (me == null || !TryFindPlace(arg, out Vector3 to, out string placeName))
                    {
                        Log("Usage: goto ZONE (part of a zone name) or goto high N (0-5, a high spot)");
                        break;
                    }
                    me.DebugTeleport(to);
                    Log($"Teleported to {placeName}.");
                    break;
                default:
                    Log($"Unknown command \"{cmd}\". Type help.");
                    break;
            }
        }

        static bool TryFindPlace(string arg, out Vector3 position, out string name)
        {
            position = Vector3.zero;
            name = null;
            if (string.IsNullOrWhiteSpace(arg)) return false;
            var words = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words[0].Equals("high", StringComparison.OrdinalIgnoreCase) && words.Length > 1 && int.TryParse(words[1], out int spot)
                && spot >= 0 && spot < ZoneMap.HighSpotCount)
            {
                position = ZoneMap.HighSpot(spot) + new Vector3(-1.5f, 0.1f, 0f);
                name = "the " + ZoneMap.HighSpotNames[spot];
                return true;
            }
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
                if (ZoneMap.ZoneNames[z].IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    position = ZoneMap.ZoneCenter(z) + new Vector3(0f, 0.1f, -3f);
                    name = "the " + ZoneMap.ZoneNames[z];
                    return true;
                }
            return false;
        }

        static int FindGun(string arg)
        {
            if (GameConfig.Guns == null || string.IsNullOrWhiteSpace(arg)) return -1;
            if (int.TryParse(arg, out int id)) return GameConfig.GunById(id) != null ? id : -1;
            for (int i = 0; i < GameConfig.Guns.Length; i++)
            {
                var g = GameConfig.Guns[i];
                if (g != null && g.displayName.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            }
            return -1;
        }
    }
}
