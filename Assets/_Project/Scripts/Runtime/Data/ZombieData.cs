using UnityEngine;

namespace ZombiesFPS.Data
{
    public enum ZombieSpeedTier
    {
        Walk,
        Jog,
        Sprint,
    }

    /// <summary>One zombie type. Health is not here: it comes from RoundData so every type scales the same way.</summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Zombie Data", fileName = "Zombie_New")]
    public class ZombieData : ScriptableObject
    {
        public string displayName = "Walker";
        [Tooltip("Health multiplier on top of RoundData health. Walker = 1.")]
        public float healthMultiplier = 1f;

        [Header("Movement (m/s)")]
        public float walkSpeed = 1.4f;
        public float jogSpeed = 3.0f;
        public float sprintSpeed = 4.9f;

        [Header("Attack")]
        public float damage = 40f;
        public float attackRange = 1.7f;
        [Tooltip("Time from starting the swing to the hit landing. The player can back off to dodge.")]
        public float attackWindup = 0.45f;
        public float attackRecovery = 0.55f;

        [Header("Reactions")]
        [Tooltip("How long a hit slows the zombie down.")]
        public float staggerTime = 0.12f;
        [Range(0f, 1f)] public float staggerSpeedScale = 0.3f;

        [Header("Rewards (Phase 2 economy uses these)")]
        public long pointsPerHit = 10;
        public long pointsPerKill = 60;
        public long pointsPerHeadshotKill = 100;

        public float SpeedFor(ZombieSpeedTier tier)
        {
            switch (tier)
            {
                case ZombieSpeedTier.Jog: return jogSpeed;
                case ZombieSpeedTier.Sprint: return sprintSpeed;
                default: return walkSpeed;
            }
        }
    }
}
