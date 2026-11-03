using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.Core
{
    /// <summary>Prepared, textured user models. Prefabs contain only render geometry; the map owns collision.</summary>
    public static class ImportedModels
    {
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();
        public static GameObject Get(string id)
        {
            if (!Cache.TryGetValue(id, out var model) || model == null)
                Cache[id] = model = Resources.Load<GameObject>("ImportedModels/" + id);
            return model;
        }
        public static Bounds Bounds(GameObject model)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                var matrix = model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
}
