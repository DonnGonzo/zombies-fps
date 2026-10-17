using System;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Player;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Hitscan gun driven by GunData: fire rate, magazine + reserve, reload, aim down sights,
    /// spread/bloom and recoil. The local raycast only drives effects; the shot itself goes to
    /// the host through <see cref="Fired"/>, and the host's verdict comes back via <see cref="ConfirmHit"/>.
    /// </summary>
    public class HitscanGun : MonoBehaviour
    {
        public float baseFov = 75f;

        GunData _data;
        Camera _camera;
        PlayerLook _look;
        PlayerMotor _motor;
        Func<bool> _canShoot;
        AudioSource _audio;
        AudioSource _uiAudio;
        WeaponView _view;

        float _nextFireTime;
        float _bloom;
        float _reloadEndTime;
        float _reloadDuration;
        bool _magOutPlayed;
        bool _dryFirePlayed;

        public GunData Data => _data;
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? 1f - Mathf.Clamp01((_reloadEndTime - Time.time) / _reloadDuration) : 0f;
        public float AimAmount { get; private set; }
        public float CurrentSpread { get; private set; }
        public float LastShotTime { get; private set; } = -100f;

        /// <summary>Raised when the host confirms a hit: (killed, headshot).</summary>
        public event Action<bool, bool> Hit;

        /// <summary>Raised on every shot with (origin, direction) so the host can resolve it.</summary>
        public event Action<Vector3, Vector3> Fired;

        public void Init(GunData data, Camera cam, PlayerLook look, PlayerMotor motor, Func<bool> canShoot, AudioSource gunAudio, AudioSource uiAudio, WeaponView view)
        {
            _data = data;
            _camera = cam;
            _look = look;
            _motor = motor;
            _canShoot = canShoot;
            _audio = gunAudio;
            _uiAudio = uiAudio;
            _view = view;
            Magazine = data.magazineSize;
            Reserve = data.startingReserve;
            _camera.fieldOfView = baseFov;
        }

        void Update()
        {
            if (_data == null) return;
            float dt = Time.deltaTime;
            bool canAct = _canShoot() && _look.CursorLocked && !_look.LockedThisFrame;

            UpdateReload();

            bool wantsAim = canAct && GameInput.AimHeld && !IsReloading && !_motor.IsSprinting;
            AimAmount = Mathf.MoveTowards(AimAmount, wantsAim ? 1f : 0f, dt / _data.aimTime);
            _look.AimAmount = AimAmount;
            float eased = AimAmount * AimAmount * (3f - 2f * AimAmount);
            _camera.fieldOfView = Mathf.Lerp(baseFov, _data.aimFov, eased);

            _bloom = Mathf.MoveTowards(_bloom, 0f, dt * 4f);
            float moveFactor = Mathf.Clamp01(_motor.PlanarSpeed / _motor.walkSpeed);
            float airPenalty = _motor.IsGrounded ? 0f : 2f;
            CurrentSpread = Mathf.Lerp(_data.hipSpread, _data.aimSpread, eased)
                            + (moveFactor * _data.movingSpread + airPenalty) * (1f - eased * 0.75f)
                            + _bloom;

            if (!canAct) return;

            if (GameInput.ReloadPressed) TryStartReload();

            bool trigger = _data.automatic ? GameInput.FireHeld : GameInput.FirePressed;
            if (!GameInput.FireHeld) _dryFirePlayed = false;
            if (trigger && !IsReloading && !_motor.IsSprinting && Time.time >= _nextFireTime)
            {
                if (Magazine > 0)
                {
                    Fire();
                }
                else
                {
                    if (!_dryFirePlayed)
                    {
                        _uiAudio.PlayOneShot(ProceduralAudio.DryFire, 0.8f);
                        _dryFirePlayed = true;
                    }
                    TryStartReload();
                }
            }
        }

        void Fire()
        {
            Magazine--;
            _nextFireTime = Time.time + _data.SecondsPerShot;
            LastShotTime = Time.time;

            Transform cam = _camera.transform;
            Vector2 offset = UnityEngine.Random.insideUnitCircle * CurrentSpread;
            Vector3 dir = Quaternion.AngleAxis(offset.x, cam.up) * Quaternion.AngleAxis(offset.y, cam.right) * cam.forward;

            Vector3 end = cam.position + dir * _data.range;
            // Local raycast is for effects only; damage is decided by the host.
            if (Physics.Raycast(cam.position, dir, out var hit, _data.range, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                bool flesh = hitbox != null && hitbox.Owner != null && hitbox.Owner.IsAlive;
                if (flesh) FxSystem.Instance?.Impact(hit.point, hit.normal, FxSystem.Blood, hitbox.isHead ? 9 : 5, 3.5f, 0.07f);
                else FxSystem.Instance?.Impact(hit.point, hit.normal, FxSystem.Dust, 4, 2.5f, 0.05f);
            }
            Fired?.Invoke(cam.position, dir);

            if (_view != null)
            {
                _view.OnFire();
                FxSystem.Instance?.ShowTracer(_view.Muzzle.position, end);
            }

            _audio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            _audio.PlayOneShot(ProceduralAudio.Gunshot, 0.75f);

            float recoilScale = Mathf.Lerp(1f, _data.aimRecoilScale, AimAmount);
            _look.AddRecoil(_data.recoilPitch * recoilScale, UnityEngine.Random.Range(-_data.recoilYaw, _data.recoilYaw) * recoilScale);
            _look.AddShake(0.12f);
            _bloom = Mathf.Min(_bloom + _data.bloomPerShot, _data.maxBloom);

            if (Magazine == 0 && Reserve > 0) TryStartReload();
        }

        void TryStartReload()
        {
            if (IsReloading || Magazine >= _data.magazineSize || Reserve <= 0) return;
            IsReloading = true;
            _reloadDuration = Magazine == 0 ? _data.emptyReloadTime : _data.reloadTime;
            _reloadEndTime = Time.time + _reloadDuration;
            _magOutPlayed = false;
        }

        void UpdateReload()
        {
            if (!IsReloading) return;
            if (!_magOutPlayed && ReloadProgress > 0.15f)
            {
                _uiAudio.PlayOneShot(ProceduralAudio.MagOut, 0.7f);
                _magOutPlayed = true;
            }
            if (Time.time < _reloadEndTime) return;

            int take = Mathf.Min(_data.magazineSize - Magazine, Reserve);
            Magazine += take;
            Reserve -= take;
            IsReloading = false;
            _uiAudio.PlayOneShot(ProceduralAudio.MagIn, 0.8f);
        }

        /// <summary>Called when the host reports this player's shot hit a zombie.</summary>
        public void ConfirmHit(bool killed, bool headshot)
        {
            if (killed) _uiAudio.PlayOneShot(headshot ? ProceduralAudio.HeadshotTick : ProceduralAudio.KillTick, 0.9f);
            else _uiAudio.PlayOneShot(headshot ? ProceduralAudio.HeadshotTick : ProceduralAudio.HitTick, 0.6f);
            Hit?.Invoke(killed, headshot);
        }

        /// <summary>Back to a full magazine and starting reserve (used on restart).</summary>
        public void ResetAmmo()
        {
            Magazine = _data.magazineSize;
            Reserve = _data.startingReserve;
            IsReloading = false;
        }

        /// <summary>Wall-buy ammo refill hook for Phase 3.</summary>
        public void RefillAmmo()
        {
            Reserve = _data.maxReserve;
        }
    }
}
