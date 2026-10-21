using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Sealed rooms: while a step is being played its room's doorways are fields nobody can cross, either way, and
    /// if everyone inside goes down the step is called off (the downed get back up) and has to be started again.
    /// The same goes for the Sunken Temple's guardian, Ssethra, who rises when both of the Temple's relics are
    /// taken and has to be killed before anyone leaves.
    /// </summary>
    public partial class EasterEgg
    {
        /// <summary>The Temple's guardian: 0 asleep, 1 risen (the Temple sealed), 2 slain.</summary>
        public NetworkVariable<byte> Guardian = new NetworkVariable<byte>(0);

        public const byte GuardianAsleep = 0, GuardianRisen = 1, GuardianSlain = 2;
        const float WipeGrace = 4f;

        // One set of fields per room a step (or the guardian) can seal.
        readonly Dictionary<int, Lockdown> _seals = new Dictionary<int, Lockdown>();

        // Server.
        NetZombie _guardian;
        bool _guardianDue;
        float _guardianNextAdd, _guardianNextMove, _wipeCheckFrom;

        void BuildSeals(ZoneMap map)
        {
            _seals[EasterEggData.LanternZone] = _lockdown;
            foreach (var step in EasterEggData.Steps)
                if (step != null && step.zone >= 0 && !_seals.ContainsKey(step.zone))
                    _seals[step.zone] = Lockdown.Build(map.Root, step.zone);
            if (!_seals.ContainsKey(EasterEggData.TempleZone))
                _seals[EasterEggData.TempleZone] = Lockdown.Build(map.Root, EasterEggData.TempleZone);
        }

        void DestroySeals()
        {
            foreach (var seal in _seals.Values)
                if (seal != null && seal != _lockdown) Destroy(seal.gameObject);
            _seals.Clear();
        }

        /// <summary>The room sealed right now (every machine), or -1. The final step is off the map and seals nothing.</summary>
        public int SealedZone
        {
            get
            {
                if (Guardian.Value == GuardianRisen) return EasterEggData.TempleZone;
                if (!Active.Value) return -1;
                var step = CurrentStep;
                return step == null || step.kind == EggStepKind.Summon ? -1 : step.zone;
            }
        }

        void UpdateSeals()
        {
            int zone = SealedZone;
            foreach (var pair in _seals)
                if (pair.Value != null) pair.Value.Sealed = pair.Key == zone;
        }

        /// <summary>Whether the whole squad (everyone not bled out) is in a room, with the reason when not.</summary>
        static bool SquadIn(int zone, string toDo, out string what)
        {
            int inside = PlayersInZone(zone, out int needed);
            what = inside < needed ? $"Everyone must be in the {ZoneMap.ZoneNames[zone]} to {toDo} ({inside}/{needed} here)" : null;
            return inside >= needed;
        }

        // ---------------------------------------------------------------- the Temple's guardian (server)

        /// <summary>Host: a relic was picked up. Taking the Temple's second one wakes its guardian.</summary>
        void CheckGuardian(int relic)
        {
            if (Guardian.Value != GuardianAsleep || EasterEggData.RelicZone(relic) != EasterEggData.TempleZone) return;
            for (int i = 0; i < EasterEggData.RelicCount; i++)
                if (EasterEggData.RelicZone(i) == EasterEggData.TempleZone && !RelicTaken(i)) return;
            CollectSpawnSpots(EasterEggData.TempleZone);
            _spawnSpots.RemoveAll(p => (p - ZoneMap.GuardianRise).sqrMagnitude < 6f * 6f);
            Guardian.Value = GuardianRisen;
            _guardianDue = true;
            // It rises once Doctor Morrow has seen it coming.
            _guardianNextAdd = Time.time + 3.5f;
            _guardianNextMove = _guardianNextAdd + EasterEggData.BossMoveEvery(EggBoss.Serpent);
            _wipeCheckFrom = Time.time + WipeGrace;
        }

        void GuardianTick(int players)
        {
            if (_guardian != null && !_guardian.IsSpawned) _guardian = null;
            if (Time.time < _guardianNextAdd) return;
            if (_guardianDue)
            {
                if (ZombiePool.Instance == null || !NavMesh.SamplePosition(ZoneMap.GuardianRise, out var hit, 3f, NavMesh.AllAreas))
                {
                    _guardianNextAdd = Time.time + 0.5f;
                    return;
                }
                _guardian = Spawn(hit.position, ZombieKind.Boss, EasterEggData.BossHealth(EggBoss.Serpent), ZombieSpeedTier.Jog, EggBoss.Serpent);
                BurstRpc(hit.position, (byte)EggBoss.Serpent);
                _guardianDue = false;
                _guardianNextAdd = Time.time + EasterEggData.GuardianAddsInterval;
                return;
            }
            // The drowned monks climb out of the pools while it lives.
            if (_zombies.Count < EasterEggData.GuardianAddsCap + 2 * Mathf.Max(0, players - 1))
            {
                var kind = Random.value < 0.4f ? ZombieKind.Runner : ZombieKind.Walker;
                _guardianNextAdd = Time.time + (TrySpawn(kind, AddsHealthScale, ZombieSpeedTier.Jog) ? EasterEggData.GuardianAddsInterval : 0.25f);
            }
            // Its move: a hiss that pulls three more out of the water round it.
            if (_guardian != null && _guardian.IsAlive && Time.time >= _guardianNextMove)
            {
                _guardianNextMove = Time.time + EasterEggData.BossMoveEvery(EggBoss.Serpent);
                var at = _guardian.transform.position;
                for (int i = 0; i < 3; i++) SpawnNear(at, ZombieKind.Runner, AddsHealthScale);
                BurstRpc(at, (byte)EggBoss.Serpent);
            }
        }

        /// <summary>Host: the guardian died (true when that zombie was it).</summary>
        bool OnGuardianDied(NetZombie zombie)
        {
            if (zombie == null || zombie != _guardian) return false;
            _guardian = null;
            foreach (var z in _zombies)
                if (z != null && z.IsSpawned) BurstRpc(z.transform.position, 0);
            ClearAdds();
            foreach (var p in NetPlayer.All) p.Points.Value += EasterEggData.GuardianReward;
            Guardian.Value = GuardianSlain;
            return true;
        }

        bool OnGuardianRecycled(NetZombie zombie)
        {
            if (zombie == null || zombie != _guardian) return false;
            _guardian = null;
            _guardianDue = true;
            return true;
        }

        void ClearAdds()
        {
            foreach (var z in _zombies.ToArray())
                if (z != null && z.IsSpawned) z.ServerRemove();
            _zombies.Clear();
        }

        /// <summary>Host: the guardian and its adds gone, without a winner (a wipe, a reset or the test console).</summary>
        void RemoveGuardian()
        {
            if (_guardian != null && _guardian.IsSpawned) _guardian.ServerRemove();
            _guardian = null;
            _guardianDue = false;
        }

        // ---------------------------------------------------------------- wipes (server)

        /// <summary>
        /// Host, every frame and just before the round would end the game: when everyone in the sealed room (or down in
        /// the Sanctum or the Keep) is down or bled out, the step is called off and the downed get back up. True when
        /// that happened.
        /// </summary>
        public bool ServerCheckWipe()
        {
            if (!IsServer || Time.time < _wipeCheckFrom) return false;
            var step = CurrentStep;
            bool arena = Active.Value && step != null && step.kind == EggStepKind.Summon && Phase.Value != EggPhase.None;
            int zone = SealedZone;
            if (zone < 0 && !arena) return false;
            int inside = 0, standing = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.State.Value == LifeState.Dead) continue;
                var at = p.transform.position;
                bool there = arena ? ZoneMap.InArena(at) : ZoneMap.ZoneOfPoint(at) == zone && at.y > -1f && at.y < ZoneMap.WallHeight;
                if (!there) continue;
                inside++;
                if (p.IsAlive || p.SelfReviving) standing++;
            }
            if (standing > 0) return false;
            // Nobody left inside at all means they all bled out there.
            Wipe(zone, arena);
            return true;
        }

        void Wipe(int zone, bool arena)
        {
            bool guardian = Guardian.Value == GuardianRisen && !Active.Value;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.State.Value != LifeState.Downed) continue;
                var at = p.transform.position;
                if (arena ? ZoneMap.InArena(at) : ZoneMap.ZoneOfPoint(at) == zone) p.ServerRaise();
            }
            if (guardian)
            {
                // The relics slide back into the water; picking them up again wakes it again.
                RemoveGuardian();
                ClearAdds();
                byte relics = Relics.Value;
                for (int i = 0; i < EasterEggData.RelicCount; i++)
                    if (EasterEggData.RelicZone(i) == EasterEggData.TempleZone) relics = (byte)(relics & ~(1 << i));
                Relics.Value = relics;
                Guardian.Value = GuardianAsleep;
                WipedRpc(true);
                return;
            }
            var step = CurrentStep;
            ClearZombies();
            StopWatchmanRun();
            if (arena)
            {
                int i = 0;
                foreach (var p in NetPlayer.All)
                    if (p != null && p.State.Value != LifeState.Dead && ZoneMap.InArena(p.transform.position)) p.ServerTeleport(ZoneMap.ReturnSpot(i++));
            }
            Kills.Value = 0;
            Total.Value = 0;
            Paused.Value = false;
            WatchmanHealth.Value = 1f;
            Phase.Value = EggPhase.None;
            Active.Value = false;
            WipedRpc(false);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void WipedRpc(bool guardian)
        {
            LineSaid?.Invoke(guardian ? EasterEggData.GuardianWon : EasterEggData.StepWiped);
            if (guardian) Moment?.Invoke("THE GUARDIAN PREVAILS", "The Temple's relics are back in the water. Take them again to face it.", EasterEggData.BossGlow(EggBoss.Serpent));
            else Moment?.Invoke("THE STEP IS LOST", "Everyone in the room went down. Start it again when you're ready.", new Color(1f, 0.45f, 0.35f));
        }

        // ---------------------------------------------------------------- every machine

        void OnGuardianChanged(byte was, byte now)
        {
            ShowState();
            if (now == GuardianRisen && was == GuardianAsleep)
            {
                Say(EasterEggData.GuardianWakes);
                Moment?.Invoke("THE TEMPLE IS SEALED", $"{EasterEggData.BossName(EggBoss.Serpent)} has risen. Kill it to leave.", EasterEggData.BossGlow(EggBoss.Serpent));
            }
            else if (now == GuardianSlain && was == GuardianRisen)
            {
                LineSaid?.Invoke(EasterEggData.GuardianSlain);
                Moment?.Invoke("GUARDIAN SLAIN", $"The Temple's doors are open. +{EasterEggData.GuardianReward} points each.", EasterEggData.BossGlow(EggBoss.Serpent));
            }
        }
    }
}
