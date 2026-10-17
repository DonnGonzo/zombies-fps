using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Player;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// The first-person gun model: greybox geometry plus procedural motion (aim, bob, sway,
    /// recoil kick, sprint and reload poses, muzzle flash). Purely visual; never affects where bullets go.
    /// </summary>
    public class WeaponView : MonoBehaviour
    {
        static readonly Vector3 HipPosition = new Vector3(0.17f, -0.17f, 0.32f);
        // Puts the iron sights on the camera's centre line.
        static readonly Vector3 AimPosition = new Vector3(0f, -0.071f, 0.2f);

        HitscanGun _gun;
        PlayerMotor _motor;
        PlayerLook _look;
        Transform _model;
        GameObject _flash;
        Light _flashLight;
        float _flashUntil;

        float _bobPhase;
        float _kickBack, _kickPitch;
        Vector2 _sway;
        float _sprintWeight;
        Renderer[] _accentParts;
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

        /// <summary>Stretches and tints the shared greybox model to suggest which gun is held.</summary>
        void Restyle()
        {
            var data = _gun.Data;
            if (data == null || _model == null) return;
            _model.localScale = new Vector3(1f, 1f, Mathf.Clamp(data.modelLength, 0.3f, 2f));
            var accent = Greybox.Lit(_gun.Camo ?? data.accent);
            foreach (var r in _accentParts) r.sharedMaterial = accent;
        }

        void BuildModel()
        {
            _model = Greybox.Empty("GunModel", transform, HipPosition);
            var dark = new Color(0.16f, 0.17f, 0.18f);
            var mid = new Color(0.28f, 0.29f, 0.3f);
            var tan = new Color(0.42f, 0.36f, 0.27f);

            Greybox.Box("Receiver", _model, new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.09f, 0.32f), dark, collider: false);
            var handguard = Greybox.Box("Handguard", _model, new Vector3(0f, 0.005f, 0.22f), new Vector3(0.065f, 0.065f, 0.18f), tan, collider: false);
            Greybox.Box("Barrel", _model, new Vector3(0f, 0.015f, 0.36f), new Vector3(0.03f, 0.03f, 0.2f), dark, collider: false);
            var mag = Greybox.Box("Magazine", _model, new Vector3(0f, -0.1f, 0.06f), new Vector3(0.045f, 0.14f, 0.07f), mid, collider: false);
            mag.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            var grip = Greybox.Box("Grip", _model, new Vector3(0f, -0.085f, -0.09f), new Vector3(0.04f, 0.1f, 0.05f), tan, collider: false);
            grip.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            var stock = Greybox.Box("Stock", _model, new Vector3(0f, -0.015f, -0.25f), new Vector3(0.05f, 0.08f, 0.18f), tan, collider: false);
            _accentParts = new[] { handguard.GetComponent<Renderer>(), grip.GetComponent<Renderer>(), stock.GetComponent<Renderer>() };
            // Iron sights: rear notch halves and a front post, tops at y = 0.071.
            Greybox.Box("RearSightL", _model, new Vector3(-0.012f, 0.058f, -0.12f), new Vector3(0.01f, 0.026f, 0.015f), dark, collider: false);
            Greybox.Box("RearSightR", _model, new Vector3(0.012f, 0.058f, -0.12f), new Vector3(0.01f, 0.026f, 0.015f), dark, collider: false);
            Greybox.Box("FrontSight", _model, new Vector3(0f, 0.052f, 0.28f), new Vector3(0.006f, 0.038f, 0.01f), dark, collider: false);
            Greybox.Prim(PrimitiveType.Cube, "FrontSightDot", _model, new Vector3(0f, 0.069f, 0.274f), new Vector3(0.006f, 0.006f, 0.002f),
                new Color(1f, 0.45f, 0.1f), collider: false, unlit: true);

            Muzzle = Greybox.Empty("Muzzle", _model, new Vector3(0f, 0.015f, 0.47f));
            _flash = Greybox.Prim(PrimitiveType.Sphere, "MuzzleFlash", Muzzle, Vector3.zero, new Vector3(0.07f, 0.07f, 0.14f),
                new Color(1f, 0.82f, 0.45f), collider: false, unlit: true);
            _flashLight = _flash.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.range = 7f;
            _flashLight.intensity = 3f;
            _flashLight.color = new Color(1f, 0.75f, 0.4f);
            _flashLight.shadows = LightShadows.None;
            _flash.SetActive(false);

            Greybox.DisableShadows(_model.gameObject);

            // The melee knife, held in the off hand and only shown mid-swing.
            _knife = Greybox.Empty("Knife", transform, new Vector3(-0.12f, -0.16f, 0.36f));
            Greybox.Box("Blade", _knife, new Vector3(0f, 0f, 0.08f), new Vector3(0.012f, 0.035f, 0.16f), new Color(0.82f, 0.84f, 0.87f), collider: false);
            Greybox.Box("Grip", _knife, new Vector3(0f, 0f, -0.04f), new Vector3(0.024f, 0.032f, 0.09f), new Color(0.1f, 0.09f, 0.08f), collider: false);
            Greybox.DisableShadows(_knife.gameObject);
            _knife.gameObject.SetActive(false);
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
            _flash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            _flash.transform.localScale = new Vector3(0.07f, 0.07f, 0.14f) * Random.Range(0.8f, 1.3f);
            _flash.SetActive(true);
        }

        void LateUpdate()
        {
            if (_model == null || _gun == null) return;
            float dt = Time.deltaTime;
            float aim = _gun.AimAmount;
            float aimEased = aim * aim * (3f - 2f * aim);

            if (_flash.activeSelf && Time.time >= _flashUntil) _flash.SetActive(false);

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
