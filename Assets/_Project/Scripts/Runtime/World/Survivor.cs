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
            _body = Greybox.Empty("Body", transform, Vector3.zero);
            Greybox.Prim(PrimitiveType.Capsule, "Torso", _body, new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 0.42f, 0.36f), jacket, collider: false);
            Greybox.Prim(PrimitiveType.Capsule, "Legs", _body, new Vector3(0f, 0.3f, 0.15f), new Vector3(0.42f, 0.3f, 0.5f), new Color(0.2f, 0.22f, 0.28f), collider: false);
            Greybox.Prim(PrimitiveType.Sphere, "Head", _body, new Vector3(0f, 1.25f, 0f), Vector3.one * 0.3f, skin, collider: false);
            _arm = Greybox.Empty("Arm", _body, new Vector3(0.28f, 1.0f, 0f));
            Greybox.Box("Forearm", _arm, new Vector3(0f, 0.25f, 0f), new Vector3(0.1f, 0.5f, 0.1f), jacket, collider: false);
            Greybox.Prim(PrimitiveType.Cylinder, "Flare", transform, new Vector3(0.6f, 0.08f, 0.4f), new Vector3(0.05f, 0.08f, 0.05f), new Color(1f, 0.2f, 0.15f), collider: false, unlit: true);
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
            _flare.intensity = 1.6f + Mathf.PerlinNoise(Time.time * 8f, 0.3f) * 1.4f;
            bool flash = _hurtFlash > 0f;
            _hurtFlash -= Time.deltaTime;
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].sharedMaterial = flash ? _white : _materials[i];
        }
    }
}
