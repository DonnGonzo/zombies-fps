using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Depot's "Last Chance" corner (three slot machines and a blackjack table on its south wall), the Lantern
    /// Core's sci-fi capsules (see SpecimenTanks) and the hex sphere over the upgrade machine. The models come from
    /// ImportedModels; each piece's collision is a plain box so the map plays the same without the art.
    /// </summary>
    public partial class ZoneMap
    {
        public const int BlackjackStation = 3;
        /// <summary>The Depot's south wall, inside face (local z).</summary>
        const float CasinoWall = -12f + WallHalf;
        /// <summary>
        /// Where a player stands to play each station, slots then blackjack. The slots sit between the window at x 0
        /// and the Slick Fingers machine at x 8; the table sits west of the Plague Ward door at x -13.
        /// </summary>
        public static Vector3[] CasinoPositions => s_CasinoPositions ??= new[] { D(2.8f, 0f, -10f), D(4.1f, 0f, -10f), D(5.4f, 0f, -10f), D(-18f, 0f, -9.1f) };
        // Built on first use: static fields in other parts of ZoneMap (the zone anchors) may not be set up yet.
        static Vector3[] s_CasinoPositions;
        const float SlotRange = 1.3f, TableRange = 2f;

        public static float CasinoRange(int station) => station == BlackjackStation ? TableRange : SlotRange;

        /// <summary>An imported model, <paramref name="height"/> metres tall, its base centred on <paramref name="floor"/>; null without the art.</summary>
        GameObject AuthoredVisual(string id, Vector3 floor, float height, float yaw = 0f)
        {
            var prefab = ImportedModels.Get(id);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, Root);
            go.name = id;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Object.Destroy(go); return null; }
            Bounds Measure()
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                return b;
            }
            go.transform.localScale *= height / Mathf.Max(0.01f, Measure().size.y);
            var bounds = Measure();
            go.transform.position += floor - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return go;
        }

        /// <summary>An imported model with an invisible box of <paramref name="size"/> (before the yaw) for collision.</summary>
        void SolidModel(string id, Vector3 floor, float height, Vector3 size, float yaw = 0f)
        {
            AuthoredVisual(id, floor, height, yaw);
            var solid = Greybox.Box(id + " collision", Root, floor + Vector3.up * (size.y / 2f), size, Metal, surface: Surface.Metal);
            solid.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            solid.GetComponent<Renderer>().enabled = false;
        }

        void BuildCasinoAndResearchProps()
        {
            const float slotDepth = 0.72f, tableDepth = 1.18f;
            for (int i = 0; i < BlackjackStation; i++)
                SolidModel("a-slot-machine", D(CasinoPositions[i].x - ZoneAnchors[DepotZone].x, 0f, CasinoWall + slotDepth / 2f + 0.05f),
                    2f, new Vector3(0.98f, 2f, slotDepth));
            var table = CasinoPositions[BlackjackStation];
            SolidModel("gambling-table", D(table.x - ZoneAnchors[DepotZone].x, 0f, CasinoWall + tableDepth / 2f + 0.35f),
                1.2f, new Vector3(1.72f, 1.1f, tableDepth));
            RemodelSign(DepotZone, "LAST CHANCE  /  SLOTS", 4.1f, 3.2f, CasinoWall + 0.06f, 0f, 4.2f);
            RemodelSign(DepotZone, "THE WATCH'S TABLE  /  BLACKJACK", -18f, 3.2f, CasinoWall + 0.06f, 0f, 4.6f);
            RemodelLight(DepotZone, 4.1f, 3.6f, -10.6f, new Color(1f, 0.62f, 0.22f));
            RemodelLight(DepotZone, -18f, 3.6f, -10.2f, new Color(1f, 0.62f, 0.22f));
        }

        /// <summary>The casino stations stay clear of dressing, like the wall buys do.</summary>
        void CasinoKeepOuts()
        {
            for (int i = 0; i < CasinoPositions.Length; i++)
                _keepOut.Add((CasinoPositions[i] + Vector3.back * 1f, i == BlackjackStation ? 2.2f : 1.4f));
        }
    }
}
