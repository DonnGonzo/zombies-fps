using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Player;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// The first-person gun model: geometry built from primitives for each kind of gun (see
    /// <see cref="GunModels"/>) plus procedural motion (aim, bob, sway, recoil kick, sprint and reload poses,
    /// muzzle flash). Purely visual; never affects where bullets go.
    /// </summary>
    public class WeaponView : MonoBehaviour
    {
        static readonly Vector3 HipPosition = new Vector3(0.17f, -0.17f, 0.32f);
        // Puts the sights (model y = GunModels.SightY) on the camera's centre line.
        static readonly Vector3 AimPosition = new Vector3(0f, -0.071f, 0.2f);
        static readonly Color FlashHot = new Color(1f, 0.93f, 0.72f);
        static readonly Color FlashWarm = new Color(1f, 0.6f, 0.2f);
        static readonly Color FlashLightColor = new Color(1f, 0.75f, 0.4f);

        HitscanGun _gun;
        PlayerMotor _motor;
        PlayerLook _look;
        Transform _model;
        GameObject _flash;
        Light _flashLight;
        Renderer[] _flashCore, _flashProngs;
        Transform[] _prongs;
        float _flashUntil;

        float _bobPhase;
        float _kickBack, _kickPitch;
        Vector2 _sway;
        float _sprintWeight;
        // One model per gun, built the first time it's held.
        readonly Dictionary<GunData, GunModels.Model> _models = new Dictionary<GunData, GunModels.Model>();
        GunModels.Model _current;
        // Upgraded guns glow in their machine tier's colour: strips down the sides and a ring at the muzzle.
        GameObject _glow;
        Renderer[] _glowParts;
        Light _glowLight;
        int _glowTier = -1;
        Transform _knife;
        float _meleeStart = -100f, _throwStart = -100f;
        const float MeleeTime = 0.38f, ThrowTime = 0.45f;

        public Transform Muzzle { get; private set; }

        public void Init(HitscanGun gun, PlayerMotor motor, PlayerLook look)
        {
            _gun = gun;
            _motor = motor;
            _look = look;
            BuildModel();
            Restyle();
            _gun.Equipped += Restyle;
        }

        void OnDestroy()
        {
            if (_gun != null) _gun.Equipped -= Restyle;
        }

        /// <summary>Shows the held gun's model and paints it: its own colours, or the camo picked for it.</summary>
        void Restyle()
        {
            var data = _gun.Data;
            if (data == null || _model == null) return;
            if (!_models.TryGetValue(data, out var m) || m == null || m.Root == null)
            {
                m = GunModels.Build(data, _model);
                Greybox.DisableShadows(m.Root);
                _models[data] = m;
            }
            if (_current != m)
            {
                if (_current != null && _current.Root != null) _current.Root.SetActive(false);
                _current = m;
                m.Root.SetActive(true);
                Muzzle = m.Muzzle;
                _flash.transform.SetParent(m.Muzzle, false);
                _flash.transform.localPosition = Vector3.zero;
                _glow = m.Glow;
                _glowParts = m.GlowParts;
                _glowLight = m.GlowLight;
                _glowTier = -1;
            }

            // Camos paint the whole gun as metal: the furniture in the camo colour, the receiver a shade darker.
            var camo = _gun.Camo;
            Material furniture, body;
            if (camo.HasValue)
            {
                var c = camo.Value;
                furniture = Greybox.Lit(new Color(c.r, c.g, c.b, 1f), Surface.Metal, true);
                body = Greybox.Lit(new Color(c.r * 0.72f, c.g * 0.72f, c.b * 0.72f, 1f), Surface.Metal, true);
            }
            else
            {
                furniture = Greybox.Lit(m.FurnitureColor(data.accent), m.FurnitureSurface, true);
                body = Greybox.Lit(m.BodyColor, m.BodySurface, true);
            }
            foreach (var r in m.Furniture) r.sharedMaterial = furniture;
            foreach (var r in m.Body) r.sharedMaterial = body;
            if (m.Cores.Count > 0)
            {
                var core = Greybox.Unlit(new Color(data.accent.r, data.accent.g, data.accent.b, 1f));
                foreach (var r in m.Cores) r.sharedMaterial = core;
            }

            // Energy guns flash in their own colour.
            var hot = m.EnergyFlash ? Color.Lerp(data.accent, Color.white, 0.5f) : FlashHot;
            var warm = m.EnergyFlash ? data.accent : FlashWarm;
            var hotMat = Greybox.Unlit(new Color(hot.r, hot.g, hot.b, 1f));
            var warmMat = Greybox.Unlit(new Color(warm.r, warm.g, warm.b, 1f));
            foreach (var r in _flashCore) r.sharedMaterial = hotMat;
            foreach (var r in _flashProngs) r.sharedMaterial = warmMat;
            _flashLight.color = m.EnergyFlash ? new Color(warm.r, warm.g, warm.b) : FlashLightColor;
        }

        void BuildModel()
        {
            _model = Greybox.Empty("GunModel", transform, HipPosition);
            // Until a gun is equipped the muzzle sits where a rifle's would.
            Muzzle = Greybox.Empty("Muzzle", _model, new Vector3(0f, 0.015f, 0.47f));
            BuildFlash();

            // The melee knife, held in the off hand and only shown mid-swing.
            _knife = Greybox.Empty("Knife", transform, new Vector3(-0.12f, -0.16f, 0.36f));
            Greybox.Box("Blade", _knife, new Vector3(0f, 0f, 0.08f), new Vector3(0.012f, 0.035f, 0.16f), new Color(0.78f, 0.8f, 0.83f), collider: false,
                surface: Surface.Metal, moving: true);
            Greybox.Box("Spine", _knife, new Vector3(0f, 0.016f, 0.07f), new Vector3(0.013f, 0.005f, 0.13f), new Color(0.3f, 0.31f, 0.33f), collider: false,
                surface: Surface.Metal, moving: true);
            Greybox.Box("Guard", _knife, new Vector3(0f, 0f, 0.005f), new Vector3(0.016f, 0.05f, 0.01f), new Color(0.15f, 0.15f, 0.16f), collider: false,
                surface: Surface.Metal, moving: true);
            Greybox.Box("Grip", _knife, new Vector3(0f, 0f, -0.04f), new Vector3(0.024f, 0.032f, 0.09f), new Color(0.1f, 0.09f, 0.08f), collider: false,
                surface: Surface.Rubber, moving: true);
            Greybox.Box("Pommel", _knife, new Vector3(0f, 0f, -0.088f), new Vector3(0.026f, 0.034f, 0.008f), new Color(0.3f, 0.31f, 0.33f), collider: false,
                surface: Surface.Metal, moving: true);
            Greybox.DisableShadows(_knife.gameObject);
            _knife.gameObject.SetActive(false);
        }

        /// <summary>
        /// A bright star at the muzzle: a hot core, a forward spike and five prongs, each shot turned and sized
        /// at random. Glows past white so the bloom catches it.
        /// </summary>
        void BuildFlash()
        {
            _flash = Greybox.Empty("MuzzleFlash", Muzzle, Vector3.zero).gameObject;
            var core = Greybox.Prim(PrimitiveType.Sphere, "Core", _flash.transform, new Vector3(0f, 0f, 0.012f), new Vector3(0.042f, 0.042f, 0.07f),
                FlashHot, collider: false, unlit: true);
            var spike = Greybox.Prim(PrimitiveType.Cube, "Spike", _flash.transform, new Vector3(0f, 0f, 0.07f), new Vector3(0.014f, 0.014f, 0.12f),
                FlashWarm, collider: false, unlit: true);
            spike.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var spikeCore = Greybox.Prim(PrimitiveType.Cube, "SpikeCore", _flash.transform, new Vector3(0f, 0f, 0.045f), new Vector3(0.008f, 0.008f, 0.07f),
                FlashHot, collider: false, unlit: true);
            _prongs = new Transform[5];
            _flashProngs = new Renderer[_prongs.Length + 1];
            for (int i = 0; i < _prongs.Length; i++)
            {
                var pivot = Greybox.Empty("Prong", _flash.transform, new Vector3(0f, 0f, 0.008f));
                pivot.localRotation = Quaternion.Euler(0f, 0f, i * 72f);
                var prong = Greybox.Prim(PrimitiveType.Cube, "Blade", pivot, new Vector3(0f, 0.04f, 0f), new Vector3(0.01f, 0.08f, 0.003f),
                    FlashWarm, collider: false, unlit: true);
                _prongs[i] = pivot;
                _flashProngs[i] = prong.GetComponent<Renderer>();
            }
            _flashProngs[_prongs.Length] = spike.GetComponent<Renderer>();
            _flashCore = new[] { core.GetComponent<Renderer>(), spikeCore.GetComponent<Renderer>() };
            _flashLight = _flash.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = 7f;
            _flashLight.intensity = 3f;
            _flashLight.color = FlashLightColor;
            _flashLight.shadows = LightShadows.None;
            Greybox.DisableShadows(_flash);
            _flash.SetActive(false);
        }

        /// <summary>Lights the upgrade glow in the tier's colour, gently pulsing and flaring with each shot.</summary>
        void UpdateGlow()
        {
            if (_glow == null) return;
            int tier = _gun.UpgradeTier;
            if (tier != _glowTier)
            {
                _glowTier = tier;
                _glow.SetActive(tier > 0);
                if (tier > 0)
                {
                    var color = UpgradeData.TierGlow(tier);
                    var mat = Greybox.Unlit(color);
                    foreach (var r in _glowParts) r.sharedMaterial = mat;
                    _glowLight.color = color;
                }
            }
            if (tier <= 0) return;
            float sinceShot = Time.time - _gun.LastShotTime;
            float flare = sinceShot < 0.15f ? 1f - sinceShot / 0.15f : 0f;
            _glowLight.intensity = 0.9f + 0.35f * Mathf.Sin(Time.time * (2.5f + tier)) + 1.6f * flare;
        }

        /// <summary>A knife slash across the screen; the gun ducks out of the way.</summary>
        public void Melee() => _meleeStart = Time.time;

        /// <summary>The gun dips while the off hand throws.</summary>
        public void Throw() => _throwStart = Time.time;

        public void OnFire()
        {
            float aim = _gun != null ? _gun.AimAmount : 0f;
            _kickBack += Mathf.Lerp(0.035f, 0.018f, aim);
            _kickPitch += Mathf.Lerp(3.5f, 1.2f, aim);
            _flashUntil = Time.time + 0.035f;
            float scale = _current != null ? _current.FlashScale : 1f;
            _flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _flash.transform.localScale = Vector3.one * (scale * Random.Range(0.8f, 1.3f));
            foreach (var p in _prongs) p.localScale = new Vector3(1f, Random.Range(0.5f, 1.5f), 1f);
            _flashLight.intensity = Random.Range(2.5f, 3.5f);
            _flash.SetActive(true);
        }

        void LateUpdate()
        {
            if (_model == null || _gun == null) return;
            float dt = Time.deltaTime;
            float aim = _gun.AimAmount;
            float aimEased = aim * aim * (3f - 2f * aim);

            if (_flash.activeSelf && Time.time >= _flashUntil) _flash.SetActive(false);
            UpdateGlow();
            // A scope's tube would fill the view when looking through it; only its front ring and reticle stay.
            if (_current != null && _current.HideWhenAimed != null)
            {
                bool show = aimEased < 0.9f;
                if (_current.HideWhenAimed.activeSelf != show) _current.HideWhenAimed.SetActive(show);
            }

            // Bob from movement.
            float speed01 = _motor.IsGrounded ? Mathf.Clamp01(_motor.PlanarSpeed / _motor.sprintSpeed) : 0f;
            _bobPhase += dt * (6f + 6f * speed01) * (speed01 > 0.05f ? 1f : 0.2f);
            float bobAmount = speed01 * (1f - aimEased * 0.85f);
            Vector3 bob = new Vector3(Mathf.Sin(_bobPhase) * 0.018f, -Mathf.Abs(Mathf.Cos(_bobPhase)) * 0.014f, 0f) * bobAmount;
            // Idle breathing.
            bob.y += Mathf.Sin(Time.time * 1.6f) * 0.002f * (1f - aimEased);

            // Sway lags behind the look direction.
            Vector2 look = _look.LastLookDelta;
            Vector2 swayTarget = new Vector2(Mathf.Clamp(-look.y * 0.5f, -4f, 4f), Mathf.Clamp(look.x * 0.5f, -4f, 4f)) * (1f - aimEased * 0.7f);
            _sway = Vector2.Lerp(_sway, swayTarget, 1f - Mathf.Exp(-10f * dt));

            // Recoil spring back to rest.
            float recover = 1f - Mathf.Exp(-16f * dt);
            _kickBack = Mathf.Lerp(_kickBack, 0f, recover);
            _kickPitch = Mathf.Lerp(_kickPitch, 0f, recover);

            _sprintWeight = Mathf.MoveTowards(_sprintWeight, _motor.IsSprinting ? 1f : 0f, dt * 5f);
            float reloadWeight = _gun.IsReloading ? Mathf.Sin(Mathf.PI * _gun.ReloadProgress) : 0f;
            reloadWeight = Mathf.Clamp01(reloadWeight * 1.6f);
            float lowered = 1f - _gun.RaiseProgress;
            lowered *= lowered;

            Vector3 pos = Vector3.Lerp(HipPosition, AimPosition, aimEased) + bob
                          + new Vector3(-0.05f, -0.05f, -0.02f) * _sprintWeight
                          + new Vector3(0f, -0.1f, -0.02f) * reloadWeight
                          + new Vector3(0f, -0.22f, -0.06f) * lowered
                          + new Vector3(0f, 0f, -_kickBack);
            Vector3 euler = new Vector3(_sway.x - _kickPitch, _sway.y, 0f)
                            + new Vector3(12f, -32f, 12f) * _sprintWeight
                            + new Vector3(35f, 0f, 28f) * reloadWeight
                            + new Vector3(40f, 0f, 0f) * lowered;

            // Melee and throws pull the gun down and to the side for a moment.
            float melee = Mathf.Clamp01((Time.time - _meleeStart) / MeleeTime);
            float meleeWeight = melee < 1f ? Mathf.Sin(Mathf.PI * melee) : 0f;
            float thrown = Mathf.Clamp01((Time.time - _throwStart) / ThrowTime);
            float throwWeight = thrown < 1f ? Mathf.Sin(Mathf.PI * thrown) : 0f;
            pos += new Vector3(0.06f, -0.12f, -0.04f) * meleeWeight + new Vector3(0.02f, -0.16f, -0.05f) * throwWeight;
            euler += new Vector3(25f, 20f, -25f) * meleeWeight + new Vector3(30f, 0f, -10f) * throwWeight;

            bool slashing = melee < 1f;
            if (_knife.gameObject.activeSelf != slashing) _knife.gameObject.SetActive(slashing);
            if (slashing)
            {
                float k = melee * melee * (3f - 2f * melee);
                _knife.localPosition = Vector3.Lerp(new Vector3(0.28f, 0.02f, 0.3f), new Vector3(-0.3f, -0.18f, 0.42f), k);
                _knife.localRotation = Quaternion.Euler(Mathf.Lerp(-20f, 30f, k), Mathf.Lerp(-60f, -110f, k), Mathf.Lerp(-30f, -80f, k));
            }

            _model.localPosition = pos;
            _model.localRotation = Quaternion.Euler(euler);
        }
    }
}
