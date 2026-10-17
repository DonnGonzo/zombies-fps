using UnityEngine;

namespace ZombiesFPS.Data
{
    /// <summary>Everything that defines a gun. Balance lives here, not in code.</summary>
    [CreateAssetMenu(menuName = "Zombies FPS/Gun Data", fileName = "Gun_New")]
    public class GunData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Rook-9 Carbine";

        [Header("Damage")]
        [Min(1)] public long damage = 40;
        [Min(1f)] public float headshotMultiplier = 2.5f;
        [Min(1f)] public float range = 120f;

        [Header("Fire")]
        public bool automatic = true;
        [Min(1f)] public float roundsPerMinute = 650f;

        [Header("Ammo")]
        [Min(1)] public int magazineSize = 30;
        [Min(0)] public int startingReserve = 180;
        [Min(0)] public int maxReserve = 240;
        [Min(0.1f)] public float reloadTime = 2.0f;
        [Min(0.1f)] public float emptyReloadTime = 2.5f;

        [Header("Accuracy (degrees)")]
        public float hipSpread = 1.6f;
        public float aimSpread = 0.12f;
        [Tooltip("Extra spread at full walk speed.")]
        public float movingSpread = 1.4f;
        [Tooltip("Spread added per shot, recovers quickly.")]
        public float bloomPerShot = 0.25f;
        public float maxBloom = 1.5f;

        [Header("Recoil (degrees)")]
        public float recoilPitch = 0.9f;
        public float recoilYaw = 0.35f;
        [Range(0f, 1f)] public float aimRecoilScale = 0.6f;

        [Header("Aim down sights")]
        public float aimFov = 52f;
        [Min(0.01f)] public float aimTime = 0.16f;

        [Header("Handling")]
        [Tooltip("Time to raise the gun after switching to it or buying it.")]
        [Min(0.05f)] public float raiseTime = 0.45f;

        [Header("Look (greybox)")]
        [Tooltip("Stretches the first-person model front to back: under 1 for pistols, over 1 for long guns.")]
        [Min(0.3f)] public float modelLength = 1f;
        public Color accent = new Color(0.42f, 0.36f, 0.27f);

        public float SecondsPerShot => 60f / roundsPerMinute;
    }
}
