using System;
using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.Player
{
    /// <summary>
    /// Player health with delayed regeneration. In Phase 0 dying ends the run; downs, revives and perks
    /// (lost on going down) arrive with multiplayer in Phase 1+.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public float maxHealth = 100f;
        public float regenDelay = 4f;
        public float regenPerSecond = 30f;

        PlayerMotor _motor;
        PlayerLook _look;
        Transform _pitchPivot;
        AudioSource _audio;
        float _deathTime;
        Vector3 _pivotStart;

        public float Health { get; private set; }
        public bool IsAlive => Health > 0f;
        public float Normalized => Health / maxHealth;
        public float LastDamageTime { get; private set; } = -100f;
        /// <summary>World-space direction from the player toward whatever last hit them.</summary>
        public Vector3 LastDamageDirection { get; private set; }

        public event Action<float> Damaged;
        public event Action Died;

        public void Init(PlayerMotor motor, PlayerLook look, Transform pitchPivot, AudioSource audio)
        {
            _motor = motor;
            _look = look;
            _pitchPivot = pitchPivot;
            _audio = audio;
            _pivotStart = pitchPivot.localPosition;
            Health = maxHealth;
        }

        public void TakeDamage(float amount, Vector3 fromPosition)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            LastDamageTime = Time.time;
            Vector3 dir = fromPosition - transform.position;
            dir.y = 0f;
            LastDamageDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;

            if (_audio != null) _audio.PlayOneShot(ProceduralAudio.PlayerHurt, 0.9f);
            if (_look != null) _look.AddShake(0.7f);
            Damaged?.Invoke(amount);

            if (Health <= 0f) Die();
        }

        void Die()
        {
            _deathTime = Time.time;
            if (_motor != null) _motor.InputLocked = true;
            if (_look != null) _look.InputLocked = true;
            Died?.Invoke();
        }

        void Update()
        {
            if (!IsAlive)
            {
                // Slump to the floor.
                if (_pitchPivot != null)
                {
                    float k = Mathf.Clamp01((Time.time - _deathTime) / 0.8f);
                    k = 1f - (1f - k) * (1f - k);
                    _pitchPivot.localPosition = Vector3.Lerp(_pivotStart, new Vector3(0f, 0.35f, 0f), k);
                    _pitchPivot.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -20f, k), 0f, Mathf.Lerp(0f, 35f, k));
                }
                return;
            }

            if (Health < maxHealth && Time.time - LastDamageTime >= regenDelay)
                Health = Mathf.Min(maxHealth, Health + regenPerSecond * Time.deltaTime);
        }
    }
}
