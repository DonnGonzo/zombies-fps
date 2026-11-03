using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Static batching for the map pieces that aren't in the merged dressing (walls, trims, machines' casings,
    /// props kept as their own objects), once the whole map is built. The map is made in code, so Unity's
    /// build-time static batching never sees it; this does the same at load. Anything that might move later is
    /// left out: whatever sits under a script (doors, generators, machines, billboards), anything animated or
    /// physical, text, and materials projected in object space (Greybox's "moving" look).
    /// </summary>
    public partial class ZoneMap
    {
        void BatchStatic()
        {
            var batch = new List<GameObject>();
            var moving = new Dictionary<Transform, bool>();
            foreach (var r in Root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled) continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                if (r.GetComponent<TextMesh>() != null || ObjectSpace(r) || MightMove(r.transform, moving)) continue;
                batch.Add(r.gameObject);
            }
            if (batch.Count > 1) StaticBatchingUtility.Combine(batch.ToArray(), Root.gameObject);
        }

        static bool ObjectSpace(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && m.HasProperty("_ObjectSpace") && m.GetFloat("_ObjectSpace") > 0.5f) return true;
            return false;
        }

        /// <summary>True when t or a parent below the map's root has a script or a moving part on it.</summary>
        bool MightMove(Transform t, Dictionary<Transform, bool> known)
        {
            if (t == null || t == Root) return false;
            if (known.TryGetValue(t, out bool cached)) return cached;
            bool moves = t.GetComponent<MonoBehaviour>() != null || t.GetComponent<Rigidbody>() != null
                || t.GetComponent<Animator>() != null || t.GetComponent<Animation>() != null
                || t.GetComponent<ParticleSystem>() != null || t.GetComponent<LODGroup>() != null
                || MightMove(t.parent, known);
            known[t] = moves;
            return moves;
        }
    }
}
