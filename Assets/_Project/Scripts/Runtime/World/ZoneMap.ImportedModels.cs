using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    public partial class ZoneMap
    {
        static string ImportedReplacement(PackItem item)
        {
            if (item.Path == Pew.Path) return "church-bench-2";
            if (item.Path == SickBed.Path) return "hospital-bed";
            if (item.Path == GaolCell.Path) return "prison-cell";
            return null;
        }

        // Fits only the visual; the caller creates the same collider with Map Art enabled or disabled.
        bool ImportedDecor(string id, Vector3 floor, Quaternion rotation, Vector3 size)
        {
            if (!SurfaceTextures.PhotosEnabled || id == null) return false;
            var prefab = ImportedModels.Get(id);
            if (prefab == null) return false;
            var b = ImportedModels.Bounds(prefab);
            if (b.size.x < 0.0001f || b.size.y < 0.0001f || b.size.z < 0.0001f) return false;
            var scale = new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z);
            var matrix = Matrix4x4.TRS(floor, rotation, scale) * Matrix4x4.Translate(-new Vector3(b.center.x, b.min.y, b.center.z));
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                var transform = matrix * prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                    _decor.Add(filter.sharedMesh, sub, transform, renderer.sharedMaterials[sub], true);
            }
            return true;
        }

        void ImportedGarageVehicle()
        {
            var size = new Vector3(2f, 1.9f, 4.2f);
            var box = Greybox.Box("Garage Vehicle on lift", Root, GR(-8f, 1.6f, -6f), size, GarageTeal, surface: Surface.Metal);
            if (ImportedDecor("zombie-pickup-truck", GR(-8f, 0.65f, -6f), Quaternion.identity, size))
                box.GetComponent<Renderer>().enabled = false;
        }

        bool _importedFreightTruck;
        void ImportedFreightTruck()
        {
            // Keep the established roof, ladder and collectible at (-6.5, 3, -3).
            var body = Greybox.Box("Freight truck", Root, LD(-6.5f, 1.5f, -2.5f), new Vector3(3f, 3f, 7f), DockSteel, surface: Surface.Metal);
            var cab = Greybox.Box("Truck cab", Root, LD(-6.5f, 1.2f, 2.2f), new Vector3(2.8f, 2.4f, 2.2f), DockRust, surface: Surface.Metal);
            // Preserve the source truck's proportions. A hitched freight trailer fills the old long roof footprint.
            _importedFreightTruck = ImportedDecor("hl2-beta-air-exchange-truck-remade", LD(-6.5f, 0f, 0.95f), Quaternion.identity, new Vector3(2.6f, 3f, 4.7f));
            if (!_importedFreightTruck) return;
            body.GetComponent<Renderer>().enabled = false;
            cab.GetComponent<Renderer>().enabled = false;
            Deco(LD(-6.5f, 1.8f, -3.7f), new Vector3(2.9f, 2.4f, 4.6f), DockSteel, Surface.Metal);
            ContainerDetails(LoadingDockZone, -7.95f, -6f, -5.05f, -1.4f, 3f, DockSteel);
            Deco(LD(-6.5f, 0.5f, -3.7f), new Vector3(2.8f, 0.25f, 4.5f), Soot, Surface.Metal);
            foreach (float x in new[] { -7.95f, -5.05f })
                foreach (float z in new[] { -5.1f, -4f })
                    Rod(LD(x - 0.12f, 0.45f, z), LD(x + 0.12f, 0.45f, z), 0.9f, Soot, Surface.Rubber);
            // A narrow maintenance tread explains the existing walkable roof and ladder landing.
            Deco(LD(-6.5f, 2.985f, -2.5f), new Vector3(1.1f, 0.03f, 6.9f), DockSteel, Surface.Metal);
            for (float z = -5.8f; z < 0.9f; z += 0.3f)
                Deco(LD(-6.5f, 3.002f, z), new Vector3(1.08f, 0.006f, 0.04f), DockRust, Surface.Rust);
        }
    }
}
