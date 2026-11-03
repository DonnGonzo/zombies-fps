using UnityEngine;

namespace ZombiesFPS.Combat
{
    /// <summary>Put on any collider that can be shot. Routes the hit to its owner with a zone multiplier.</summary>
    [RequireComponent(typeof(Collider))]
    public class Hitbox : MonoBehaviour
    {
        public bool isHead;
        [Tooltip("Damage multiplier for body zones. Head damage uses the gun's headshot multiplier instead.")]
        public float multiplier = 1f;

        public IDamageable Owner { get; private set; }

        public void Init(IDamageable owner, bool head, float zoneMultiplier = 1f)
        {
            Owner = owner;
            isHead = head;
            multiplier = zoneMultiplier;
        }
    }
}
