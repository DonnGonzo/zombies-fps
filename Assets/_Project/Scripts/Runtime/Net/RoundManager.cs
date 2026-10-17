using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    public enum GamePhase : byte
    {
        Intermission,
        RoundActive,
        GameOver,
    }

    /// <summary>
    /// The one host-owned game state machine: intermission, round active and game over. Each round spawns a
    /// budget of zombies from RoundData, never more than the live cap at once, and pays a bonus when it is
    /// cleared. The mix of zombie types widens with the rounds, and every 10th round adds a boss. Players who
    /// bled out come back at the end of the round. Everyone sees the state through network variables.
    /// </summary>
    public class RoundManager : NetworkBehaviour
    {
        public static RoundManager Instance { get; private set; }

        const float MinSpawnDistance = 5f;
        const float RetrySpawnDelay = 0.25f;
        /// <summary>Zombies come from one of this many open windows nearest the players, so they find them fast.</summary>
        const int NearestWindows = 4;
        const long DebugPoints = 5000;
        const float FirstBossDelay = 6f;
        const float BossSpacing = 10f;

        /// <summary>Raised on every machine when a boss goes down, with the points everyone got.</summary>
        public static event System.Action<long> BossDefeated;

        public NetworkVariable<GamePhase> Phase = new NetworkVariable<GamePhase>(GamePhase.Intermission);
        /// <summary>The current round, or the last one played during an intermission. 0 before round 1.</summary>
        public NetworkVariable<int> Round = new NetworkVariable<int>(0);
        /// <summary>Server time when the intermission ends.</summary>
        public NetworkVariable<double> PhaseEndsAt = new NetworkVariable<double>(0);
        /// <summary>Zombies still to spawn plus zombies alive: the round ends at 0.</summary>
        public NetworkVariable<int> ZombiesLeft = new NetworkVariable<int>(0);
        public NetworkVariable<int> AliveZombies = new NetworkVariable<int>(0);
        /// <summary>Test mode: players take no damage and have endless ammo.</summary>
        public NetworkVariable<bool> GodMode = new NetworkVariable<bool>(false);
        /// <summary>Set with game over when round 500 is cleared.</summary>
        public NetworkVariable<bool> Won = new NetworkVariable<bool>(false);

        readonly List<NetZombie> _zombies = new List<NetZombie>();
        readonly List<Transform> _spawnPoints = new List<Transform>();
        int _toSpawn;
        float _nextSpawnTime;
        int _bossesToSpawn;
        float _nextBossTime;
        readonly int[] _aliveByKind = new int[ZombieData.KindCount];
        readonly float[] _weights = new float[ZombieData.KindCount];

        public bool IsGameOver => Phase.Value == GamePhase.GameOver;
        public int DisplayRound => Mathf.Max(1, Round.Value);
        public float PhaseTimeLeft => NetworkManager == null ? 0f : Mathf.Max(0f, (float)(PhaseEndsAt.Value - NetworkManager.ServerTime.Time));
        public long CurrentZombieHealth => GameConfig.Round.HealthForRound(DisplayRound);
        public ZombieSpeedTier CurrentSpeedTier => GameConfig.Round.SpeedTierForRound(DisplayRound);
        public int LiveCap => GameConfig.Round.LiveCapFor(NetPlayer.All.Count);
        /// <summary>The lights are out this round (a mini-event every 25th round): dark, runner-heavy, double points.</summary>
        public bool IsBlackout => Phase.Value == GamePhase.RoundActive && GameConfig.Round.IsBlackoutRound(Round.Value);
        public float PointsMultiplier => IsBlackout ? GameConfig.Round.blackoutPointsMultiplier : 1f;

        double Now => NetworkManager.ServerTime.Time;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) BeginIntermission(GameConfig.Round.firstRoundDelay);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- server loop

        void Update()
        {
            if (!IsServer) return;
            HandleDebugKeys();

            if (IsGameOver)
            {
                if (GameInput.DebugPressed(DebugKey.Restart)) ServerRestart();
                return;
            }

            if (NetPlayer.All.Count > 0 && !AnyPlayerStanding())
            {
                Phase.Value = GamePhase.GameOver;
                return;
            }

            _zombies.RemoveAll(z => z == null || !z.IsSpawned);

            switch (Phase.Value)
            {
                case GamePhase.Intermission:
                    if (Now >= PhaseEndsAt.Value) StartRound(Round.Value + 1);
                    break;

                case GamePhase.RoundActive:
                    if (_toSpawn > 0 && AliveZombies.Value < LiveCap && Time.time >= _nextSpawnTime)
                    {
                        bool spawned = TrySpawn(PickKind(Round.Value));
                        _nextSpawnTime = Time.time + (spawned ? GameConfig.Round.SpawnIntervalForRound(Round.Value, NetPlayer.All.Count) : RetrySpawnDelay);
                    }
                    // Bosses come on top of the horde and ignore the live cap.
                    if (_bossesToSpawn > 0 && Time.time >= _nextBossTime)
                        _nextBossTime = Time.time + (TrySpawn(ZombieKind.Boss) ? BossSpacing : RetrySpawnDelay);
                    if (_toSpawn == 0 && _bossesToSpawn == 0 && AliveZombies.Value == 0) CompleteRound();
                    break;
            }
        }

        void HandleDebugKeys()
        {
            if (GameInput.DebugPressed(DebugKey.GodMode)) GodMode.Value = !GodMode.Value;
            if (GameInput.DebugPressed(DebugKey.GivePoints))
                foreach (var p in NetPlayer.All) p.Points.Value += DebugPoints;
            if (IsGameOver) return;
            if (GameInput.DebugPressed(DebugKey.OfferPowerUp)) OfferPowerUps(false);
            if (GameInput.DebugPressed(DebugKey.SpawnBoss) && Phase.Value == GamePhase.RoundActive)
            {
                _bossesToSpawn++;
                _nextBossTime = Time.time;
                UpdateZombiesLeft();
            }
            if (GameInput.DebugPressed(DebugKey.NextRound)) SkipToRound(Round.Value + 1);
            if (GameInput.DebugPressed(DebugKey.JumpTenRounds)) SkipToRound(Round.Value + 10);
        }

        // ---------------------------------------------------------------- test console (host only)

        /// <summary>Host only: jump straight to a round, as the ] key does.</summary>
        public void DebugSkipToRound(int round)
        {
            if (IsServer && !IsGameOver) SkipToRound(Mathf.Clamp(round, 1, RoundData.MaxRound));
        }

        public void DebugToggleGod()
        {
            if (IsServer) GodMode.Value = !GodMode.Value;
        }

        public void DebugOfferPowerUps(bool empowered)
        {
            if (IsServer && !IsGameOver) OfferPowerUps(empowered);
        }

        /// <summary>Host only: spawns extra zombies of a type right now (bosses included), on top of the round.</summary>
        public int DebugSpawn(ZombieKind kind, int count)
        {
            if (!IsServer || IsGameOver || Phase.Value != GamePhase.RoundActive) return 0;
            int spawned = 0;
            for (int i = 0; i < Mathf.Clamp(count, 1, 50); i++)
                if (TrySpawn(kind)) spawned++;
            UpdateZombiesLeft();
            return spawned;
        }

        /// <summary>Host only: removes every zombie, which ends the round once the spawn queue is empty too.</summary>
        public void DebugClearZombies()
        {
            if (!IsServer || IsGameOver) return;
            ClearZombies();
            UpdateZombiesLeft();
        }

        static bool AnyPlayerStanding()
        {
            foreach (var p in NetPlayer.All)
                if (p.IsAlive || p.SelfReviving) return true; // Solo Second Wind gets back up.
            return false;
        }

        void BeginIntermission(float seconds)
        {
            Phase.Value = GamePhase.Intermission;
            PhaseEndsAt.Value = Now + seconds;
            _toSpawn = 0;
            _bossesToSpawn = 0;
            UpdateZombiesLeft();
        }

        void StartRound(int round)
        {
            Round.Value = Mathf.Clamp(round, 1, RoundData.MaxRound);
            _toSpawn = GameConfig.Round.ZombieCountForRound(Round.Value, NetPlayer.All.Count);
            _nextSpawnTime = Time.time + 1f;
            _bossesToSpawn = GameConfig.Round.BossCountForRound(Round.Value);
            _nextBossTime = Time.time + FirstBossDelay;
            if (MissionBoard.Instance != null) MissionBoard.Instance.ServerOnRoundStarted(Round.Value);
            if (Round.Value > 1)
                foreach (var p in NetPlayer.All) p.ServerRefillEquipment(full: false);
            Phase.Value = GamePhase.RoundActive;
            UpdateZombiesLeft();
        }

        void CompleteRound()
        {
            long bonus = GameConfig.Round.RoundBonus(Round.Value);
            long xp = GameConfig.Shop != null ? GameConfig.Shop.XpForRound(Round.Value) : 0;
            foreach (var p in NetPlayer.All)
            {
                p.Points.Value += bonus;
                p.ServerAddXp(xp);
            }
            RespawnBledOutPlayers();
            if (MissionBoard.Instance != null) MissionBoard.Instance.ServerOnRoundCompleted();
            var upgrades = GameConfig.Upgrades;
            if (upgrades != null && upgrades.IsPowerUpRound(Round.Value)) OfferPowerUps(upgrades.IsEmpoweredRound(Round.Value));

            if (Round.Value >= RoundData.MaxRound)
            {
                Won.Value = true;
                Phase.Value = GamePhase.GameOver;
                return;
            }
            BeginIntermission(GameConfig.Round.intermissionTime);
        }

        static void OfferPowerUps(bool empowered)
        {
            foreach (var p in NetPlayer.All) p.ServerOfferPowerUps(empowered);
        }

        void RespawnBledOutPlayers()
        {
            int slot = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p.State.Value == LifeState.Dead) p.ServerRespawn(GameConfig.PlayerSpawn(slot));
                slot++;
            }
        }

        /// <summary>
        /// Test mode: clears the board and starts a later round straight away (no round bonus). Power-ups from
        /// the rounds skipped over still arrive, one offer per milestone, so jumping ahead plays like getting there.
        /// </summary>
        void SkipToRound(int round)
        {
            var upgrades = GameConfig.Upgrades;
            int firstSkipped = Phase.Value == GamePhase.Intermission ? Round.Value + 1 : Round.Value;
            if (upgrades != null)
                for (int r = Mathf.Max(1, firstSkipped); r < round; r++)
                    if (upgrades.IsPowerUpRound(r)) OfferPowerUps(upgrades.IsEmpoweredRound(r));
            ClearZombies();
            RespawnBledOutPlayers();
            StartRound(Mathf.Max(1, round));
        }

        void ServerRestart()
        {
            ClearZombies();
            Weapons.Throwable.ClearAll();
            int slot = 0;
            foreach (var p in NetPlayer.All) p.ServerResetForRestart(GameConfig.PlayerSpawn(slot++));
            if (MapState.Instance != null) MapState.Instance.ServerReset();
            if (MissionBoard.Instance != null) MissionBoard.Instance.ServerReset();
            if (BuildManager.Instance != null) BuildManager.Instance.ServerReset();
            Round.Value = 0;
            Won.Value = false;
            BeginIntermission(GameConfig.Round.firstRoundDelay);
        }

        void ClearZombies()
        {
            foreach (var z in _zombies.ToArray())
                if (z != null) z.ServerRemove();
            _zombies.Clear();
            _toSpawn = 0;
            _bossesToSpawn = 0;
            System.Array.Clear(_aliveByKind, 0, _aliveByKind.Length);
            AliveZombies.Value = 0;
            UpdateZombiesLeft();
        }

        void UpdateZombiesLeft() => ZombiesLeft.Value = _toSpawn + _bossesToSpawn + AliveZombies.Value;

        // ---------------------------------------------------------------- spawning

        /// <summary>A weighted pick among the types this round allows, skipping types at their alive limit. Walkers always qualify.</summary>
        ZombieKind PickKind(int round)
        {
            float total = 0f;
            for (int i = 0; i < ZombieData.KindCount; i++)
            {
                var data = GameConfig.ZombieType((ZombieKind)i);
                float w = i == 0 ? 1f : data.kind == (ZombieKind)i ? data.WeightForRound(round) : 0f;
                if ((ZombieKind)i == ZombieKind.Runner && IsBlackout) w *= GameConfig.Round.blackoutRunnerWeight;
                if (data.maxAlive > 0 && _aliveByKind[i] >= data.maxAlive) w = 0f;
                _weights[i] = w;
                total += w;
            }
            if (total <= 0f) return ZombieKind.Walker;
            float roll = Random.value * total;
            for (int i = 0; i < ZombieData.KindCount; i++)
            {
                roll -= _weights[i];
                if (roll < 0f && _weights[i] > 0f) return (ZombieKind)i;
            }
            return ZombieKind.Walker;
        }

        long HealthFor(ZombieKind kind)
        {
            double health = CurrentZombieHealth;
            var data = GameConfig.ZombieType(kind);
            int extraPlayers = Mathf.Max(0, NetPlayer.All.Count - 1);
            if (data.healthPerExtraPlayer > 0f) health *= 1.0 + data.healthPerExtraPlayer * extraPlayers;
            return (long)System.Math.Round(health);
        }

        bool TrySpawn(ZombieKind kind)
        {
            // Windows in open zones, nearest the players first; pick at random among the closest few.
            GameConfig.Map.CollectSpawnPoints(MapState.Instance != null ? MapState.Instance.OpenDoorMask : 0, _spawnPoints);
            _spawnPoints.RemoveAll(p => TooCloseToPlayers(p.position));
            _spawnPoints.Sort((a, b) => DistanceToPlayers(a.position).CompareTo(DistanceToPlayers(b.position)));
            int count = Mathf.Min(NearestWindows, _spawnPoints.Count);
            int start = Random.Range(0, Mathf.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                var point = _spawnPoints[(start + i) % count];
                if (!NavMesh.SamplePosition(point.position, out var hit, 2f, NavMesh.AllAreas)) continue;

                var zombie = ZombiePool.Instance.Get(hit.position, point.rotation);
                zombie.ServerSetup(kind, HealthFor(kind), CurrentSpeedTier);
                zombie.Died += OnZombieDied;
                zombie.Recycled += OnZombieRecycled;
                zombie.NetworkObject.Spawn(true);
                if (!_zombies.Contains(zombie)) _zombies.Add(zombie);

                if (kind == ZombieKind.Boss) _bossesToSpawn--;
                else _toSpawn--;
                _aliveByKind[(int)kind]++;
                AliveZombies.Value++;
                UpdateZombiesLeft();
                return true;
            }
            return false;
        }

        static float DistanceToPlayers(Vector3 position)
        {
            float best = float.MaxValue;
            foreach (var p in NetPlayer.All)
                if (p.IsAlive) best = Mathf.Min(best, (p.transform.position - position).sqrMagnitude);
            return best;
        }

        bool TooCloseToPlayers(Vector3 position)
        {
            foreach (var p in NetPlayer.All)
                if ((p.transform.position - position).sqrMagnitude < MinSpawnDistance * MinSpawnDistance) return true;
            return false;
        }

        void OnZombieDied(NetZombie zombie, DamageInfo info)
        {
            Forget(zombie);
            if (MissionBoard.Instance != null) MissionBoard.Instance.ServerOnKill(zombie, info.IsHeadshot);
            if (zombie.Kind == ZombieKind.Boss)
            {
                long bonus = GameConfig.Round.bossBonus;
                foreach (var p in NetPlayer.All) p.Points.Value += bonus;
                BossDefeatedRpc(bonus);
            }
        }

        void OnZombieRecycled(NetZombie zombie)
        {
            Forget(zombie);
            if (Phase.Value == GamePhase.RoundActive)
            {
                if (zombie.Kind == ZombieKind.Boss) _bossesToSpawn++;
                else _toSpawn++;
            }
            UpdateZombiesLeft();
        }

        void Forget(NetZombie zombie)
        {
            int kind = (int)zombie.Kind;
            if (kind < _aliveByKind.Length) _aliveByKind[kind] = Mathf.Max(0, _aliveByKind[kind] - 1);
            AliveZombies.Value = Mathf.Max(0, AliveZombies.Value - 1);
            UpdateZombiesLeft();
        }

        [Rpc(SendTo.ClientsAndHost)]
        void BossDefeatedRpc(long bonus) => BossDefeated?.Invoke(bonus);
    }
}
