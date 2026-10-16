using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Player;

namespace ZombiesFPS.Zombies
{
    /// <summary>
    /// Phase 0 practice spawner: keeps a chosen number of zombies alive at the chosen test round's
    /// health and speed. Zombies are pooled up to the live cap. Phase 2 replaces this with the
    /// RoundManager (spawn budget per round, intermissions, game over).
    /// </summary>
    public class ZombieSpawner : MonoBehaviour
    {
        public float minSpawnDistance = 12f;
        public float spawnInterval = 0.8f;

        static readonly Color[] SkinTones =
        {
            new Color(0.42f, 0.5f, 0.36f),
            new Color(0.5f, 0.52f, 0.42f),
            new Color(0.38f, 0.44f, 0.4f),
        };

        ZombieData _zombieData;
        RoundData _roundData;
        Transform[] _spawnPoints;
        Transform _player;
        PlayerHealth _playerHealth;
        readonly List<Zombie> _pool = new List<Zombie>();
        float _nextSpawnTime;

        public int TestRound { get; private set; } = 1;
        public int TargetAlive { get; private set; } = 1;
        public int Alive { get; private set; }
        public int Kills { get; private set; }
        public int Headshots { get; private set; }
        public long Points { get; private set; }
        public long CurrentZombieHealth => _roundData.HealthForRound(TestRound);
        public ZombieSpeedTier CurrentSpeedTier => _roundData.SpeedTierForRound(TestRound);
        public int LiveCap => _roundData.liveCap;

        public void Init(ZombieData zombieData, RoundData roundData, Transform[] spawnPoints, Transform player, PlayerHealth playerHealth)
        {
            _zombieData = zombieData;
            _roundData = roundData;
            _spawnPoints = spawnPoints;
            _player = player;
            _playerHealth = playerHealth;

            // Pre-warm the pool so nothing is instantiated mid-fight.
            for (int i = 0; i < _roundData.liveCap; i++) CreateZombie(i);
            _nextSpawnTime = Time.time + 1.5f;
        }

        public void SetTestRound(int round) => TestRound = Mathf.Clamp(round, 1, RoundData.MaxRound);
        public void SetTargetAlive(int count) => TargetAlive = Mathf.Clamp(count, 0, _roundData.liveCap);

        Zombie CreateZombie(int index)
        {
            var go = new GameObject("Zombie " + index);
            go.SetActive(false); // Inactive until placed on the NavMesh.
            go.transform.SetParent(transform, false);
            var zombie = go.AddComponent<Zombie>();
            zombie.Build(SkinTones[index % SkinTones.Length]);
            zombie.Damaged += OnZombieDamaged;
            zombie.Died += OnZombieDied;
            _pool.Add(zombie);
            return zombie;
        }

        void Update()
        {
            if (_roundData == null || _playerHealth == null || !_playerHealth.IsAlive) return;

            if (GameInput.DebugPressed(DebugKey.ZombiesUp)) SetTargetAlive(TargetAlive + 1);
            if (GameInput.DebugPressed(DebugKey.ZombiesDown)) SetTargetAlive(TargetAlive - 1);
            if (GameInput.DebugPressed(DebugKey.RoundUp)) SetTestRound(TestRound + (TestRound >= 10 ? 5 : 1));
            if (GameInput.DebugPressed(DebugKey.RoundDown)) SetTestRound(TestRound - (TestRound > 10 ? 5 : 1));

            if (Alive < TargetAlive && Time.time >= _nextSpawnTime)
            {
                if (TrySpawn()) _nextSpawnTime = Time.time + spawnInterval;
                else _nextSpawnTime = Time.time + 0.25f;
            }
        }

        bool TrySpawn()
        {
            Zombie zombie = null;
            foreach (var z in _pool)
            {
                if (!z.IsActive)
                {
                    zombie = z;
                    break;
                }
            }
            if (zombie == null) return false; // Every pooled zombie is alive or still a corpse.

            // Random spawn point that isn't right next to the player.
            int start = Random.Range(0, _spawnPoints.Length);
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                var point = _spawnPoints[(start + i) % _spawnPoints.Length];
                if ((point.position - _player.position).sqrMagnitude < minSpawnDistance * minSpawnDistance) continue;
                if (!NavMesh.SamplePosition(point.position, out var hit, 2f, NavMesh.AllAreas)) continue;

                Vector3 look = _player.position - hit.position;
                look.y = 0f;
                var rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : point.rotation;
                zombie.Spawn(hit.position, rotation, _zombieData, CurrentZombieHealth, CurrentSpeedTier, _player, _playerHealth);
                Alive++;
                return true;
            }
            return false;
        }

        void OnZombieDamaged(Zombie zombie, DamageInfo info, DamageResult result)
        {
            if (!result.Killed) Points += _zombieData.pointsPerHit;
        }

        void OnZombieDied(Zombie zombie, DamageInfo info)
        {
            Alive = Mathf.Max(0, Alive - 1);
            Kills++;
            if (info.IsHeadshot) Headshots++;
            Points += info.IsHeadshot ? _zombieData.pointsPerHeadshotKill : _zombieData.pointsPerKill;
        }
    }
}
