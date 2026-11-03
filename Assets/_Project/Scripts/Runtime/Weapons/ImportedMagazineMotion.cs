using UnityEngine;

namespace ZombiesFPS.Weapons
{
    /// <summary>Magazine insertion sampled from the supplied pistol animation, with its presentation turntable removed.</summary>
    public sealed class ImportedMagazineMotion : ScriptableObject
    {
        public Vector3[] insertion;
        public Vector3 Evaluate(float progress)
        {
            if (progress < 0f || progress >= 1f || insertion == null || insertion.Length < 2) return Vector3.zero;
            if (progress < 0.25f) return Vector3.Lerp(Vector3.zero, insertion[0], Mathf.SmoothStep(0f, 1f, progress / 0.25f));
            float frame = Mathf.Clamp01((progress - 0.35f) / 0.65f) * (insertion.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(frame), insertion.Length - 2);
            return Vector3.Lerp(insertion[index], insertion[index + 1], frame - index);
        }
    }
}
