using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Core;
using ZombiesFPS.Weapons;

namespace ZombiesFPS.World
{
    /// <summary>
    /// A buyable door: an iron portcullis with bronze bands and a rune seal, hung in a dressed stone frame with
    /// a power lamp on the lintel. Red while the facility has no power, amber once it can be bought, green when
    /// open. Closed, it blocks players with its collider and zombies by carving the NavMesh. Opening winds the
    /// gate up into the wall. Purely local: MapState tells every machine which doors are open and whether the
    /// power is on.
    /// </summary>
    public class Door : MonoBehaviour
    {
        const float RaiseTime = 1.7f;
        const float ShakeTime = 0.25f;

        public static readonly Color NoPowerColor = new Color(1f, 0.16f, 0.1f);
        public static readonly Color PoweredColor = new Color(1f, 0.66f, 0.18f);
        public static readonly Color OpenColor = new Color(0.3f, 1f, 0.45f);

        static readonly Color Iron = new Color(0.17f, 0.17f, 0.18f);
        static readonly Color Bronze = new Color(0.55f, 0.38f, 0.17f);
        static readonly Color Stone = new Color(0.5f, 0.47f, 0.42f);
        static readonly Color StoneDark = new Color(0.36f, 0.34f, 0.31f);
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Collider _collider;
        NavMeshObstacle _obstacle;
        AudioSource _audio;
        Transform _gate;
        Renderer[] _gateRenderers;
        Renderer[] _glows;
        MaterialPropertyBlock _block;
        float _openedAt = -1f;
        float _gateLift;
        bool _powered;

        public int Index { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsPowered => _powered;

        public void Init(int index)
        {
            Index = index;
            _collider = GetComponent<Collider>();
            _obstacle = gameObject.AddComponent<NavMeshObstacle>();
            _obstacle.shape = NavMeshObstacleShape.Box;
            _obstacle.size = Vector3.one; // Local space, so it matches the scaled box.
            _obstacle.carving = true;
            _obstacle.carveOnlyStationary = true;
            _audio = GameSettings.Effect(gameObject.AddComponent<AudioSource>());
            _audio.spatialBlend = 1f;
            _audio.minDistance = 4f;
            _audio.maxDistance = 50f;
            _audio.playOnAwake = false;

            // The scaled box only blocks; the art is built unscaled beside it, so its parts keep their shape.
            var own = GetComponent<Renderer>();
            if (own != null) own.enabled = false;
            BuildArt();
            SetPowered(false);
        }

        void BuildArt()
        {
            float w = ZoneMap.GapWidth, h = ZoneMap.GapHeight, depth = ZoneMap.WallThickness;
            var art = Greybox.Empty("Door Art", transform.parent, transform.localPosition + transform.localRotation * (Vector3.down * (h / 2f)));
            art.localRotation = transform.localRotation;
            var glows = new System.Collections.Generic.List<Renderer>();

            // The frame: dressed stone jambs and a lintel, a little proud of the wall on both sides.
            float jamb = 0.44f, frameDepth = depth + 0.24f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (w / 2f + jamb / 2f);
                Greybox.Box("Jamb", art, new Vector3(x, (h + 0.1f) / 2f, 0f), new Vector3(jamb, h + 0.1f, frameDepth), Stone, collider: false, surface: Surface.Ashlar);
                Greybox.Box("Plinth", art, new Vector3(x, 0.2f, 0f), new Vector3(jamb + 0.1f, 0.4f, frameDepth + 0.08f), StoneDark, collider: false, surface: Surface.Ashlar);
                // A bronze conduit up each jamb on both faces, its rune channel lit by the power.
                for (int face = -1; face <= 1; face += 2)
                {
                    float z = face * (frameDepth / 2f + 0.015f);
                    Greybox.Box("Conduit", art, new Vector3(x, h / 2f + 0.15f, z), new Vector3(0.14f, h - 0.5f, 0.03f), Bronze, collider: false, surface: Surface.Metal);
                    glows.Add(Glow("Channel", art, new Vector3(x, h / 2f + 0.15f, z + face * 0.017f), new Vector3(0.04f, h - 0.7f, 0.006f)));
                }
            }
            float lintelY = h + 0.27f;
            Greybox.Box("Lintel", art, new Vector3(0f, lintelY, 0f), new Vector3(w + jamb * 2f + 0.16f, 0.54f, frameDepth + 0.06f), Stone, collider: false, surface: Surface.Ashlar);
            for (int face = -1; face <= 1; face += 2)
            {
                float z = face * ((frameDepth + 0.06f) / 2f);
                // A bronze keystone with the power lamp set in it, and the gate's winding housing beside it.
                Greybox.Box("Keystone", art, new Vector3(0f, lintelY, z + face * 0.04f), new Vector3(0.62f, 0.5f, 0.08f), Bronze, collider: false, surface: Surface.Metal);
                Greybox.Box("LampRim", art, new Vector3(0f, lintelY, z + face * 0.09f), new Vector3(0.3f, 0.3f, 0.04f), Iron, collider: false, surface: Surface.Metal);
                var lamp = Greybox.Prim(PrimitiveType.Sphere, "Lamp", art, new Vector3(0f, lintelY, z + face * 0.11f), new Vector3(0.22f, 0.22f, 0.1f), NoPowerColor, collider: false, unlit: true);
                glows.Add(lamp.GetComponent<Renderer>());
                for (int side = -1; side <= 1; side += 2)
                {
                    var drum = Greybox.Prim(PrimitiveType.Cylinder, "Winch", art, new Vector3(side * (w / 2f - 0.35f), lintelY, z + face * 0.1f), new Vector3(0.32f, 0.06f, 0.32f), Iron,
                        collider: false, surface: Surface.Metal);
                    drum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    glows.Add(Glow("Rune", art, new Vector3(side * (w / 2f - 0.35f), lintelY, z + face * 0.165f), new Vector3(0.12f, 0.12f, 0.006f)));
                }
            }

            // The gate: iron bars with bronze cross bands, spiked feet and a round rune seal in the middle.
            _gate = Greybox.Empty("Gate", art, Vector3.zero);
            const int bars = 7, bands = 4;
            float bar = 0.09f, span = w - 0.12f;
            for (int i = 0; i < bars; i++)
            {
                float x = -span / 2f + span * i / (bars - 1);
                Greybox.Box("Bar", _gate, new Vector3(x, h / 2f + 0.05f, 0f), new Vector3(bar, h - 0.1f, bar), Iron, collider: false, surface: Surface.Metal, moving: true);
                var spike = Greybox.Box("Spike", _gate, new Vector3(x, 0.07f, 0f), new Vector3(0.1f, 0.1f, 0.1f), Iron, collider: false, surface: Surface.Metal, moving: true);
                spike.transform.localRotation = Quaternion.Euler(0f, 45f, 45f);
            }
            for (int i = 0; i < bands; i++)
            {
                float y = 0.45f + (h - 0.8f) * i / (bands - 1);
                Greybox.Box("Band", _gate, new Vector3(0f, y, 0f), new Vector3(w - 0.04f, 0.12f, 0.14f), Bronze, collider: false, surface: Surface.Metal, moving: true);
            }
            float sealY = h * 0.55f;
            var seal = Greybox.Prim(PrimitiveType.Cylinder, "Seal", _gate, new Vector3(0f, sealY, 0f), new Vector3(0.95f, 0.07f, 0.95f), Bronze, collider: false, surface: Surface.Metal, moving: true);
            seal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = Glow("SealRing", _gate, new Vector3(0f, sealY, 0f), new Vector3(0.78f, 0.075f, 0.78f), PrimitiveType.Cylinder);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glows.Add(ring);
            var hub = Greybox.Prim(PrimitiveType.Cylinder, "SealHub", _gate, new Vector3(0f, sealY, 0f), new Vector3(0.6f, 0.08f, 0.6f), Bronze, collider: false, surface: Surface.Metal, moving: true);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int face = -1; face <= 1; face += 2)
            {
                var eye = Greybox.Box("SealEye", _gate, new Vector3(0f, sealY, face * 0.085f), new Vector3(0.16f, 0.16f, 0.01f), NoPowerColor, collider: false);
                eye.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                eye.GetComponent<Renderer>().sharedMaterial = Greybox.Unlit(NoPowerColor);
                glows.Add(eye.GetComponent<Renderer>());
            }
            _gateRenderers = _gate.GetComponentsInChildren<Renderer>(true);
            _glows = glows.ToArray();
            _block = new MaterialPropertyBlock();
        }

