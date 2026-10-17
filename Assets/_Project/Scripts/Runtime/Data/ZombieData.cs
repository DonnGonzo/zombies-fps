using UnityEngine;

namespace ZombiesFPS.Data
{
    public enum ZombieSpeedTier
    {
        Walk,
        Jog,
        Sprint,
    }

    /// <summary>Every zombie type. The order is the network id and the index into GameConfig.ZombieTypes.</summary>
    public enum ZombieKind : byte
    {
        Walker,
        Runner,
        Crawler,
        Brute,
        Spitter,
        Boss,
    }

    /// <summary>One zombie type. Health is not here: it comes from RoundData so every type scales the same way.</summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Zombie Data", fileName = "Zombie_New")]
    public class ZombieData : ScriptableObject
    {
        public const int KindCount = 6;

        public ZombieKind kind = ZombieKind.Walker;
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

        [Header("Spawning")]
        [Tooltip("First round this type can appear.")]
        public int firstRound = 1;
        [Tooltip("Share of spawns relative to walkers (walker = 1) once fully ramped in.")]
        public float spawnWeight = 1f;
        [Tooltip("Rounds it takes to ramp from a trickle to the full weight.")]
        public int rampRounds = 1;
        [Tooltip("Most of this type alive at once (0 = no limit).")]
        public int maxAlive = 0;

        [Header("Look")]
        public Vector3 bodyScale = Vector3.one;
        [Tooltip("Multiplies the random skin tone.")]
        public Color skinTint = Color.white;
        public Color clothColor = new Color(0.22f, 0.2f, 0.18f);
        [Tooltip("Moves on all fours: a low target that's hard to hit.")]
        public bool crawls;
        [Tooltip("Shoulder and chest plates.")]
        public bool armored;

        [Header("Toughness")]
        [Tooltip("Damage taken on the body (0.5 = armour halves it). Headshots ignore this.")]
        public float bodyMultiplier = 1f;
        [Tooltip("Damage taken on the glowing weak point on its back (0 = no weak point).")]
        public float weakPointMultiplier = 0f;
        [Tooltip("Extra health per player beyond the first (0.5 = +50%).")]
        public float healthPerExtraPlayer = 0f;

        [Header("Leg hits")]
        [Tooltip("A hit slows the player to this share of their speed.")]
        [Range(0.1f, 1f)] public float slowOnHit = 1f;
        public float slowTime = 0f;

        [Header("Ranged (acid spit)")]
        [Tooltip("Spits at players in sight within this range (0 = melee only).")]
        public float spitRange = 0f;
        public float spitCooldown = 3.5f;
        public float spitWindup = 0.5f;
        public float spitSpeed = 12f;
        [Tooltip("Damage per second to anyone standing in the acid.")]
        public float puddleDps = 14f;
        public float puddleTime = 4f;
        public float puddleRadius = 1.5f;

        public bool IsBoss => kind == ZombieKind.Boss;

        /// <summary>Spawn weight on a round: 0 before firstRound, then ramps up to spawnWeight. Bosses never come from the mix.</summary>
        public float WeightForRound(int round)
        {
            if (IsBoss || round < firstRound) return 0f;
            return spawnWeight * Mathf.Clamp01((round - firstRound + 1f) / Mathf.Max(1, rampRounds));
        }

        public float SpeedFor(ZombieSpeedTier tier)
        {
            switch (tier)
            {
                case ZombieSpeedTier.Jog: return jogSpeed;
                case ZombieSpeedTier.Sprint: return sprintSpeed;
                default: return walkSpeed;
            }
        }

