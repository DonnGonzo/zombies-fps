using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// Scanned CC0 prop models (Poly Haven: barrels, crates, lanterns and so on) for dressing the map.
    ///
    /// Each prop lives in Resources/CC0/Props/slug as slug_mesh.bytes (made from the glTF by a converter, already
    /// in Unity's axes and metres, its origin at the middle of its base) and per material key_albedo and
    /// key_normal textures. A plain binary mesh needs no model import settings, so it looks the same on every
    /// machine. A key ending in _glass is drawn glowing (lantern panes).
    /// </summary>
    public static class PropModels
    {
        public sealed class Model
        {
            public Mesh Mesh;
            /// <summary>One per submesh, with the material key each was made from.</summary>
            public Material[] Materials;
            public string[] Keys;
            public Bounds Bounds => Mesh.bounds;
        }

        public const string Barrel = "wine_barrel_01";
        public const string Chest = "wooden_crate_01";
        public const string LongCrate = "wooden_crate_02";
        public const string Bucket = "wooden_bucket_01";
        public const string Table = "WoodenTable_01";
        public const string Shield = "kite_shield";
        public const string Lantern = "wooden_lantern_01";
        public const string Basket = "wicker_basket_02";
        public const string FirePit = "stone_fire_pit";

        static readonly Color GlassGlow = new Color(1f, 0.7f, 0.38f);

        static readonly Dictionary<string, Model> s_Models = new Dictionary<string, Model>();
        static Material s_Base;

        /// <summary>The prop, or null when the map art is off or its files aren't in the project.</summary>
        public static Model Get(string slug)
        {
            if (!SurfaceTextures.PhotosEnabled) return null;
            if (s_Models.TryGetValue(slug, out var cached)) return cached != null && cached.Mesh != null ? cached : null;
            Model model = null;
            try { model = Load(slug); }
            catch (System.Exception e) { Debug.LogWarning($"Prop {slug} couldn't be loaded: {e.Message}"); }
            s_Models[slug] = model;
            return model;
        }

        static Model Load(string slug)
        {
            var data = Resources.Load<TextAsset>($"CC0/Props/{slug}/{slug}_mesh");
            if (data == null) return null;
            using var reader = new BinaryReader(new MemoryStream(data.bytes));
            if (new string(reader.ReadChars(4)) != "PRP1") return null;
            int subCount = reader.ReadInt32();
            var keys = new string[subCount];
            for (int i = 0; i < subCount; i++) keys[i] = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
            int n = reader.ReadInt32();
            var pos = new Vector3[n];
            var nrm = new Vector3[n];
            var uv = new Vector2[n];
            for (int i = 0; i < n; i++) pos[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < n; i++) nrm[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            for (int i = 0; i < n; i++) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());

            var mesh = new Mesh { name = slug, indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(pos);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = subCount;
            for (int s = 0; s < subCount; s++)
            {
                var tris = new int[reader.ReadInt32()];
                for (int i = 0; i < tris.Length; i++) tris[i] = reader.ReadInt32();
                mesh.SetTriangles(tris, s);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var mats = new Material[subCount];
            for (int s = 0; s < subCount; s++) mats[s] = MaterialFor(slug, keys[s]);
            return new Model { Mesh = mesh, Materials = mats, Keys = keys };
        }

        static Material MaterialFor(string slug, string key)
        {
            if (key.EndsWith("_glass")) return Greybox.Unlit(GlassGlow);
            if (s_Base == null) s_Base = Resources.Load<Material>("Materials/PropLit") ?? Resources.Load<Material>("Materials/GreyboxLit");
            var albedo = Resources.Load<Texture2D>($"CC0/Props/{slug}/{key}_albedo");
            var normal = Resources.Load<Texture2D>($"CC0/Props/{slug}/{key}_normal");
            var mat = s_Base != null ? new Material(s_Base) : new Material(Shader.Find("Standard"));
            mat.name = "Prop " + key;
            mat.color = Color.white;
            if (albedo != null) mat.mainTexture = albedo;
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Glossiness", key.Contains("clamps") ? 0.4f : 0.18f);
            mat.SetFloat("_Metallic", key.Contains("clamps") ? 0.6f : 0f);
            mat.enableInstancing = true;
            return mat;
        }
    }
}
