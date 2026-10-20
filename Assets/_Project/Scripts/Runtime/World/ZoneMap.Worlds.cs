using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Furniture from Ivan's Ten Worlds pack (the chapel, archive, catacomb and garden rooms): church pews, an altar
    /// dais, organ pipes and iron chandeliers in the Chapel, tall bookcases, reading tables and a celestial globe in
    /// the Archives, arcane pylons, worktables and tapestries in the Lantern Core lab, and benches, a fountain and
    /// planted beds in the Courtyard and Greenhouse. Meshes are Resources/CC0/TenWorlds/name.bytes (made by
    /// tools/tenworlds/split_props.py) with the pack's own textures.
    ///
    /// Some replace the look of the old grey boxes (pews, bookshelves); the boxes keep their colliders, so the solid
    /// map stays the same with or without the art.
    /// </summary>
    public partial class ZoneMap
    {
        /// <summary>Rooms with tapestries on the walls (at most how many).</summary>
        static readonly Dictionary<int, int> TapestryZones = new Dictionary<int, int> { [10] = 2, [12] = 2, [14] = 3, [15] = 2 };

        /// <summary>
        /// The pack's hospital, gaol and furnace pieces were steel and white paint; in this map they're iron and
        /// wood, so those materials draw with these textures instead.
        /// </summary>
        static string TextureKey(string key) => key switch
        {
            "brushed_steel" => "rusted_iron",
            "white_paint" => "wood_pale",
            "concrete" => "limestone",
            _ => key,
        };

        static readonly Dictionary<string, Material> s_WorldMaterials = new Dictionary<string, Material>();

        /// <summary>A Ten Worlds material by its key (wood_dark, brass...): the pack's colour and normal maps, with its metal and gloss.</summary>
        static Material WorldsMaterial(string key)
        {
            if (s_WorldMaterials.TryGetValue(key, out var cached) && cached != null) return cached;
            Material mat;
            if (key == "clean_glass") mat = Greybox.Unlit(new Color(0.55f, 0.7f, 0.72f, 0.3f));
            else
            {
                string tex = TextureKey(key);
                var (metallic, gloss) = tex switch
                {
                    "black_steel" => (0.8f, 0.45f),
                    "brass" => (0.85f, 0.62f),
                    "rusted_iron" => (0.45f, 0.15f),
                    "marble" => (0f, 0.6f),
                    "stained_blue" => (0f, 0.8f),
                    "wood_mid" => (0f, 0.25f),
                    "copper" => (0.8f, 0.4f),
                    _ => (0f, 0.15f),
                };
                mat = PropModels.Textured("Worlds " + key,
                    Resources.Load<Texture2D>($"CC0/TenWorlds/Textures/{tex}_albedo"),
                    Resources.Load<Texture2D>($"CC0/TenWorlds/Textures/{tex}_normal"), metallic, gloss);
            }
            s_WorldMaterials[key] = mat;
            return mat;
        }

        /// <summary>
        /// Hides a grey box's own look and fills it with a row of the model along its length (two rows back to back
        /// when asked), each stretched a little to fit. Height is the box's unless given. The box keeps its collider;
        /// without the model it stays as it was.
        /// </summary>
        void Reskin(GameObject box, PackItem item, bool backToBack, float height = 0f)
        {
            var model = PropModels.GetPack(item.Path, PackMaterial);
            if (model == null) return;
            box.GetComponent<Renderer>().enabled = false;
            var size = box.transform.localScale;
            bool alongX = size.x >= size.z;
            float length = alongX ? size.x : size.z, depth = alongX ? size.z : size.x;
            float h = height > 0f ? height : size.y;
            var facing = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            var floor = box.transform.localPosition - Vector3.up * size.y / 2f;
            var b = model.Bounds;
            float sy = h / b.size.y;
            // As many copies as fit at about their own proportions, then stretched to fill the length exactly.
            int n = Mathf.Max(1, Mathf.RoundToInt(length / (b.size.x * sy)));
            int rows = backToBack ? 2 : 1;
            var scale = new Vector3(length / n / b.size.x, sy, depth / rows / b.size.z);
            var pivot = Matrix4x4.Translate(-new Vector3(b.center.x, b.min.y, b.center.z));
            for (int row = 0; row < rows; row++)
            {
                float z = backToBack ? (row == 0 ? depth / 4f : -depth / 4f) : 0f;
                var rot = facing * (row == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f));
                for (int i = 0; i < n; i++)
                {
                    var at = floor + facing * new Vector3(-length / 2f + (i + 0.5f) * length / n, 0f, z);
                    var m = Matrix4x4.TRS(at, rot, scale) * pivot;
                    for (int s = 0; s < model.Materials.Length; s++) _decor.Add(model.Mesh, s, m, model.Materials[s], true);
                    ShelveBooks(item.Path, model, at, rot, scale);
                }
            }
        }

        /// <summary>
        /// An iron chandelier hanging from the beam at a lamp's spot, in place of the lantern, if the model's there and
        /// nothing below reaches up into it (a loft or a tall stack, where it would hang in players' faces).
        /// </summary>
        bool HangChandelier(Vector3 lampPosition, PackItem item)
        {
            var model = PropModels.GetPack(item.Path, PackMaterial);
            if (model == null) return false;
            float bottom = lampPosition.y - 0.1f - model.Bounds.size.y;
            // Anything solid between head height on the floor and the chandelier's foot.
            if (Solid(new Vector3(lampPosition.x, (2f + bottom) / 2f, lampPosition.z), new Vector3(1.6f, (bottom - 2f) / 2f, 1.6f), Quaternion.identity)) return false;
            Rod(lampPosition + Vector3.up * 0.28f, lampPosition - Vector3.up * 0.1f, 0.03f, new Color(0.12f, 0.11f, 0.1f), Surface.Metal);
            AddModelHanging(model, lampPosition - Vector3.up * 0.1f, Quaternion.Euler(0f, 45f, 0f));
            return true;
        }

        /// <summary>Tall tapestries on the walls of the chapel, lab and archives, above head height clear of everything.</summary>
        void HangTapestries()
        {
            var model = PropModels.GetPack(Tapestry.Path, PackMaterial);
            if (model == null) return;
            var hung = new Dictionary<int, int>();
            for (int i = 0; i < _faces.Count; i++)
            {
                var f = _faces[i];
                if (!TapestryZones.TryGetValue(f.Zone, out int max) || f.Length < 5f) continue;
                var rng = new System.Random(6100 + i * 271);
                for (float t = Range(rng, 3f, 5f); t < f.Length - 3f; t += Range(rng, 7f, 11f))
                {
                    hung.TryGetValue(f.Zone, out int n);
                    if (n >= max) break;
                    // Hangs from 5 m down to 1.5 m, flat on the wall.
                    if (!WallFree(f, t, 3.25f, 0.75f, 1.8f, 0.12f)) continue;
                    hung[f.Zone] = n + 1;
                    AddModel(model, f.At(t, 1.5f, 0.04f), Quaternion.LookRotation(f.Inward, Vector3.up), false);
                }
            }
        }
    }
}
