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
    /// <summary>
    /// Host-owned game state shared with everyone: practice spawner settings, zombie count and game over.
    /// Phase 2 grows this into the RoundManager (rounds, spawn budget, intermissions).
    /// </summary>
    public class GameDirector : NetworkBehaviour
    {
        public static GameDirector Instance { get; private set; }

        const float MinSpawnDistance = 12f;
        const float SpawnInterval = 0.8f;

        public NetworkVariable<int> TestRound = new NetworkVariable<int>(1);
        public NetworkVariable<int> TargetAlive = new NetworkVariable<int>(1);
        public NetworkVariable<int> AliveZombies = new NetworkVariable<int>(0);
        public NetworkVariable<bool> GameOver = new NetworkVariable<bool>(false);

        readonly List<NetZombie> _zombies = new List<NetZombie>();
        float _nextSpawnTime;

        public long CurrentZombieHealth => GameConfig.Round.HealthForRound(TestRound.Value);
        public ZombieSpeedTier CurrentSpeedTier => GameConfig.Round.SpeedTierForRound(TestRound.Value);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _nextSpawnTime = Time.time + 2f;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!IsServer) return;

            // Debug keys work on the host only.
            int cap = GameConfig.Round.liveCap;
            if (GameInput.DebugPressed(DebugKey.ZombiesUp)) TargetAlive.Value = Mathf.Min(cap, TargetAlive.Value + 1);
            if (GameInput.DebugPressed(DebugKey.ZombiesDown)) TargetAlive.Value = Mathf.Max(0, TargetAlive.Value - 1);
            if (GameInput.DebugPressed(DebugKey.RoundUp)) TestRound.Value = Mathf.Min(RoundData.MaxRound, TestRound.Value + (TestRound.Value >= 10 ? 5 : 1));
            if (GameInput.DebugPressed(DebugKey.RoundDown)) TestRound.Value = Mathf.Max(1, TestRound.Value - (TestRound.Value > 10 ? 5 : 1));

            if (GameOver.Value)
            {
                if (GameInput.DebugPressed(DebugKey.Restart)) ServerRestart();
                return;
            }

            if (NetPlayer.All.Count > 0)
            {
                bool anyAlive = false;
                foreach (var p in NetPlayer.All) anyAlive |= p.IsAlive;
                if (!anyAlive)
                {
                    GameOver.Value = true;
                    return;
                }
            }

            _zombies.RemoveAll(z => z == null);
            if (AliveZombies.Value < Mathf.Min(TargetAlive.Value, cap) && Time.time >= _nextSpawnTime)
                _nextSpawnTime = Time.time + (TrySpawn() ? SpawnInterval : 0.25f);
        }

        bool TrySpawn()
        {
            var points = GameConfig.ZombieSpawnPoints;
            int start = Random.Range(0, points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[(start + i) % points.Length];
                if (TooCloseToPlayers(point.position)) continue;
                if (!NavMesh.SamplePosition(point.position, out var hit, 2f, NavMesh.AllAreas)) continue;

                // Phase 2 replaces Instantiate with a pooled prefab handler (design rule: no Instantiate mid-round).
                var go = Instantiate(GameConfig.ZombiePrefab, hit.position, point.rotation);
                var zombie = go.GetComponent<NetZombie>();
                zombie.ServerSetup(CurrentZombieHealth, CurrentSpeedTier);
                zombie.Died += OnZombieDied;
                go.GetComponent<NetworkObject>().Spawn(true);
                _zombies.Add(zombie);
                AliveZombies.Value++;
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
        }

        void ServerRestart()
        {
            foreach (var z in _zombies)
                if (z != null && z.IsSpawned) z.NetworkObject.Despawn(true);
            _zombies.Clear();
            AliveZombies.Value = 0;

            int slot = 0;
            foreach (var p in NetPlayer.All) p.ServerResetForRestart(GameConfig.PlayerSpawn(slot++));
            GameOver.Value = false;
            _nextSpawnTime = Time.time + 2f;
        }
    }
}
