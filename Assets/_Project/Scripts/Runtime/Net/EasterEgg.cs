using System;
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
    /// Host-owned easter egg (the steps are in EasterEggData). Step one: with the whole squad inside the Lantern
    /// Core, anyone can touch the reactor. Doctor Morrow wakes up and talks, the doors seal with energy fields,
    /// lightning arcs off the reactor and fifty runners spawn inside the room, on top of the round. They don't
    /// count toward it: the round carries on and can end while the trial is still going. When the fifty are dead
    /// the fields drop, the reactor stays lit, and everyone is paid.
    ///
    /// Every machine follows the network variables: Doctor Morrow's lines, the fields, the reactor's look and the
    /// music all come from Active, Kills and StepsDone changing, so nothing else needs sending.
    /// </summary>
    public class EasterEgg : NetworkBehaviour
    {
        public static EasterEgg Instance { get; private set; }

        public const long StepReward = 2500;
        const float SpawnMinDistance = 7f;
        const float SpawnFallbackDistance = 4f;
        const float FirstSpawnDelay = 9f;

        /// <summary>Every machine: a step began (step).</summary>
        public static event Action<int> StepStarted;
        /// <summary>Every machine: a step was finished (step).</summary>
        public static event Action<int> StepCompleted;
        /// <summary>Every machine: Doctor Morrow says a line.</summary>
        public static event Action<EggLine> LineSaid;

        /// <summary>Steps finished. The step being played, or next up, has this index.</summary>
        public NetworkVariable<int> StepsDone = new NetworkVariable<int>(0);
        /// <summary>A step is running (for survival: the room is sealed).</summary>
        public NetworkVariable<bool> Active = new NetworkVariable<bool>(false);
        public NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public NetworkVariable<int> Total = new NetworkVariable<int>(0);

        Lockdown _lockdown;
        ReactorFx _fx;

        // Server.
        readonly List<NetZombie> _zombies = new List<NetZombie>();
        readonly List<Vector3> _spawnSpots = new List<Vector3>();
        int _toSpawn;
        float _nextSpawn;

        public EggStep CurrentStep => EasterEggData.At(StepsDone.Value);
        public bool Finished => StepsDone.Value >= EasterEggData.PlayableSteps;
        public int Remaining => Mathf.Max(0, Total.Value - Kills.Value);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            var map = GameConfig.Map;
            if (map != null)
            {
                _lockdown = Lockdown.Build(map.Root, EasterEggData.LanternZone);
                _fx = ReactorFx.Build(map.Root, ZoneMap.ReactorBase, map.CoilTops);
            }
            Active.OnValueChanged += OnActiveChanged;
            Kills.OnValueChanged += OnKillsChanged;
            StepsDone.OnValueChanged += OnStepsDoneChanged;
            ShowState();
        }

        public override void OnNetworkDespawn()
        {
            Active.OnValueChanged -= OnActiveChanged;
            Kills.OnValueChanged -= OnKillsChanged;
            StepsDone.OnValueChanged -= OnStepsDoneChanged;
            if (_lockdown != null) Destroy(_lockdown.gameObject);
            if (_fx != null) Destroy(_fx.gameObject);
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- queries (every machine)

        /// <summary>
        /// Players in a zone, out of everyone who still has to be there (players bled out and waiting for the round
        /// to end don't count). Downed players count where they lie.
        /// </summary>
        public static int PlayersInZone(int zone, out int needed)
        {
            needed = 0;
            int inside = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.State.Value == LifeState.Dead) continue;
                needed++;
                var at = p.transform.position;
                if (ZoneMap.ZoneOfPoint(at) == zone && at.y > -1f && at.y < ZoneMap.WallHeight) inside++;
            }
            return inside;
        }

        /// <summary>
        /// What touching the reactor would do for a player, for the prompt and the host's check: 0 when it starts the
        /// next step, -1 (with the reason in what) when it can't.
        /// </summary>
        public long ReactorPrice(NetPlayer player, out string what)
        {
            what = null;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.IsGameOver) return -1;
            if (Active.Value)
            {
                what = $"The Lantern is waking. {Remaining} left";
                return -1;
            }
            var step = CurrentStep;
            if (Finished || step == null)
            {
                what = "The Lantern hums, waiting for Doctor Morrow's next step";
                return -1;
            }
            if (step.kind == EggStepKind.Survival)
            {
                int inside = PlayersInZone(step.zone, out int needed);
                if (inside < needed)
                {
                    what = $"Everyone must be in the {ZoneMap.ZoneNames[step.zone]} to wake the reactor ({inside}/{needed} here)";
                    return -1;
                }
                what = "touch the reactor";
                return 0;
            }
            return -1;
        }

        // ---------------------------------------------------------------- server

        /// <summary>Host: a player touched the reactor. Starts the next step if everything is in place.</summary>
        public void ServerTouchReactor(NetPlayer player)
        {
            if (!IsServer || ReactorPrice(player, out _) < 0) return;
            var step = CurrentStep;
            switch (step.kind)
            {
                case EggStepKind.Survival:
                    StartSurvival(step);
                    break;
            }
        }

        void StartSurvival(EggStep step)
        {
            CollectSpawnSpots(step.zone);
            Kills.Value = 0;
            Total.Value = step.zombies;
            _toSpawn = step.zombies;
            // Doctor Morrow has his say before the first of them climbs out of the floor.
            _nextSpawn = Time.time + FirstSpawnDelay;
            Active.Value = true;
        }

        void Update()
        {
            if (!IsServer || !Active.Value) return;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.IsGameOver) return;
            _zombies.RemoveAll(z => z == null || !z.IsSpawned);
            if (_toSpawn > 0 && Time.time >= _nextSpawn && _zombies.Count < EasterEggData.AliveCap(NetPlayer.All.Count))
                _nextSpawn = Time.time + (TrySpawn() ? EasterEggData.SpawnInterval : 0.25f);
        }

        /// <summary>Open floor in the room, on a grid, for the trial's zombies to rise from.</summary>
        void CollectSpawnSpots(int zone)
        {
            _spawnSpots.Clear();
            foreach (var r in ZoneMap.ZoneRects(zone))
                for (float x = r.xMin + 1.5f; x < r.xMax - 1f; x += 2.5f)
                    for (float z = r.yMin + 1.5f; z < r.yMax - 1f; z += 2.5f)
                    {
                        var p = new Vector3(x, 0.2f, z);
                        if (!NavMesh.SamplePosition(p, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                        // Floor level only: not the catwalk, not on top of the consoles.
                        if (hit.position.y > 0.4f || ZoneMap.ZoneOfPoint(hit.position) != zone) continue;
                        _spawnSpots.Add(hit.position);
                    }
        }

        bool TrySpawn()
        {
            if (_spawnSpots.Count == 0 || ZombiePool.Instance == null) return false;
            var at = PickSpot(SpawnMinDistance) ?? PickSpot(SpawnFallbackDistance);
            if (!at.HasValue) return false;
            var step = CurrentStep;
            var rounds = RoundManager.Instance;
            int round = rounds != null ? rounds.DisplayRound : 1;
            long health = Math.Max(1L, (long)Math.Round(GameConfig.Round.HealthForRound(round) * (double)EasterEggData.HealthScale));
            var tier = GameConfig.Round.SpeedTierForRound(round);
            // Running zombies, whatever the round.
            if (tier < ZombieSpeedTier.Jog) tier = ZombieSpeedTier.Jog;

            var face = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            var zombie = ZombiePool.Instance.Get(at.Value, face);
            zombie.ServerSetup(step != null ? step.zombieKind : ZombieKind.Runner, health, tier);
            zombie.Died += OnZombieDied;
            zombie.Recycled += OnZombieRecycled;
            zombie.NetworkObject.Spawn(true);
            _zombies.Add(zombie);
            _toSpawn--;
            StrikeRpc(at.Value);
            return true;
        }

        Vector3? PickSpot(float minDistance)
        {
            int start = UnityEngine.Random.Range(0, _spawnSpots.Count);
            for (int i = 0; i < _spawnSpots.Count; i++)
            {
                var p = _spawnSpots[(start + i) % _spawnSpots.Count];
                bool clear = true;
                foreach (var player in NetPlayer.All)
                    if ((player.transform.position - p).sqrMagnitude < minDistance * minDistance)
                    {
                        clear = false;
                        break;
                    }
                if (clear) return p;
            }
            return null;
        }

        void OnZombieDied(NetZombie zombie, DamageInfo info)
        {
            _zombies.Remove(zombie);
            if (!Active.Value) return;
            Kills.Value++;
            if (Kills.Value >= Total.Value) CompleteStep();
        }

        /// <summary>A stuck one comes back somewhere else in the room.</summary>
        void OnZombieRecycled(NetZombie zombie)
        {
            _zombies.Remove(zombie);
            if (Active.Value) _toSpawn++;
        }

        void CompleteStep()
        {
            foreach (var p in NetPlayer.All) p.Points.Value += StepReward;
            _toSpawn = 0;
            Active.Value = false;
            StepsDone.Value++;
        }

        /// <summary>Host: a restart puts the egg back to the start.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            foreach (var z in _zombies.ToArray())
                if (z != null) z.ServerRemove();
            _zombies.Clear();
            _toSpawn = 0;
            Active.Value = false;
            Kills.Value = 0;
            Total.Value = 0;
            StepsDone.Value = 0;
        }

        /// <summary>Host only (test console): finishes the step being played, or skips ahead one step.</summary>
        public void DebugCompleteStep()
        {
            if (!IsServer || Finished) return;
            if (Active.Value)
            {
                foreach (var z in _zombies.ToArray())
                    if (z != null) z.ServerRemove();
                _zombies.Clear();
                Kills.Value = Total.Value;
            }
            CompleteStep();
        }

        /// <summary>Every machine: lightning from the reactor to where a zombie climbs out of the floor.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void StrikeRpc(Vector3 at)
        {
            if (_fx != null) _fx.Strike(at);
        }

        // ---------------------------------------------------------------- every machine

        void OnActiveChanged(bool was, bool now)
        {
            ShowState();
            var step = CurrentStep;
            if (now && !was && step != null)
            {
                Say(step.onStart);
                StepStarted?.Invoke(StepsDone.Value);
            }
        }

        void OnKillsChanged(int was, int now)
        {
            var step = CurrentStep;
            if (!Active.Value || step == null || step.along == null) return;
            foreach (var (kills, line) in step.along)
                if (was < kills && now >= kills) LineSaid?.Invoke(line);
        }

        void OnStepsDoneChanged(int was, int now)
        {
            ShowState();
            // A restart sets it back to 0: nothing to say.
            if (now != was + 1) return;
            var step = EasterEggData.At(was);
            if (step != null) Say(step.onComplete);
            StepCompleted?.Invoke(was);
        }

        static void Say(EggLine[] lines)
        {
            if (lines == null) return;
            foreach (var line in lines) LineSaid?.Invoke(line);
        }

        void ShowState()
        {
            if (_lockdown != null) _lockdown.Sealed = Active.Value;
            if (_fx != null) _fx.SetState(Active.Value ? ReactorFx.State.Waking : StepsDone.Value > 0 ? ReactorFx.State.Awake : ReactorFx.State.Dormant);
        }
    }
}