        static Renderer Glow(string name, Transform parent, Vector3 pos, Vector3 size, PrimitiveType type = PrimitiveType.Cube)
            => Greybox.Prim(type, name, parent, pos, size, NoPowerColor, collider: false, unlit: true).GetComponent<Renderer>();

        /// <summary>Whether the facility has power, so the door can be bought (amber) or not (red).</summary>
        public void SetPowered(bool powered)
        {
            _powered = powered;
            Tint(IsOpen ? OpenColor : powered ? PoweredColor : NoPowerColor);
        }

        void Tint(Color color)
        {
            if (_glows == null) return;
            _block.SetColor(ColorId, color);
            foreach (var r in _glows) r.SetPropertyBlock(_block);
        }

        public void SetOpen(bool open, bool instant)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            _collider.enabled = !open;
            _obstacle.enabled = !open;
            if (open)
            {
                _openedAt = instant ? Time.time - RaiseTime : Time.time;
                if (!instant)
                {
                    FxSystem.Instance?.Impact(transform.position + Vector3.down * (ZoneMap.GapHeight / 2f - 0.1f), Vector3.up, FxSystem.Dust, 10, 2f, 0.06f);
                    _audio.PlayOneShot(ProceduralAudio.GateRaise, 1f);
                }
                Tint(OpenColor);
            }
            else
            {
                _openedAt = -1f;
                _gateLift = 0f;
                if (_gate != null) _gate.localPosition = Vector3.zero;
                SetVisible(true);
                SetPowered(_powered);
            }
        }

        void Update()
        {
            if (_openedAt >= 0f)
            {
                // A judder as the winch takes the weight, then the gate winds up into the wall above.
                float e = Time.time - _openedAt;
                float t = Mathf.Clamp01((e - ShakeTime) / (RaiseTime - ShakeTime));
                _gateLift = (ZoneMap.GapHeight + 0.15f) * Mathf.SmoothStep(0f, 1f, t);
                float shake = e < ShakeTime ? Mathf.Sin(e * 90f) * 0.025f : 0f;
                if (_gate != null) _gate.localPosition = new Vector3(shake, _gateLift + Mathf.Abs(shake), 0f);
                if (e >= RaiseTime)
                {
                    SetVisible(false);
                    _openedAt = -1f;
                }
                return;
            }
            // A powered door waiting to be bought breathes, so it reads as live.
            if (_powered && !IsOpen)
            {
                var c = PoweredColor * (0.85f + 0.15f * Mathf.Sin(Time.time * 3f + Index));
                c.a = 1f;
                Tint(c);
            }
        }

        void SetVisible(bool visible)
        {
            if (_gateRenderers == null) return;
            foreach (var r in _gateRenderers) r.enabled = visible;
        }
    }
}
