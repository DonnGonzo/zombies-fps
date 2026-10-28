using System;
using UnityEngine;

namespace ZombiesFPS.Data
{
    public enum ThrowableKind : byte
    {
        Grenade,
        Knife,
        Concussion,
    }

    /// <summary>One kind of throwable: how many you carry, how it flies, and what it does.</summary>
    [Serializable]
    public class ThrowableDef
    {
        public string name;
        public int startCount = 2;
        public int maxCount = 4;
        [Tooltip("Given back at the start of every round, up to the max.")]
        public int perRound = 1;
        public float throwSpeed = 16f;
        [Tooltip("Seconds before it goes off. 0 = on impact (knives).")]
        public float fuse = 2.2f;
        public float radius = 6f;
        [Tooltip("Flat damage, plus a share of the current round's zombie health so it stays useful late.")]
        public long damage = 300;
        public float roundHealthShare = 1f;
        [Tooltip("Damage at the edge of the radius, as a share of the centre.")]
        public float edgeDamage = 0.3f;
        [Header("Concussion")]
        public float stunTime;
        public float slowTime;
        public float slowScale = 1f;

        public long DamageFor(long roundHealth) => Math.Max(1L, damage + (long)Math.Round(roundHealth * (double)roundHealthShare));
    }

    /// <summary>
    /// Melee and throwables: grenades, throwing knives and concussion bangs. Each player carries their own
    /// supply, topped up every round and by a full-ammo reward.
    /// </summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Equipment Data", fileName = "EquipmentData")]
    public class EquipmentData : ScriptableObject
    {
        [Header("Melee")]
        public long meleeDamage = 150;
        [Tooltip("Share of the current round's zombie health added to melee damage.")]
        public float meleeRoundHealthShare = 0.35f;
        public float meleeRange = 2.1f;
        public float meleeRadius = 0.45f;
        public float meleeCooldown = 0.65f;
        [Tooltip("How long a melee hit stops a zombie in its tracks.")]
        public float meleeStagger = 0.6f;
        public float throwCooldown = 0.6f;

        [Header("Throwables")]
        public ThrowableDef grenade = new ThrowableDef
        {
            name = "Frag grenade", startCount = 2, maxCount = 4, perRound = 2, throwSpeed = 16f, fuse = 2.2f, radius = 6f,
            damage = 300, roundHealthShare = 1.2f, edgeDamage = 0.3f,
        };
        public ThrowableDef knife = new ThrowableDef
        {
            name = "Throwing knife", startCount = 3, maxCount = 3, perRound = 3, throwSpeed = 34f, fuse = 0f, radius = 0f,
            damage = 400, roundHealthShare = 1.6f, edgeDamage = 1f,
        };
        public ThrowableDef concussion = new ThrowableDef
        {
            name = "Concussion bang", startCount = 1, maxCount = 2, perRound = 1, throwSpeed = 15f, fuse = 1.4f, radius = 8f,
            damage = 40, roundHealthShare = 0f, edgeDamage = 1f, stunTime = 3f, slowTime = 3f, slowScale = 0.45f,
        };

        public ThrowableDef Def(ThrowableKind kind) => kind == ThrowableKind.Grenade ? grenade : kind == ThrowableKind.Knife ? knife : concussion;

        public long MeleeDamageFor(long roundHealth) => Math.Max(1L, meleeDamage + (long)Math.Round(roundHealth * (double)meleeRoundHealthShare));

        /// <summary>Explosion damage at a distance from the centre: full in the middle, edgeDamage at the edge.</summary>
        public static long Falloff(long centre, float distance, float radius, float edge)
        {
            if (radius <= 0f) return centre;
            float t = Mathf.Clamp01(distance / radius);
            return Math.Max(1L, (long)Math.Round(centre * (double)Mathf.Lerp(1f, edge, t)));
        }
    }
}
