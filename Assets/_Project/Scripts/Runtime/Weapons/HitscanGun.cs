using System;
using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;
using ZombiesFPS.Player;
using ZombiesFPS.Zombies;
using ZombiesFPS.World;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// Hitscan gun driven by GunData: fire rate, magazine + reserve, reload, aim down sights,
    /// spread/bloom and recoil. The local raycast only drives effects; the shot itself goes to
    /// the host through <see cref="Fired"/>, and the host's verdict comes back via <see cref="ConfirmHit"/>.
    /// </summary>
    public class HitscanGun : MonoBehaviour
    {
        /// <summary>Hip-fire field of view, from Settings. Aim zoom keeps the same ratio as at 75 degrees.</summary>
        public float BaseFov => GameSettings.Fov;
        /// <summary>Melee and throws keep the gun from firing until this time.</summary>
        public float BusyUntil { get; set; }
        /// <summary>Perk multipliers: fire rate (Hair Trigger) and spread and aim speed (Steady Hand).</summary>
        public float FireRateScale { get; set; } = 1f;
        public float SpreadScale { get; set; } = 1f;
        public WeaponView View => _view;

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
        float _raiseEndTime;
        float _raiseDuration = 0.01f;

        public GunData Data => _data;
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? 1f - Mathf.Clamp01((_reloadEndTime - Time.time) / _reloadDuration) : 0f;
        public float AimAmount { get; private set; }
        public float CurrentSpread { get; private set; }
        public float LastShotTime { get; private set; } = -100f;
        /// <summary>Name shown on the HUD, including any upgrade machine tier.</summary>
        public string DisplayName { get; private set; }
        /// <summary>Magazine and reserve multiplier (the ammo power-up and the machine tier).</summary>
        public float CapacityScale { get; private set; } = 1f;
        /// <summary>Reload speed multiplier (the reload power-up): 2 reloads in half the time.</summary>
        public float ReloadSpeed { get; set; } = 1f;
        /// <summary>Upgraded guns with explosive rounds show a burst where they hit.</summary>
        public bool ExplosiveRounds { get; set; }
        /// <summary>The upgrade machine tier of the gun in hand (0 = not upgraded): its sound and glow. Set before Equip.</summary>
        public int UpgradeTier { get; set; }
        /// <summary>How many zombies a bullet goes through after the first (for the local effects; the host decides hits).</summary>
        public int Pierce { get; set; }
        /// <summary>The camo colour of the gun in hand, or null for its own colours. Set before Equip.</summary>
        public Color? Camo { get; set; }
        public int MagazineCapacity => ScaledMagazine(_data, CapacityScale);
        public int MaxReserve => ScaledReserve(_data.maxReserve, CapacityScale);
        public int StartingReserve => ScaledReserve(_data.startingReserve, CapacityScale);

        public static int ScaledMagazine(GunData data, float scale) => Mathf.Max(1, Mathf.RoundToInt(data.magazineSize * scale));
        public static int ScaledReserve(int reserve, float scale) => Mathf.Max(0, Mathf.RoundToInt(reserve * scale));

        /// <summary>0 just switched to, 1 fully raised and ready.</summary>
        public float RaiseProgress => 1f - Mathf.Clamp01((_raiseEndTime - Time.time) / _raiseDuration);
        public bool IsRaising => Time.time < _raiseEndTime;

        /// <summary>Raised when a different gun is put in the player's hands.</summary>
        public event Action Equipped;

        /// <summary>Raised when the host confirms a hit: (killed, headshot).</summary>
        public event Action<bool, bool> Hit;

        /// <summary>Raised on every shot with (origin, direction) so the host can resolve it.</summary>
        public event Action<Vector3, Vector3, bool> Fired;

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
            DisplayName = data.displayName;
            Magazine = MagazineCapacity;
            Reserve = StartingReserve;
            _camera.fieldOfView = BaseFov;
        }

        void Update()
        {
            if (_data == null) return;
            float dt = Time.deltaTime;
            bool canAct = _canShoot() && _look.CursorLocked && !_look.LockedThisFrame && Time.time >= BusyUntil;

            UpdateReload();

            bool wantsAim = canAct && GameInput.AimHeld && !IsReloading && !IsRaising && !_motor.IsSprinting && !_motor.IsMantling;
            AimAmount = Mathf.MoveTowards(AimAmount, wantsAim ? 1f : 0f, dt / (_data.aimTime * Mathf.Lerp(1f, SpreadScale, 0.5f)));
            _look.AimAmount = AimAmount;
            float eased = AimAmount * AimAmount * (3f - 2f * AimAmount);
            _camera.fieldOfView = Mathf.Lerp(BaseFov, _data.aimFov * BaseFov / 75f, eased);

            _bloom = Mathf.MoveTowards(_bloom, 0f, dt * 4f);
            float moveFactor = Mathf.Clamp01(_motor.PlanarSpeed / _motor.walkSpeed);
            float airPenalty = _motor.IsGrounded ? 0f : 2f;
            CurrentSpread = Mathf.Lerp(_data.hipSpread, _data.aimSpread, eased)
                            + (moveFactor * _data.movingSpread + airPenalty) * (1f - eased * 0.75f)
                            + _bloom;
            CurrentSpread *= SpreadScale;

            if (!canAct) return;

            if (GameInput.ReloadPressed) TryStartReload();

            bool trigger = _data.automatic ? GameInput.FireHeld : GameInput.FirePressed;
            if (!GameInput.FireHeld) _dryFirePlayed = false;
            if (trigger && !IsReloading && !_motor.IsSprinting && !_motor.IsMantling && Time.time >= _nextFireTime)
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
            _nextFireTime = Time.time + _data.SecondsPerShot / Mathf.Max(0.1f, FireRateScale);
            LastShotTime = Time.time;

            Transform cam = _camera.transform;
            int pellets = Mathf.Max(1, _data.pellets);
            if (_view != null) _view.OnFire();
            for (int p = 0; p < pellets; p++)
            {
                float spread = CurrentSpread + (pellets > 1 ? _data.pelletSpread * SpreadScale : 0f);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * spread;
                Vector3 dir = Quaternion.AngleAxis(offset.x, cam.up) * Quaternion.AngleAxis(offset.y, cam.right) * cam.forward;

                // Local raycast is for effects only; damage is decided by the host.
                Vector3 end = TraceEffects(cam.position, dir, pellets, p == 0, out int passed);
                // The first pellet carries the machine tier's chain and explosive effects.
                Fired?.Invoke(cam.position, dir, p == 0);
                if (_view != null) FxSystem.Instance?.ShowTracer(_view.Muzzle.position, end, UpgradeData.TierGlow(UpgradeTier), passed > 1);
            }

            _audio.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            _audio.PlayOneShot(ProceduralAudio.GunshotFor(UpgradeTier), 0.75f);

            float recoilScale = Mathf.Lerp(1f, _data.aimRecoilScale, AimAmount);
            _look.AddRecoil(_data.recoilPitch * recoilScale, UnityEngine.Random.Range(-_data.recoilYaw, _data.recoilYaw) * recoilScale);
            _look.AddShake(0.12f);
            _bloom = Mathf.Min(_bloom + _data.bloomPerShot, _data.maxBloom);

            if (Magazine == 0 && Reserve > 0) TryStartReload();
        }

        readonly RaycastHit[] _traceHits = new RaycastHit[32];
        readonly HashSet<IDamageable> _traced = new HashSet<IDamageable>();

        /// <summary>
        /// Blood on every zombie the bullet goes through (as many as its pierce allows) and dust where it stops,
        /// the same way the host works out the hits. Returns where the tracer ends and how many zombies it passed.
        /// </summary>
        Vector3 TraceEffects(Vector3 origin, Vector3 dir, int pellets, bool first, out int passed)
        {
            passed = 0;
            Vector3 end = origin + dir * _data.range;
            int count = Physics.RaycastNonAlloc(origin, dir, _traceHits, _data.range, ZoneMap.ShotMask, QueryTriggerInteraction.Ignore);
            Array.Sort(_traceHits, 0, count, NearestFirst.Instance);
            _traced.Clear();
            int pierceLeft = Pierce;
            // Explosive rounds go off at the first zombie hit, or where the bullet stops (matches the host).
            bool blastDone = !(ExplosiveRounds && first);
            for (int i = 0; i < count; i++)
            {
                var hit = _traceHits[i];
                if (hit.collider.GetComponentInParent<NetPlayer>() != null) continue;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox.Owner is NetZombie z)
                {
                    if (!z.IsAlive || !_traced.Add(z)) continue;
                    passed++;
                    FxSystem.Instance?.Impact(hit.point, hit.normal, FxSystem.Blood, hitbox.isHead ? 9 : 5, 3.5f, 0.07f);
                    if (!blastDone) { blastDone = true; FxSystem.Instance?.Impact(hit.point, hit.normal, FxSystem.Blast, 16, 6f, 0.09f); }
                    pierceLeft -= z.PenetrationCost;
                    if (pierceLeft >= 0) continue;
                    return hit.point;
                }
                end = hit.point;
                FxSystem.Instance?.Impact(hit.point, hit.normal, FxSystem.Dust, pellets > 1 ? 2 : 4, 2.5f, 0.05f);
                if (!blastDone) FxSystem.Instance?.Impact(hit.point, hit.normal, FxSystem.Blast, 16, 6f, 0.09f);
                return end;
            }
            if (!blastDone) FxSystem.Instance?.Impact(end, -dir, FxSystem.Blast, 16, 6f, 0.09f);
            return end;
        }

        sealed class NearestFirst : IComparer<RaycastHit>
        {
            public static readonly NearestFirst Instance = new NearestFirst();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        void TryStartReload()
        {
            if (IsReloading || IsRaising || Magazine >= MagazineCapacity || Reserve <= 0) return;
            IsReloading = true;
            _reloadDuration = (Magazine == 0 ? _data.emptyReloadTime : _data.reloadTime) / Mathf.Max(0.1f, ReloadSpeed);
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

            int take = Mathf.Min(MagazineCapacity - Magazine, Reserve);
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
            Magazine = MagazineCapacity;
            Reserve = StartingReserve;
            IsReloading = false;
        }

        /// <summary>Puts a gun in the player's hands with the given ammo, then raises it before it can fire.</summary>
        public void Equip(GunData data, int magazine, int reserve, float capacityScale = 1f, string displayName = null)
        {
            _data = data;
            CapacityScale = Mathf.Max(0.1f, capacityScale);
            DisplayName = string.IsNullOrEmpty(displayName) ? data.displayName : displayName;
            Magazine = Mathf.Clamp(magazine, 0, MagazineCapacity);
            Reserve = Mathf.Max(0, reserve);
            IsReloading = false;
            _bloom = 0f;
            _raiseDuration = Mathf.Max(0.05f, data.raiseTime);
            _raiseEndTime = Time.time + _raiseDuration;
            _nextFireTime = Mathf.Max(_nextFireTime, _raiseEndTime);
            Equipped?.Invoke();
        }

        /// <summary>Tops the reserve up to the maximum (god mode).</summary>
        public void RefillAmmo()
        {
            Reserve = MaxReserve;
        }

        /// <summary>Full magazine and full reserve (bought ammo off the wall).</summary>
        public void FillAmmo()
        {
            Magazine = MagazineCapacity;
            Reserve = MaxReserve;
            IsReloading = false;
        }

        /// <summary>Changes capacity without touching the rounds already loaded (a new ammo power-up level).</summary>
        public void SetCapacityScale(float scale)
        {
            CapacityScale = Mathf.Max(0.1f, scale);
            Magazine = Mathf.Min(Magazine, MagazineCapacity);
        }
    }
}
