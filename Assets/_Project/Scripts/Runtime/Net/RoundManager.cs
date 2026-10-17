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
    /// cleared. Players who bled out come back at the end of the round. Everyone sees the state through
    /// network variables.
    /// </summary>
    public class RoundManager : NetworkBehaviour
    {
        public static RoundManager Instance { get; private set; }

        const float MinSpawnDistance = 12f;
        const float RetrySpawnDelay = 0.25f;

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
        int _toSpawn;
        float _nextSpawnTime;

        public bool IsGameOver => Phase.Value == GamePhase.GameOver;
        public int DisplayRound => Mathf.Max(1, Round.Value);
        public float PhaseTimeLeft => NetworkManager == null ? 0f : Mathf.Max(0f, (float)(PhaseEndsAt.Value - NetworkManager.ServerTime.Time));
        public long CurrentZombieHealth => GameConfig.Round.HealthForRound(DisplayRound);
        public ZombieSpeedTier CurrentSpeedTier => GameConfig.Round.SpeedTierForRound(DisplayRound);
        public int LiveCap => GameConfig.Round.LiveCapFor(NetPlayer.All.Count);

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
                        bool spawned = TrySpawn();
                        _nextSpawnTime = Time.time + (spawned ? GameConfig.Round.SpawnIntervalForRound(Round.Value, NetPlayer.All.Count) : RetrySpawnDelay);
                    }
                    if (_toSpawn == 0 && AliveZombies.Value == 0) CompleteRound();
                    break;
            }
        }

        void HandleDebugKeys()
        {
            if (GameInput.DebugPressed(DebugKey.GodMode)) GodMode.Value = !GodMode.Value;
            if (IsGameOver) return;
            if (GameInput.DebugPressed(DebugKey.NextRound)) SkipToRound(Round.Value + 1);
            if (GameInput.DebugPressed(DebugKey.JumpTenRounds)) SkipToRound(Round.Value + 10);
        }

        static bool AnyPlayerStanding()
        {
            foreach (var p in NetPlayer.All)
                if (p.IsAlive) return true;
            return false;
        }

        void BeginIntermission(float seconds)
        {
            Phase.Value = GamePhase.Intermission;
            PhaseEndsAt.Value = Now + seconds;
            _toSpawn = 0;
            UpdateZombiesLeft();
        }

        void StartRound(int round)
        {
            Round.Value = Mathf.Clamp(round, 1, RoundData.MaxRound);
            _toSpawn = GameConfig.Round.ZombieCountForRound(Round.Value, NetPlayer.All.Count);
            _nextSpawnTime = Time.time + 1f;
            Phase.Value = GamePhase.RoundActive;
            UpdateZombiesLeft();
        }

        void CompleteRound()
        {
            long bonus = GameConfig.Round.RoundBonus(Round.Value);
            foreach (var p in NetPlayer.All) p.Points.Value += bonus;
            RespawnBledOutPlayers();

            if (Round.Value >= RoundData.MaxRound)
            {
                Won.Value = true;
                Phase.Value = GamePhase.GameOver;
                return;
            }
            BeginIntermission(GameConfig.Round.intermissionTime);
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

        /// <summary>Test mode: clears the board and starts a later round straight away (no round bonus).</summary>
        void SkipToRound(int round)
        {
            ClearZombies();
            RespawnBledOutPlayers();
            StartRound(Mathf.Max(1, round));
        }

        void ServerRestart()
        {
            ClearZombies();
            int slot = 0;
            foreach (var p in NetPlayer.All) p.ServerResetForRestart(GameConfig.PlayerSpawn(slot++));
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
            AliveZombies.Value = 0;
            UpdateZombiesLeft();
        }

        void UpdateZombiesLeft() => ZombiesLeft.Value = _toSpawn + AliveZombies.Value;

        // ---------------------------------------------------------------- spawning

        bool TrySpawn()
        {
            var points = GameConfig.ZombieSpawnPoints;
            int start = Random.Range(0, points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[(start + i) % points.Length];
                if (TooCloseToPlayers(point.position)) continue;
                if (!NavMesh.SamplePosition(point.position, out var hit, 2f, NavMesh.AllAreas)) continue;

                var zombie = ZombiePool.Instance.Get(hit.position, point.rotation);
                zombie.ServerSetup(CurrentZombieHealth, CurrentSpeedTier);
                zombie.Died += OnZombieDied;
                zombie.Recycled += OnZombieRecycled;
                zombie.NetworkObject.Spawn(true);
                if (!_zombies.Contains(zombie)) _zombies.Add(zombie);

                _toSpawn--;
                AliveZombies.Value++;
                UpdateZombiesLeft();
                return true;
            }
            return false;
        }

        bool TooCloseToPlayers(Vector3 position)
        {
            foreach (var p in NetPlayer.All)
                if ((p.transform.position - position).sqrMagnitude < MinSpawnDistance * MinSpawnDistance) return true;
            return false;
        }

        void OnZombieDied(NetZombie zombie, DamageInfo info)
        {
            AliveZombies.Value = Mathf.Max(0, AliveZombies.Value - 1);
            UpdateZombiesLeft();
        }

        void OnZombieRecycled(NetZombie zombie)
        {
            AliveZombies.Value = Mathf.Max(0, AliveZombies.Value - 1);
            if (Phase.Value == GamePhase.RoundActive) _toSpawn++;
            UpdateZombiesLeft();
        }
    }
}
