using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.Player
{
    /// <summary>First-person movement on a CharacterController: walk, sprint, aim-walk, jump.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public float walkSpeed = 4.6f;
        public float sprintSpeed = 7.2f;
        public float aimSpeed = 2.8f;
        public float groundAcceleration = 14f;
        public float airAcceleration = 2.5f;
        public float jumpHeight = 1.1f;
        public float gravity = -22f;
        public float downedSpeed = 1.1f;

        CharacterController _controller;
        Vector3 _velocity;

        public bool InputLocked { get; set; }
        /// <summary>Scales walk, sprint and aim speed (the move speed power-up). Crawling is unaffected.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Downed players crawl: slow, no sprint, no jump.</summary>
        public bool Downed { get; set; }
        public bool IsGrounded => _controller.isGrounded;
        public bool IsSprinting { get; private set; }
        public Vector3 Velocity => _velocity;
        public float PlanarSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        /// <summary>Moves the player instantly (spawn, respawn), bypassing collision.</summary>
        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _velocity = Vector3.zero;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
            bool aiming = !InputLocked && GameInput.AimHeld;
            bool firing = !InputLocked && GameInput.FireHeld;

            IsSprinting = !InputLocked && !Downed && GameInput.SprintHeld && input.y > 0.1f && !aiming && !firing;
            float targetSpeed = Downed ? downedSpeed : (IsSprinting ? sprintSpeed : aiming ? aimSpeed : walkSpeed) * SpeedMultiplier;

            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * targetSpeed;
            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            float accel = _controller.isGrounded ? groundAcceleration : airAcceleration;
            planar = Vector3.Lerp(planar, wish, 1f - Mathf.Exp(-accel * dt));

            float vertical = _velocity.y;
            if (_controller.isGrounded && vertical < 0f) vertical = -2f;
            if (_controller.isGrounded && !InputLocked && !Downed && GameInput.JumpPressed)
                vertical = Mathf.Sqrt(2f * jumpHeight * -gravity);
            vertical += gravity * dt;

            _velocity = new Vector3(planar.x, vertical, planar.z);
            var flags = _controller.Move(_velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;
        }
    }
}
