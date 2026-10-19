using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// Energy fields across every doorway of one room, for an easter egg step that seals the squad in. Players
    /// can't walk or jump through them; shots, thrown things' sight lines and zombies' paths pass (the fields are
    /// on the invisible-blocker layer and are never in the NavMesh), so the round's zombies can still come in.
    /// </summary>
    public class Lockdown : MonoBehaviour
    {
        static readonly Color FieldColor = new Color(0.35f, 1f, 0.7f);

        readonly List<GameObject> _fields = new List<GameObject>();
        readonly List<Material> _materials = new List<Material>();
        readonly List<Transform> _bands = new List<Transform>();
        AudioSource[] _hums;
        bool _sealed;
        float _changedAt = -10f;

        public bool Sealed
        {
            get => _sealed;
            set
            {
                if (value == _sealed) return;
                _sealed = value;
                _changedAt = Time.time;
                foreach (var f in _fields) f.SetActive(value);
                if (_hums != null)
                    foreach (var h in _hums)
                    {
                        if (value) h.Play();
                        else h.Stop();
                    }
            }
        }

        /// <summary>Fields for every door into a zone, switched off until sealed.</summary>
        public static Lockdown Build(Transform parent, int zone)
        {
            var root = new GameObject("Lockdown " + ZoneMap.ZoneNames[zone]);
            root.transform.SetParent(parent, false);
            var lockdown = root.AddComponent<Lockdown>();
            var hums = new List<AudioSource>();
            for (int d = 0; d < ZoneMap.DoorLinks.Length; d++)
            {
                var link = ZoneMap.DoorLinks[d];
                if (link.x != zone && link.y != zone) continue;
                var c = ZoneMap.DoorCenter(d);
                var rot = ZoneMap.DoorInWallAlongZ(d) ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

                var field = new GameObject("Field");
                field.transform.SetParent(root.transform, false);
                field.transform.localPosition = c + Vector3.up * (ZoneMap.GapHeight / 2f);
                field.transform.localRotation = rot;
                field.layer = ZoneMap.BlockerLayer;
                field.AddComponent<BoxCollider>().size = new Vector3(ZoneMap.GapWidth + 0.2f, ZoneMap.GapHeight, ZoneMap.WallThickness);

                // A sheet of light, with bands of brighter light running up it.
                var sheet = Greybox.Prim(PrimitiveType.Cube, "Sheet", field.transform, Vector3.zero, new Vector3(ZoneMap.GapWidth, ZoneMap.GapHeight, 0.04f),
                    FieldColor, collider: false);
                var mat = new Material(Greybox.Unlit(new Color(FieldColor.r, FieldColor.g, FieldColor.b, 0.3f)));
                sheet.GetComponent<Renderer>().sharedMaterial = mat;
                lockdown._materials.Add(mat);
                for (int b = 0; b < 3; b++)
                {
                    var band = Greybox.Prim(PrimitiveType.Cube, "Band", field.transform, Vector3.zero, new Vector3(ZoneMap.GapWidth, 0.05f, 0.06f), FieldColor, collider: false, unlit: true);
                    lockdown._bands.Add(band.transform);
                }
                foreach (float x in new[] { -1f, 1f })
                    Greybox.Prim(PrimitiveType.Cube, "Edge", field.transform, new Vector3(x * ZoneMap.GapWidth / 2f, 0f, 0f), new Vector3(0.08f, ZoneMap.GapHeight, 0.1f),
                        FieldColor, collider: false, unlit: true);
                foreach (var r in field.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                var hum = GameSettings.Effect(field.AddComponent<AudioSource>());
                hum.clip = ProceduralAudio.FieldHum;
                hum.loop = true;
                hum.spatialBlend = 1f;
                hum.minDistance = 1.5f;
                hum.maxDistance = 14f;
                hum.rolloffMode = AudioRolloffMode.Linear;
                hum.playOnAwake = false;
                hums.Add(hum);

                field.SetActive(false);
                lockdown._fields.Add(field);
            }
            lockdown._hums = hums.ToArray();
            return lockdown;
        }

        void Update()
        {
            if (!_sealed) return;
            float t = Time.time;
            // Flickers on over the first half second.
            float on = Mathf.Clamp01((t - _changedAt) / 0.5f);
            float alpha = (0.22f + Mathf.Sin(t * 5f) * 0.05f + Mathf.PerlinNoise(t * 7f, 0f) * 0.08f) * (on < 1f && Random.value < 0.4f ? 0.2f : on);
            foreach (var m in _materials)
            {
                var c = m.color;
                c.a = alpha;
                m.color = c;
            }
            for (int i = 0; i < _bands.Count; i++)
            {
                float k = Mathf.Repeat(t * 0.6f + (i % 3) / 3f, 1f);
                var p = _bands[i].localPosition;
                p.y = (k - 0.5f) * ZoneMap.GapHeight;
                _bands[i].localPosition = p;
            }
        }

        void OnDestroy()
        {
            foreach (var m in _materials)
                if (m != null) Destroy(m);
        }
    }
}
