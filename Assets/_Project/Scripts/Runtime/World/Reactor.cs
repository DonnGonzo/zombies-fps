using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The Lantern Core's reactor (Ivan's Reactor_01 model, 6 m tall, 1.96 m across): steel body, glass chamber,
    /// a column of plasma and blobs of plasma drifting up and down inside, with a pulsing light. Only looks: the
    /// map gives the core its collider either way.
    /// </summary>
    public class Reactor : MonoBehaviour
    {
        public const float Height = 6.04f;
        public const float Diameter = 1.96f;

        static readonly Color PlasmaOrange = new Color(1f, 0.33f, 0.05f);
        static readonly Color PlasmaGold = new Color(1f, 0.62f, 0.12f);

        readonly List<Transform> _blobs = new List<Transform>();
        readonly List<Vector3> _origins = new List<Vector3>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        Light _light;
        float _baseIntensity;

        /// <summary>The reactor standing at a point on the floor, or null when its files aren't in the project.</summary>
        public static Reactor Build(Transform parent, Vector3 floorPos, Quaternion rot, Light light)
        {
            var body = PropModels.LoadRaw("CC0/Reactor/Reactor_01_mesh");
            if (body.mesh == null) return null;
            var root = new GameObject("Reactor");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = floorPos;
            root.transform.localRotation = rot;
            var reactor = root.AddComponent<Reactor>();
            reactor._meshes.Add(body.mesh);
            var mats = new Material[body.keys.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = MaterialFor(body.keys[i]);
            root.AddComponent<MeshFilter>().sharedMesh = body.mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = mats;

            var blobs = PropModels.LoadRaw("CC0/Reactor/Reactor_01_blobs");
            if (blobs.mesh != null)
            {
                for (int s = 0; s < blobs.keys.Length; s++)
                {
                    var mesh = Submesh(blobs.mesh, s, out var center);
                    reactor._meshes.Add(mesh);
                    var go = new GameObject("Plasma Blob");
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = center;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = MaterialFor(blobs.keys[s]);
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    reactor._blobs.Add(go.transform);
                    reactor._origins.Add(center);
                }
                Object.Destroy(blobs.mesh);
            }
            reactor._light = light;
            if (light != null) reactor._baseIntensity = light.intensity;
            return reactor;
        }

        /// <summary>Metals take the map's steel texture; glass is see-through; plasma and the screen glow.</summary>
        static Material MaterialFor(string key) => key switch
        {
            "CharcoalMetal" => Greybox.Lit(new Color(0.2f, 0.22f, 0.24f), Surface.Metal),
            "Gunmetal" => Greybox.Lit(new Color(0.3f, 0.33f, 0.35f), Surface.Metal),
            "DarkSteel" => Greybox.Lit(new Color(0.14f, 0.16f, 0.18f), Surface.Metal),
            "ScratchedSteel" => Greybox.Lit(new Color(0.43f, 0.45f, 0.46f), Surface.Metal),
            "Copper" => Greybox.Lit(new Color(0.44f, 0.26f, 0.16f), Surface.Rust),
            "Rust" => Greybox.Lit(new Color(0.39f, 0.19f, 0.13f), Surface.Rust),
            "WarningYellow" => Greybox.Lit(new Color(0.8f, 0.6f, 0.16f), Surface.Plain),
            "Rubber" => Greybox.Lit(new Color(0.07f, 0.07f, 0.09f), Surface.Rubber),
            "Glass" => Greybox.Unlit(new Color(0.27f, 0.57f, 0.6f, 0.22f)),
            "IndicatorCyan" => Greybox.Unlit(new Color(0.19f, 0.8f, 0.88f)),
            "PlasmaGold" => Greybox.Unlit(PlasmaGold),
            _ => Greybox.Unlit(PlasmaOrange),
        };

        /// <summary>One submesh as its own mesh, centred on its middle (returned) so it can move on its own.</summary>
        static Mesh Submesh(Mesh source, int sub, out Vector3 center)
        {
            var tris = source.GetTriangles(sub);
            var verts = source.vertices;
            var normals = source.normals;
            var map = new Dictionary<int, int>();
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            for (int i = 0; i < tris.Length; i++)
            {
                if (!map.TryGetValue(tris[i], out int j))
                {
                    j = v.Count;
                    map[tris[i]] = j;
                    v.Add(verts[tris[i]]);
                    n.Add(normals[tris[i]]);
                }
                tris[i] = j;
            }
            var b = new Bounds(v[0], Vector3.zero);
            foreach (var p in v) b.Encapsulate(p);
            center = b.center;
            for (int i = 0; i < v.Count; i++) v[i] -= center;
            var mesh = new Mesh { name = "Plasma Blob" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < _blobs.Count; i++)
            {
                var p = _origins[i];
                p.y += Mathf.Sin(t * (0.48f + i * 0.09f) + i * 1.6f) * 0.18f;
                p.x += Mathf.Sin(t * 0.37f + i) * 0.04f;
                _blobs[i].localPosition = p;
            }
            if (_light != null) _light.intensity = _baseIntensity * (1f + Mathf.Sin(t * 1.7f) * 0.25f);
        }

        void OnDestroy()
        {
            foreach (var m in _meshes)
                if (m != null) Destroy(m);
        }
    }
}
