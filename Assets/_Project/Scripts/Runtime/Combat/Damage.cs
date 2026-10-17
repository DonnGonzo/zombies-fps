using UnityEngine;

namespace ZombiesFPS.Combat
{
    /// <summary>One hit. Damage is 64-bit because late-round values outgrow int (see design doc, round 500).</summary>
    public readonly struct DamageInfo
    {
        public readonly long Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly bool IsHeadshot;
        /// <summary>Network client id of the player who dealt the damage.</summary>
        public readonly ulong AttackerId;

        public DamageInfo(long amount, Vector3 point, Vector3 direction, bool isHeadshot, ulong attackerId = 0)
        {
            AttackerId = attackerId;
            Amount = amount;
            Point = point;
            Direction = direction;
            IsHeadshot = isHeadshot;
        }
    }

    public readonly struct DamageResult
    {
        public readonly long Dealt;
        public readonly bool Killed;

        public DamageResult(long dealt, bool killed)
        {
            Dealt = dealt;
            Killed = killed;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        DamageResult TakeDamage(in DamageInfo info);
    }
}
