using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Weapons;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Melee and throwables for one player. The owner swings or throws and tells the host; the host checks
    /// the supply, flies the authoritative grenade, knife or bang, and deals the damage. Supplies refill a
    /// little every round. A throw winds up first (<see cref="ThrowMotion"/>): teammates see the wind-up start,
    /// and the throwable only leaves the hand, aimed where the player is looking then, at the release.
    /// </summary>
    public partial class NetPlayer
    {
        public NetworkVariable<int> Grenades = new NetworkVariable<int>(0);
        public NetworkVariable<int> Knives = new NetworkVariable<int>(0);
        public NetworkVariable<int> Bangs = new NetworkVariable<int>(0);

        static EquipmentData Equipment => GameConfig.Equipment;

        // Owner.
        float _nextMelee;
        float _nextThrow;
        float _releaseAt = -1f;
        ThrowableKind _throwing;
        int _throwCounter;

        // Server.
        readonly Collider[] _blast = new Collider[96];
        readonly System.Collections.Generic.HashSet<NetZombie> _blastHit = new System.Collections.Generic.HashSet<NetZombie>();

        public int CountOf(ThrowableKind kind) => kind == ThrowableKind.Grenade ? Grenades.Value : kind == ThrowableKind.Knife ? Knives.Value : Bangs.Value;
        NetworkVariable<int> CountVariable(ThrowableKind kind) => kind == ThrowableKind.Grenade ? Grenades : kind == ThrowableKind.Knife ? Knives : Bangs;

        /// <summary>Owner, every frame: melee and throw keys.</summary>
        void OwnerEquipmentTick(bool canAct)
        {
            if (Equipment == null || _gun == null) return;
            Transform cam = _gun.View != null ? _gun.View.transform.parent : null;
            if (cam == null) return;
            if (_releaseAt >= 0f)
            {
                // Mid wind-up: nothing else until it's thrown. Going down drops it (none is spent).
                if (!IsAlive)
                {
                    _releaseAt = -1f;
                    _gun.View.CancelThrow();
                }
                else if (Time.time >= _releaseAt)
                {
                    _releaseAt = -1f;
                    Release(_throwing, cam);
                }
                return;
            }
            if (!canAct) return;
            if (GameInput.MeleePressed && Time.time >= _nextMelee)
            {
                _nextMelee = Time.time + Equipment.meleeCooldown;
                _gun.BusyUntil = Time.time + Equipment.meleeCooldown * 0.7f;
                _gun.View.Melee();
                if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.Whoosh, 0.8f);
                MeleeRpc(cam.position, cam.forward);
                return;
            }
            if (Time.time < _nextThrow) return;
            if (GameInput.GrenadePressed) TryThrow(ThrowableKind.Grenade, cam);
            else if (GameInput.KnifePressed) TryThrow(ThrowableKind.Knife, cam);
            else if (GameInput.ConcussionPressed) TryThrow(ThrowableKind.Concussion, cam);
        }

        void TryThrow(ThrowableKind kind, Transform cam)
        {
            if (CountOf(kind) <= 0)
            {
                if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.DryFire, 0.8f);
                return;
            }
            float total = ThrowMotion.Total(kind);
            _nextThrow = Time.time + Mathf.Max(Equipment.throwCooldown, total);
            _gun.BusyUntil = Time.time + total * 0.85f;
            _gun.View.Throw(kind);
            _throwing = kind;
            _releaseAt = Time.time + ThrowMotion.WindUp(kind);
            if (_uiAudio != null && kind != ThrowableKind.Knife) _uiAudio.PlayOneShot(ProceduralAudio.Pin, 0.8f);
            ThrowWindUpRpc(kind);
        }

        /// <summary>Owner: the hand lets go. This copy flies at once; the host's (and everyone else's) follows.</summary>
        void Release(ThrowableKind kind, Transform cam)
        {
            if (_uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.Whoosh, kind == ThrowableKind.Knife ? 0.8f : 0.6f);
            // Lob grenades and bangs a little upward; knives fly straight. They leave from the off hand, just left of centre.
            Vector3 dir = kind == ThrowableKind.Knife ? cam.forward : (cam.forward + Vector3.up * 0.18f).normalized;
            Vector3 origin = cam.position + cam.forward * 0.4f - cam.right * 0.05f;
            int seq = ++_throwCounter & 0xFFFFF;
            var def = Equipment.Def(kind);
            if (!IsServer && CountOf(kind) > 0)
                Throwable.Launch(ThrowId(seq), kind, OwnerClientId, origin, dir * def.throwSpeed, def.fuse, false, null);
            ThrowRpc(kind, origin, dir, seq);
        }

        /// <summary>The same id on every machine: the player's object, then the owner's count of throws.</summary>
        long ThrowId(int seq) => ((long)NetworkObjectId << 20) | (uint)(seq & 0xFFFFF);

        // ---------------------------------------------------------------- server

        /// <summary>Host only. Start-of-run supply.</summary>
        public void ServerResetEquipment()
        {
            if (Equipment == null) return;
            Grenades.Value = Equipment.grenade.startCount;
            Knives.Value = Equipment.knife.startCount;
            Bangs.Value = Equipment.concussion.startCount;
        }

        /// <summary>Host only. A new round tops up each throwable, up to its max.</summary>
        public void ServerRefillEquipment(bool full)
        {
            if (Equipment == null) return;
            foreach (ThrowableKind kind in System.Enum.GetValues(typeof(ThrowableKind)))
            {
                var def = Equipment.Def(kind);
                var v = CountVariable(kind);
                v.Value = full ? Mathf.Max(v.Value, def.maxCount) : Mathf.Min(def.maxCount, v.Value + def.perRound);
            }
        }

        long RoundHealth => RoundManager.Instance != null ? RoundManager.Instance.CurrentZombieHealth : GameConfig.Round.HealthForRound(1);

        [Rpc(SendTo.Server)]
        void MeleeRpc(Vector3 origin, Vector3 direction)
        {
            if (State.Value != LifeState.Alive || Equipment == null) return;
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            if ((origin - eye).sqrMagnitude > 2f * 2f) origin = eye;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

            float reach = Equipment.meleeRange * (HasSword.Value ? EasterEggData.SwordReach : 1f);
            int count = Physics.SphereCastNonAlloc(origin, Equipment.meleeRadius, direction, _hits, reach, ~0, QueryTriggerInteraction.Ignore);
            Hitbox best = null;
            Vector3 point = origin + direction * reach;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hb = _hits[i].collider.GetComponent<Hitbox>();
                if (hb == null || !(hb.Owner is NetZombie z) || !z.IsAlive) continue;
                if (_hits[i].distance < bestDist)
                {
                    bestDist = _hits[i].distance;
                    best = hb;
                    point = _hits[i].distance > 0f ? _hits[i].point : _hits[i].collider.bounds.center;
                }
            }
            if (best == null)
            {
                MeleeFxRpc(point, false);
                return;
            }
            var zombie = (NetZombie)best.Owner;
            long damage = (long)System.Math.Round(Equipment.MeleeDamageFor(RoundHealth) * (double)(best.isHead ? 1.5f : 1f) * (1f + Bonus(PowerStat.Damage)) * EggDamage);
            if (HasSword.Value) damage = SwordDamage(zombie, damage);
            var result = zombie.TakeDamage(new DamageInfo(damage, point, direction, best.isHead, OwnerClientId));
            if (!result.Killed) zombie.ServerStagger(Equipment.meleeStagger);
            AwardPoints(zombie, result, best.isHead);
            OnServerKillHeal(result);
            HitConfirmRpc(result.Killed, best.isHead);
            MeleeFxRpc(point, true);
        }

        /// <summary>
        /// Oathbreaker: a set number of hits kills any zombie (one up to round 55, two up to 100, three after),
        /// whatever its type. Bosses just take a heavy blade.
        /// </summary>
        long SwordDamage(NetZombie zombie, long knife)
        {
            if (zombie.Data.IsBoss) return (long)System.Math.Round(knife * (double)EasterEggData.SwordBossDamage);
            int round = RoundManager.Instance != null ? RoundManager.Instance.DisplayRound : 1;
            int hits = EasterEggData.SwordHits(round);
            long perHit = zombie.MaxHealth / hits + 1;
            return System.Math.Max(knife, perHit);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void MeleeFxRpc(Vector3 point, bool flesh)
        {
            if (!flesh) return;
            FxSystem.Instance?.Impact(point, Vector3.up, FxSystem.Blood, 8, 3f, 0.06f);
            if (IsOwner && _uiAudio != null) _uiAudio.PlayOneShot(ProceduralAudio.MeleeHit, 0.9f);
        }

        [Rpc(SendTo.Server)]
        void ThrowWindUpRpc(ThrowableKind kind)
        {
            if (State.Value == LifeState.Alive && CountOf(kind) > 0) ThrowWindUpFxRpc(kind);
        }

        /// <summary>Teammates see the arm go back.</summary>
        [Rpc(SendTo.NotOwner)]
        void ThrowWindUpFxRpc(ThrowableKind kind)
        {
            _rThrowAt = Time.time;
            _rThrowKind = kind;
        }

        [Rpc(SendTo.Server)]
        void ThrowRpc(ThrowableKind kind, Vector3 origin, Vector3 direction, int seq)
        {
            var rounds = RoundManager.Instance;
            if (State.Value != LifeState.Alive || Equipment == null || (rounds != null && rounds.IsGameOver)) return;
            var count = CountVariable(kind);
            if (count.Value <= 0) return;
            count.Value--;
            Vector3 eye = transform.position + Vector3.up * EyeHeight;
            if ((origin - eye).sqrMagnitude > 2f * 2f) origin = eye;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            var def = Equipment.Def(kind);
            ThrowSpawnRpc(ThrowId(seq), kind, origin, direction * def.throwSpeed, def.fuse);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ThrowSpawnRpc(long id, ThrowableKind kind, Vector3 origin, Vector3 velocity, float fuse)
        {
            if (!IsOwner && Time.time - _rThrowAt > ThrowMotion.Total(kind))
            {
                // The wind-up never showed (joined mid-throw): play the throw from its release.
                _rThrowAt = Time.time - ThrowMotion.WindUp(kind);
                _rThrowKind = kind;
            }
            Throwable.Launch(id, kind, OwnerClientId, origin, velocity, fuse, IsServer, IsServer ? ServerDetonate : (System.Action<Throwable, Vector3, Hitbox>)null);
        }

        /// <summary>Host: the authoritative throwable went off (or a knife hit something).</summary>
        void ServerDetonate(Throwable t, Vector3 point, Hitbox hitbox)
        {
            if (this == null || !IsSpawned || Equipment == null) return;
            var def = Equipment.Def(t.Kind);
            long centre = (long)System.Math.Round(def.DamageFor(RoundHealth) * (double)EggDamage);
            bool killedAny = false;
            if (t.Kind == ThrowableKind.Knife)
            {
                if (hitbox != null && hitbox.Owner is NetZombie zombie && zombie.IsAlive)
                {
                    long damage = (long)System.Math.Round(centre * (double)(hitbox.isHead ? 2f : hitbox.multiplier));
                    var result = zombie.TakeDamage(new DamageInfo(damage, point, (point - transform.position).normalized, hitbox.isHead, OwnerClientId));
                    AwardPoints(zombie, result, hitbox.isHead);
                    OnServerKillHeal(result);
                    HitConfirmRpc(result.Killed, hitbox.isHead);
                    killedAny = result.Killed;
                }
                DetonateRpc(t.Id, t.Kind, point, hitbox != null);
                return;
            }

            _blastHit.Clear();
            int n = Physics.OverlapSphereNonAlloc(point, def.radius, _blast, ~0, QueryTriggerInteraction.Ignore);
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                var hb = _blast[i].GetComponent<Hitbox>();
                if (hb == null || !(hb.Owner is NetZombie zombie) || !zombie.IsAlive || !_blastHit.Add(zombie)) continue;
                float dist = Vector3.Distance(point, zombie.transform.position);
                long damage = EquipmentData.Falloff(centre, dist, def.radius, def.edgeDamage);
                Vector3 dir = zombie.transform.position - point;
                dir.y = 0.3f;
                var result = zombie.TakeDamage(new DamageInfo(damage, zombie.transform.position + Vector3.up, dir.normalized, false, OwnerClientId));
                if (!result.Killed && def.stunTime > 0f) zombie.ServerStun(def.stunTime, def.slowTime, def.slowScale);
                AwardPoints(zombie, result, false);
                OnServerKillHeal(result);
                hits++;
                killedAny |= result.Killed;
            }
            if (hits > 0) HitConfirmRpc(killedAny, false);
            DetonateRpc(t.Id, t.Kind, point, hits > 0);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void DetonateRpc(long id, ThrowableKind kind, Vector3 point, bool hitSomething)
        {
            Throwable.Detonated(id, point);
            var fx = FxSystem.Instance;
            var local = Local;
            float dist = local != null ? Vector3.Distance(local.transform.position, point) : 100f;
            switch (kind)
            {
                case ThrowableKind.Grenade:
                    fx?.Impact(point, Vector3.up, FxSystem.Blast, 34, 9f, 0.12f);
                    fx?.Impact(point, Vector3.up, FxSystem.Dust, 18, 5f, 0.1f);
                    fx?.Flash(point, new Color(1f, 0.6f, 0.25f), 9f, 0.25f);
                    fx?.PlayAt(point, ProceduralAudio.Explosion, 1f);
                    if (local != null && local.Look != null) local.Look.AddShake(Mathf.Clamp01(1.2f - dist / 18f));
                    break;
                case ThrowableKind.Concussion:
                    fx?.Impact(point, Vector3.up, Color.white, 20, 7f, 0.06f);
                    fx?.Flash(point, new Color(0.75f, 0.9f, 1f), 12f, 0.35f);
                    fx?.PlayAt(point, ProceduralAudio.Concussion, 1f);
                    if (local != null && local.Look != null) local.Look.AddShake(Mathf.Clamp01(0.6f - dist / 30f));
                    break;
                default:
                    if (hitSomething) fx?.Impact(point, Vector3.up, FxSystem.Blood, 8, 3f, 0.05f);
                    else fx?.Impact(point, Vector3.up, FxSystem.Dust, 4, 2f, 0.04f);
                    fx?.PlayAt(point, ProceduralAudio.KnifeHit, 0.8f);
                    break;
            }
        }
    }
}
