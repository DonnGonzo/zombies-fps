using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Data;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Sergeant Brannock on the run. Once the ambush starts he leaves his post and keeps moving round the Crane Yard,
    /// away from the zombies and towards the squad, while they deal with the monsters. The host moves him with a
    /// hidden NavMeshAgent and shares where he is; every machine draws him there.
    /// </summary>
    public partial class EasterEgg
    {
        /// <summary>Where Brannock is: his post unless he's running from the ambush.</summary>
        public NetworkVariable<Vector3> WatchmanAt = new NetworkVariable<Vector3>(Vector3.zero);

        public Vector3 WatchmanPosition => WatchmanAt.Value == Vector3.zero ? ZoneMap.WatchmanSpot : WatchmanAt.Value;

        // Server.
        NavMeshAgent _watchAgent;
        Vector3 _watchGoal;
        float _nextWatchPick, _watchHitAt = -100f;
        static readonly Vector3[] s_WatchDirs = MakeDirs(16);

        static Vector3[] MakeDirs(int n)
        {
            var dirs = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                dirs[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            return dirs;
        }

        /// <summary>Host: moves Brannock once the ambush is on. Before that he waits at his post.</summary>
        void WatchmanRunTick()
        {
            if (Time.time < _introEnds) return;
            if (_watchAgent == null && !StartWatchmanRun()) return;
            if (!_watchAgent.isOnNavMesh) return;

            bool arrived = !_watchAgent.pathPending && _watchAgent.remainingDistance < 0.6f;
            if (Time.time >= _nextWatchPick || arrived)
            {
                _nextWatchPick = Time.time + EasterEggData.WatchmanRethink;
                var goal = PickWatchmanGoal(_watchAgent.transform.position);
                if ((goal - _watchGoal).sqrMagnitude > 1f || arrived)
                {
                    _watchGoal = goal;
                    _watchAgent.SetDestination(goal);
                }
            }
            // Frightened when they're on him: a burst of speed to break away.
            bool hit = Time.time - _watchHitAt < 1f;
            _watchAgent.speed = hit ? EasterEggData.WatchmanSprint : EasterEggData.WatchmanSpeed;

            var at = _watchAgent.transform.position;
            if ((at - WatchmanAt.Value).sqrMagnitude > 0.04f) WatchmanAt.Value = at;
        }

        bool StartWatchmanRun()
        {
            if (!NavMesh.SamplePosition(ZoneMap.WatchmanSpot, out var hit, 3f, NavMesh.AllAreas)) return false;
            var go = new GameObject("Brannock (host mover)");
            go.transform.position = hit.position;
            _watchAgent = go.AddComponent<NavMeshAgent>();
            _watchAgent.radius = 0.35f;
            _watchAgent.height = 1.8f;
            _watchAgent.speed = EasterEggData.WatchmanSpeed;
            _watchAgent.acceleration = 14f;
            _watchAgent.angularSpeed = 720f;
            _watchAgent.autoBraking = true;
            _watchAgent.stoppingDistance = 0.3f;
            // Zombies step round him rather than shove him about, and he never takes ladders or jumps.
            _watchAgent.avoidancePriority = 20;
            _watchAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            _watchAgent.autoTraverseOffMeshLink = false;
            _watchAgent.Warp(hit.position);
            _watchGoal = hit.position;
            _nextWatchPick = 0f;
            WatchmanAt.Value = hit.position;
            return true;
        }

        /// <summary>Host: the ambush is over (won, lost or reset). Brannock goes back to his post.</summary>
        void StopWatchmanRun()
        {
            if (_watchAgent != null) Destroy(_watchAgent.gameObject);
            _watchAgent = null;
            _watchHitAt = -100f;
            if (IsServer && IsSpawned) WatchmanAt.Value = Vector3.zero;
        }

        /// <summary>
        /// Host: somewhere in the Crane Yard a few metres off that's as far from the zombies as he can get, a little
        /// nearer the players, and not through the pack to reach it.
        /// </summary>
        Vector3 PickWatchmanGoal(Vector3 from)
        {
            Vector3 best = from;
            float bestScore = Threat(from, from) - 0.5f;
            foreach (float reach in EasterEggData.WatchmanHops)
                foreach (var dir in s_WatchDirs)
                {
                    var want = from + dir * reach;
                    if (!NavMesh.SamplePosition(want, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                    var p = hit.position;
                    if (Mathf.Abs(p.y - from.y) > 1.2f) continue;
                    if (ZoneMap.ZoneOfPoint(p) != EasterEggData.CraneYardZone) continue;
                    float score = Threat(from, p);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }
            return best;
        }

        /// <summary>Lower is safer: zombies close to the spot or on the way there count against it, players for it.</summary>
        float Threat(Vector3 from, Vector3 p)
        {
            float threat = 0f;
            foreach (var z in _zombies)
            {
                if (z == null || !z.IsAlive) continue;
                var zp = z.transform.position;
                float d = Flat(zp - p).sqrMagnitude;
                if (d > 18f * 18f) continue;
                threat += 10f / (d + 1f);
                // Running past a zombie is worse than standing far from one.
                float s = Flat(ClosestOnSegment(from, p, zp) - zp).sqrMagnitude;
                if (s < 2.5f * 2.5f) threat += 6f / (s + 0.5f);
            }
            float nearest = float.MaxValue;
            foreach (var pl in NetPlayer.All)
            {
                if (pl == null || pl.State.Value != LifeState.Alive) continue;
                nearest = Mathf.Min(nearest, Flat(pl.transform.position - p).magnitude);
            }
            // Behind the squad is good, but not on top of them.
            if (nearest < float.MaxValue) threat += Mathf.Clamp(nearest - 3f, 0f, 20f) * 0.04f;
            // Corners are traps: a small pull back towards open ground at the post.
            threat += Flat(p - ZoneMap.WatchmanSpot).magnitude * 0.01f;
            return threat;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float len = ab.sqrMagnitude;
            if (len < 0.0001f) return a;
            return a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
        }

        /// <summary>Host: catching his breath. A few seconds clear of zombies and he slowly patches himself up.</summary>
        void WatchmanRecover()
        {
            if (Time.time - _watchHitAt < EasterEggData.WatchmanRecoverDelay || WatchmanHealth.Value >= 1f) return;
            WatchmanHealth.Value = Mathf.Min(1f, WatchmanHealth.Value + EasterEggData.WatchmanRecoverRate * Time.deltaTime);
        }
    }
}
