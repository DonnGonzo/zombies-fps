using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.World;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// The full-screen map (the Map key, M by default): every zone with its name, which are open, the doors,
    /// where everyone is and which way they face, the high spots, the build stations, the shop and upgrade
    /// machine, the current missions, and how many parts of each buildable the squad has found.
    /// </summary>
    public static class MapScreen
    {
        static readonly Color ZoneOpen = new Color(0.2f, 0.23f, 0.26f, 0.95f);
        static readonly Color ZoneClosed = new Color(0.09f, 0.1f, 0.11f, 0.95f);
        static readonly Color ZoneHere = new Color(0.27f, 0.3f, 0.34f, 0.98f);
        static readonly Color Wall = new Color(0f, 0f, 0f, 0.9f);

        static Rect s_Map;
        static Rect Bounds => ZoneMap.Bounds;

        static Vector2 ToMap(Vector3 world) => new Vector2(
            s_Map.x + (world.x - Bounds.xMin) / Bounds.width * s_Map.width,
            s_Map.yMax - (world.z - Bounds.yMin) / Bounds.height * s_Map.height);

        public static void Draw(float w, float h, float u, NetPlayer local)
        {
            var map = GameConfig.Map;
            if (map == null) return;
            Ui.Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 0.72f));

            // Fit the map, keeping its shape, with room for the title and the side panel.
            float side = 330 * u;
            float maxW = w - side - 140 * u, maxH = h - 200 * u;
            float scale = Mathf.Min(maxW / Bounds.width, maxH / Bounds.height);
            float mw = Bounds.width * scale, mh = Bounds.height * scale;
            float mx = (w - mw - side - 30 * u) / 2f, my = (h - mh) / 2f + 20 * u;
            s_Map = new Rect(mx, my, mw, mh);

            Ui.Label(new Rect(mx, my - 64 * u, mw, 40 * u), "MAP", Ui.Header, Ui.Text, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(mx, my - 64 * u, mw, 40 * u), $"{Bindings.Name(GameAction.Map)} to close", Ui.Small, Ui.Muted, TextAnchor.MiddleRight);
            Ui.Fill(new Rect(mx - 6 * u, my - 6 * u, mw + 12 * u, mh + 12 * u), Wall);

            int openMask = MapState.Instance != null ? MapState.Instance.OpenDoorMask : 0;
            int here = local != null ? map.ZoneAt(local.transform.position) : -1;
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
            {
                bool open = ZoneMap.IsZoneOpen(z, openMask);
                foreach (var rect in ZoneMap.ZoneRects(z))
                {
                    Vector2 a = ToMap(new Vector3(rect.xMin, 0f, rect.yMax)), b = ToMap(new Vector3(rect.xMax, 0f, rect.yMin));
                    Ui.Fill(new Rect(a.x, a.y, b.x - a.x, b.y - a.y), z == here ? ZoneHere : open ? ZoneOpen : ZoneClosed);
                }
            }
            // Walls, then each zone's name over its middle.
            float wallPx = Mathf.Max(2f, ZoneMap.WallThickness * scale);
            foreach (var piece in ZoneMap.Walls)
            {
                if (piece.Bottom > 0f) continue;
                Vector2 a = ToMap(new Vector3(piece.A.x, 0f, piece.A.y)), b = ToMap(new Vector3(piece.B.x, 0f, piece.B.y));
                var r = piece.AlongX ? new Rect(Mathf.Min(a.x, b.x), a.y - wallPx / 2f, Mathf.Abs(b.x - a.x), wallPx)
                    : new Rect(a.x - wallPx / 2f, Mathf.Min(a.y, b.y), wallPx, Mathf.Abs(b.y - a.y));
                Ui.Fill(r, Wall);
            }
            for (int z = 0; z < ZoneMap.ZoneCount; z++)
            {
                bool open = ZoneMap.IsZoneOpen(z, openMask);
                Vector2 c = ToMap(ZoneMap.ZoneCenter(z));
                Ui.Label(new Rect(c.x - 90 * u, c.y - 13 * u, 180 * u, 26 * u), ZoneMap.ZoneNames[z].ToUpperInvariant(), Ui.Small, open ? Ui.Text : Ui.Muted, TextAnchor.MiddleCenter);
                if (!open) Ui.Label(new Rect(c.x - 90 * u, c.y + 10 * u, 180 * u, 20 * u), "locked", Ui.Tiny, Ui.A(Ui.Muted, 0.8f), TextAnchor.MiddleCenter);
            }

            // Doors: green once bought, amber with a price while closed.
            for (int d = 0; d < ZoneMap.DoorLinks.Length; d++)
            {
                bool open = (openMask & (1 << d)) != 0;
                Vector2 c = ToMap(ZoneMap.DoorCenter(d));
                // A door in a north-south wall is drawn upright.
                bool alongX = ZoneMap.DoorInWallAlongZ(d);
                float len = 3.2f * scale + 6 * u, thick = 8 * u;
                var r = alongX ? new Rect(c.x - thick / 2f, c.y - len / 2f, thick, len) : new Rect(c.x - len / 2f, c.y - thick / 2f, len, thick);
                Ui.Fill(r, open ? Ui.Good : Ui.Amber);
                if (!open && GameConfig.Prices != null)
                    Ui.Label(new Rect(c.x - 60 * u, c.y + (alongX ? 14 : 8) * u, 120 * u, 18 * u), GameConfig.Prices.DoorCost(d).ToString("N0"), Ui.Tiny, Ui.Amber, TextAnchor.MiddleCenter);
            }

            // Places worth knowing.
            Marker(ZoneMap.ShopWorldPosition, "$", "Shop", Ui.Amber, u);
            Marker(ZoneMap.MachineWorldPosition, "^", "Upgrade", Ui.Violet, u);
            for (int s = 0; s < ZoneMap.HighSpotCount; s++)
                Marker(ZoneMap.HighSpot(s), "▲", ZoneMap.HighSpotNames[s], new Color(0.85f, 0.85f, 0.7f), u, small: true);
            for (int b = 0; b < ZoneMap.BenchCount; b++)
                Marker(ZoneMap.BenchPosition(b), "B", "Build station", BuildManager.BuildableColors[0], u);
            DrawMissionMarkers(map, u);

            var build = BuildManager.Instance;
            if (build != null && build.RobotActive) Dot(build.RobotPosition.Value, BuildManager.BuildableColors[BuildManager.Bot], 10 * u, null, u);

            // Everyone: a dot in their suit colour, a tick the way they face, and their name.
            foreach (var p in NetPlayer.All)
            {
                if (p.State.Value == LifeState.Dead) continue;
                bool me = p == local;
                var color = p.State.Value == LifeState.Downed ? Ui.Bad : p.SuitColor;
                Vector2 c = ToMap(p.transform.position);
                Vector3 f = p.transform.forward;
                var tip = c + new Vector2(f.x, -f.z).normalized * (me ? 16 : 12) * u;
                Ui.Fill(new Rect(tip.x - 3 * u, tip.y - 3 * u, 6 * u, 6 * u), Ui.Text);
                if (me) Ui.RoundRect(new Rect(c.x - 11 * u, c.y - 11 * u, 22 * u, 22 * u), Ui.Text, small: true);
                Ui.RoundRect(new Rect(c.x - 8 * u, c.y - 8 * u, 16 * u, 16 * u), color, small: true);
                string label = me ? "YOU" : p.DisplayName + (p.State.Value == LifeState.Downed ? " (down)" : "");
                Ui.Label(new Rect(c.x - 100 * u, c.y + 10 * u, 200 * u, 20 * u), label, Ui.Tiny, me ? Ui.Text : Ui.A(Ui.Text, 0.85f), TextAnchor.UpperCenter);
            }

            DrawSidePanel(new Rect(s_Map.xMax + 30 * u, my, side, mh), u, local);
        }

        static void Marker(Vector3 world, string glyph, string label, Color color, float u, bool small = false)
        {
            Vector2 c = ToMap(world);
            float s = (small ? 16 : 20) * u;
            var r = new Rect(c.x - s / 2f, c.y - s / 2f, s, s);
            Ui.RoundRect(r, Ui.A(color, 0.9f), small: true);
            Ui.Label(r, glyph, Ui.Tiny, new Color(0.05f, 0.05f, 0.06f), TextAnchor.MiddleCenter);
            Ui.Label(new Rect(c.x - 90 * u, c.y - s / 2f - 18 * u, 180 * u, 18 * u), label, Ui.Tiny, Ui.A(color, 0.9f), TextAnchor.MiddleCenter);
        }

        static void Dot(Vector3 world, Color color, float size, string label, float u)
        {
            Vector2 c = ToMap(world);
            Ui.RoundRect(new Rect(c.x - size / 2f, c.y - size / 2f, size, size), color, small: true);
            if (label != null) Ui.Label(new Rect(c.x - 90 * u, c.y + size / 2f, 180 * u, 18 * u), label, Ui.Tiny, color, TextAnchor.UpperCenter);
        }

        /// <summary>Where the current placed missions are: a pulsing "!" on their generator, beacon or survivor.</summary>
        static void DrawMissionMarkers(ZoneMap map, float u)
        {
            var board = MissionBoard.Instance;
            if (board == null) return;
            for (int i = 0; i < MissionData.SlotCount; i++)
            {
                var def = board.DefIn(i);
                ulong slot = board.SlotValue(i);
                if (def == null || MissionData.DoneOf(slot) || MissionData.FailedOf(slot) || !MissionData.IsPlaced(def.objective)) continue;
                int at = MissionData.ZoneOf(slot);
                Vector3? where = null;
                if (def.objective == MissionObjective.HoldGenerator && map.Generators != null && at < map.Generators.Length) where = map.Generators[at].transform.position;
                else if (def.objective == MissionObjective.HoldHighGround && at < ZoneMap.HighSpotCount) where = ZoneMap.HighSpot(at);
                else if (def.objective == MissionObjective.ProtectSurvivor && board.TryGetSurvivor(out var s)) where = s;
                if (!where.HasValue) continue;
                Vector2 c = ToMap(where.Value);
                float pulse = 22 * u + 6 * u * Mathf.Sin(Time.unscaledTime * 5f);
                Ui.RoundRect(new Rect(c.x - pulse / 2f, c.y - pulse / 2f, pulse, pulse), Ui.A(Ui.Sky, 0.35f), small: true);
                var r = new Rect(c.x - 10 * u, c.y - 10 * u, 20 * u, 20 * u);
                Ui.RoundRect(r, Ui.Sky, small: true);
                Ui.Label(r, "!", Ui.Bold, new Color(0.05f, 0.05f, 0.06f), TextAnchor.MiddleCenter);
                Ui.Label(new Rect(c.x - 100 * u, c.y + 12 * u, 200 * u, 18 * u), def.title, Ui.Tiny, Ui.Sky, TextAnchor.UpperCenter);
            }
        }

        static void DrawSidePanel(Rect r, float u, NetPlayer local)
        {
            Ui.PanelBox(r, Ui.Amber, u, 0.9f);
            float x = r.x + 20 * u, y = r.y + 16 * u, iw = r.width - 40 * u;
            var map = GameConfig.Map;
            int here = local != null && map != null ? map.ZoneAt(local.transform.position) : -1;
            Ui.Label(new Rect(x, y, iw, 22 * u), "YOU ARE IN", Ui.Tiny, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(x, y + 20 * u, iw, 30 * u), here >= 0 ? ZoneMap.ZoneNames[here] : "-", Ui.Bold, Ui.Text, TextAnchor.MiddleLeft);
            y += 70 * u;

            var build = BuildManager.Instance;
            Ui.Label(new Rect(x, y, iw, 22 * u), "BUILDABLES", Ui.Tiny, Ui.Amber, TextAnchor.MiddleLeft);
            y += 26 * u;
            for (int item = 0; item < ZoneMap.BuildableCount; item++)
            {
                var color = BuildManager.BuildableColors[item];
                int found = build != null ? build.PartsFound(item) : 0;
                bool built = build != null && build.IsBuilt(item);
                Ui.Label(new Rect(x, y, iw, 24 * u), BuildManager.BuildableNames[item], Ui.Small, color, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(x, y, iw, 24 * u), built ? "BUILT" : $"{found}/{ZoneMap.PartsFor(item)} parts", Ui.Small, built ? Ui.Good : Ui.Text, TextAnchor.MiddleRight);
                y += 24 * u;
                string detail = null;
                if (build != null)
                {
                    if (!built)
                    {
                        var missing = new System.Collections.Generic.List<string>();
                        for (int k = 0; k < ZoneMap.PartsFor(item); k++)
                        {
                            int part = ZoneMap.FirstPart(item) + k;
                            if (!build.IsCollected(part)) missing.Add(BuildManager.PartNames[part]);
                        }
                        detail = missing.Count > 0 ? "missing: " + string.Join(", ", missing) : "ready to build at a station";
                    }
                    else if (item == BuildManager.Bot)
                    {
                        int round = RoundManager.Instance != null ? RoundManager.Instance.DisplayRound : 1;
                        detail = build.RobotActive ? $"helping, {Mathf.CeilToInt(build.RobotTimeLeft.Value)}s left"
                            : round < build.RobotReadyRound.Value ? $"offline until round {build.RobotReadyRound.Value}" : "ready at a station";
                    }
                }
                if (detail != null)
                {
                    var style = Ui.Tiny;
                    bool wrap = style.wordWrap;
                    style.wordWrap = true;
                    Ui.Label(new Rect(x, y, iw, 38 * u), detail, style, Ui.Muted, TextAnchor.UpperLeft);
                    style.wordWrap = wrap;
                    y += 40 * u;
                }
                y += 8 * u;
            }

            y += 10 * u;
            Ui.Label(new Rect(x, y, iw, 22 * u), "KEY", Ui.Tiny, Ui.Amber, TextAnchor.MiddleLeft);
            y += 24 * u;
            string[] key =
            {
                "$  spawn shop", "^  upgrade machine", "B  build station", "▲  high spot (climb up)",
                "!  mission", "doors: amber closed, green open",
            };
            foreach (var line in key)
            {
                Ui.Label(new Rect(x, y, iw, 20 * u), line, Ui.Tiny, Ui.Text, TextAnchor.MiddleLeft);
                y += 20 * u;
            }
        }
    }
}
