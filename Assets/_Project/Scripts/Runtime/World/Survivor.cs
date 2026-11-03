using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The rescue NPC for protect missions: a survivor crouched by a lit flare, waving for help. Purely visual
    /// on every machine; the host's MissionBoard decides where they are and how hurt they get.
    /// </summary>
    public class Survivor : MonoBehaviour
    {
        Transform _body, _arm;
        Light _flare;
        Transform _flareGlow;
        float _hurtFlash;
        Renderer[] _renderers;
        Material[] _materials;
        Material _white;

        public static Survivor Create()
        {
            var go = new GameObject("Survivor");
            var s = go.AddComponent<Survivor>();
            s.Build();
            go.SetActive(false);
            return s;
        }

        void Build()
        {
            var jacket = new Color(0.75f, 0.45f, 0.15f);
            var skin = new Color(0.8f, 0.64f, 0.52f);
            var jeans = new Color(0.2f, 0.22f, 0.28f);
            _body = Greybox.Empty("Body", transform, Vector3.zero);
            Greybox.Prim(PrimitiveType.Capsule, "Torso", _body, new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 0.42f, 0.36f), jacket, collider: false,
                surface: Surface.Fabric, moving: true);
            Greybox.Box("ReflectiveStripe", _body, new Vector3(0f, 0.72f, 0f), new Vector3(0.49f, 0.04f, 0.37f), new Color(0.8f, 0.82f, 0.78f), collider: false,
                surface: Surface.Fabric, moving: true);
            Greybox.Box("Backpack", _body, new Vector3(0f, 0.8f, -0.2f), new Vector3(0.34f, 0.38f, 0.14f), new Color(0.22f, 0.26f, 0.18f), collider: false,
                surface: Surface.Fabric, moving: true);
            Greybox.Prim(PrimitiveType.Capsule, "Legs", _body, new Vector3(0f, 0.3f, 0.15f), new Vector3(0.42f, 0.3f, 0.5f), jeans, collider: false,
                surface: Surface.Fabric, moving: true);
            Greybox.Box("BootL", _body, new Vector3(-0.1f, 0.06f, 0.38f), new Vector3(0.12f, 0.12f, 0.2f), new Color(0.16f, 0.12f, 0.09f), collider: false,
                surface: Surface.Rubber, moving: true);
            Greybox.Box("BootR", _body, new Vector3(0.1f, 0.06f, 0.38f), new Vector3(0.12f, 0.12f, 0.2f), new Color(0.16f, 0.12f, 0.09f), collider: false,
                surface: Surface.Rubber, moving: true);
            Greybox.Prim(PrimitiveType.Sphere, "Head", _body, new Vector3(0f, 1.25f, 0f), Vector3.one * 0.3f, skin, collider: false,
                surface: Surface.Skin, moving: true);
            Greybox.Prim(PrimitiveType.Sphere, "Beanie", _body, new Vector3(0f, 1.32f, -0.01f), new Vector3(0.31f, 0.2f, 0.31f), new Color(0.55f, 0.12f, 0.1f), collider: false,
                surface: Surface.Fabric, moving: true);
            _arm = Greybox.Empty("Arm", _body, new Vector3(0.28f, 1.0f, 0f));
            Greybox.Box("Forearm", _arm, new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.44f, 0.1f), jacket, collider: false,
                surface: Surface.Fabric, moving: true);
            Greybox.Prim(PrimitiveType.Sphere, "Hand", _arm, new Vector3(0f, 0.48f, 0f), Vector3.one * 0.1f, skin, collider: false,
                surface: Surface.Skin, moving: true);
            // A road flare: red paper tube, burning bright at the tip.
            Greybox.Prim(PrimitiveType.Cylinder, "Flare", transform, new Vector3(0.6f, 0.08f, 0.4f), new Vector3(0.05f, 0.08f, 0.05f), new Color(0.7f, 0.1f, 0.08f), collider: false,
                surface: Surface.Fabric, moving: true);
            Greybox.Prim(PrimitiveType.Sphere, "FlareTip", transform, new Vector3(0.6f, 0.175f, 0.4f), Vector3.one * 0.06f, new Color(1f, 0.55f, 0.4f), collider: false, unlit: true);
            _flareGlow = Greybox.Prim(PrimitiveType.Sphere, "FlareGlow", transform, new Vector3(0.6f, 0.2f, 0.4f), Vector3.one * 0.12f, new Color(1f, 0.2f, 0.15f, 0.35f),
                collider: false, unlit: true).transform;
            Greybox.DisableShadows(_flareGlow.gameObject);
            _flare = new GameObject("Flare Light").AddComponent<Light>();
            _flare.transform.SetParent(transform, false);
            _flare.transform.localPosition = new Vector3(0.6f, 0.4f, 0.4f);
            _flare.type = LightType.Point;
            _flare.color = new Color(1f, 0.25f, 0.15f);
            _flare.range = 6f;
            _flare.intensity = 2f;
            _flare.shadows = LightShadows.None;
            _renderers = _body.GetComponentsInChildren<Renderer>();
            _materials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _materials[i] = _renderers[i].sharedMaterial;
            _white = Greybox.Lit(Color.white);
        }

        public void Show(Vector3 position, Vector3 facing)
        {
            transform.position = position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>A quick white flash when they take a hit.</summary>
        public void Hurt() => _hurtFlash = 0.08f;

        void Update()
        {
            _arm.localRotation = Quaternion.Euler(0f, 0f, -20f + Mathf.Sin(Time.time * 6f) * 35f);
            float flicker = Mathf.PerlinNoise(Time.time * 8f, 0.3f);
            _flare.intensity = 1.6f + flicker * 1.4f;
            _flareGlow.localScale = Vector3.one * (0.1f + flicker * 0.08f);
            bool flash = _hurtFlash > 0f;
            _hurtFlash -= Time.deltaTime;
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterial = flash ? _white : _materials[i];
        }
    }
}