        /// <summary>Built-in values for a type, used when the scene has no asset for it.</summary>
        public static ZombieData CreateDefault(ZombieKind kind)
        {
            var z = CreateInstance<ZombieData>();
            z.kind = kind;
            z.name = z.displayName = kind.ToString();
            switch (kind)
            {
                case ZombieKind.Runner:
                    z.healthMultiplier = 0.6f;
                    z.walkSpeed = 3.4f; z.jogSpeed = 4.6f; z.sprintSpeed = 5.8f;
                    z.damage = 25f; z.attackWindup = 0.35f; z.attackRecovery = 0.4f;
                    z.firstRound = 8; z.spawnWeight = 0.35f; z.rampRounds = 8;
                    z.bodyScale = new Vector3(0.85f, 0.95f, 0.85f);
                    z.skinTint = new Color(1.1f, 0.85f, 0.8f); z.clothColor = new Color(0.35f, 0.12f, 0.1f);
                    z.pointsPerKill = 70;
                    break;
                case ZombieKind.Crawler:
                    z.healthMultiplier = 0.7f;
                    z.walkSpeed = 2f; z.jogSpeed = 2.6f; z.sprintSpeed = 3.4f;
                    z.damage = 20f; z.attackRange = 1.4f;
                    z.slowOnHit = 0.5f; z.slowTime = 2f;
                    z.firstRound = 12; z.spawnWeight = 0.25f; z.rampRounds = 8; z.maxAlive = 8;
                    z.crawls = true; z.clothColor = new Color(0.18f, 0.22f, 0.16f);
                    z.pointsPerKill = 70;
                    break;
                case ZombieKind.Brute:
                    z.healthMultiplier = 3f;
                    z.walkSpeed = 1.1f; z.jogSpeed = 1.7f; z.sprintSpeed = 2.5f;
                    z.damage = 70f; z.attackRange = 2f; z.attackWindup = 0.7f; z.attackRecovery = 0.8f;
                    z.staggerTime = 0.05f; z.staggerSpeedScale = 0.7f;
                    z.bodyMultiplier = 0.5f; z.armored = true;
                    z.firstRound = 20; z.spawnWeight = 0.12f; z.rampRounds = 10; z.maxAlive = 4;
                    z.bodyScale = new Vector3(1.35f, 1.3f, 1.35f); z.clothColor = new Color(0.3f, 0.3f, 0.32f);
                    z.pointsPerKill = 150; z.pointsPerHeadshotKill = 250;
                    break;
                case ZombieKind.Spitter:
                    z.healthMultiplier = 0.8f;
                    z.walkSpeed = 1.3f; z.jogSpeed = 2.3f; z.sprintSpeed = 3.2f;
                    z.damage = 30f; z.spitRange = 14f;
                    z.firstRound = 30; z.spawnWeight = 0.1f; z.rampRounds = 10; z.maxAlive = 3;
                    z.bodyScale = new Vector3(1.1f, 1f, 1.1f);
                    z.skinTint = new Color(0.8f, 1.15f, 0.7f); z.clothColor = new Color(0.2f, 0.3f, 0.12f);
                    z.pointsPerKill = 100;
                    break;
                case ZombieKind.Boss:
                    z.displayName = "Warden";
                    z.healthMultiplier = 20f; z.healthPerExtraPlayer = 0.5f;
                    z.walkSpeed = 2.4f; z.jogSpeed = 2.8f; z.sprintSpeed = 3.2f;
                    z.damage = 85f; z.attackRange = 2.6f; z.attackWindup = 0.9f; z.attackRecovery = 1f;
                    z.staggerTime = 0f; z.staggerSpeedScale = 1f;
                    z.bodyMultiplier = 0.5f; z.weakPointMultiplier = 3f; z.armored = true;
                    z.firstRound = 10; z.spawnWeight = 0f;
                    z.bodyScale = new Vector3(2f, 1.9f, 2f);
                    z.skinTint = new Color(0.8f, 0.7f, 0.7f); z.clothColor = new Color(0.15f, 0.12f, 0.12f);
                    z.pointsPerHit = 5; z.pointsPerKill = 1000; z.pointsPerHeadshotKill = 1000;
                    break;
            }
            return z;
        }
    }
}
