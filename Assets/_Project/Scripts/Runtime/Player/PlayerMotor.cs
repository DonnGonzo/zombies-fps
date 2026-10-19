using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Player
{
    /// <summary>
    /// First-person movement on a CharacterController: walk, sprint (on a stamina bar), aim-walk, jump, climb ladders,
    /// and climb up onto ledges the head reaches mid-jump.
    /// </summary>
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
        public float climbSpeed = 3.5f;
        [Header("Stamina")]
        /// <summary>Seconds of sprint on a full bar.</summary>
        public float sprintDuration = 6f;
        /// <summary>Seconds to refill an empty bar once it starts refilling.</summary>
        public float staminaRefillTime = 4f;
        /// <summary>Pause after sprinting stops before the bar refills.</summary>
        public float staminaRefillDelay = 0.8f;
        /// <summary>An emptied bar has to refill this far before sprinting works again, so it doesn't stutter.</summary>
        public float staminaRecoverAt = 0.3f;
        [Header("Climbing over")]
        public float mantleRiseSpeed = 4.2f;
        public float mantleForwardSpeed = 3.2f;

        CharacterController _controller;
        Vector3 _velocity;

        public bool InputLocked { get; set; }
        /// <summary>Scales walk, sprint and aim speed (the move speed power-up). Crawling is unaffected.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        float _slowScale = 1f;
        float _slowUntil;

        /// <summary>Slows the player for a while (a crawler's leg hit). A new slow replaces the old one.</summary>
        public void Slow(float scale, float duration)
        {
            _slowScale = Mathf.Clamp(scale, 0.1f, 1f);
            _slowUntil = Time.time + duration;
        }

        public bool IsSlowed => Time.time < _slowUntil;
        /// <summary>Downed players crawl: slow, no sprint, no jump.</summary>
        public bool Downed { get; set; }
        public bool IsGrounded => _controller.isGrounded;
        public bool IsSprinting { get; private set; }
        /// <summary>Sprint left, 0 to 1.</summary>
        public float Stamina { get; private set; } = 1f;
        /// <summary>Ran the bar dry: no sprinting until it has refilled a little.</summary>
        public bool Exhausted { get; private set; }
        /// <summary>Stretches how long a full bar lasts (Fleet Foot).</summary>
        public float StaminaScale { get; set; } = 1f;
        /// <summary>Hauling up onto a ledge right now.</summary>
        public bool IsMantling => _mantling;
        public Vector3 Velocity => _velocity;
        /// <summary>On a ladder right now.</summary>
        public bool IsClimbing => _ladder != null;

        Ladder _ladder;
        float _noGrabUntil;
        Vector3 _lastSafe;
        bool _haveSafe;

        float _lastSprintTime = -10f;
        float _airTime;

        bool _mantling;
        float _mantleTop, _mantleTime;
        Vector3 _mantleTo, _mantleDir;
        static readonly Collider[] s_Overlap = new Collider[16];

        /// <summary>A ledge has to be at least this far above the feet to climb onto it (lower ones are a step or a hop).</summary>
        const float MantleMinRise = 0.5f;
        /// <summary>How far above the top of the head a ledge can be and still be grabbed (the hands).</summary>
        const float MantleReach = 0.15f;
        /// <summary>How far in from the edge the player ends up standing.</summary>
        const float MantleInset = 0.4f;
        const float MantleTimeout = 1.2f;

        /// <summary>Below this the player has fallen out of the map and is put back where they last stood.</summary>
        const float FallOutHeight = -8f;
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
            _ladder = null;
            _mantling = false;
            _haveSafe = false;
        }

        /// <summary>
        /// Remembers the last place the player stood on the map, and puts them back there if they ever fall out of
        /// it (a backstop: the walls are sealed above, but a gap anywhere would otherwise mean falling forever).
        /// </summary>
        void GuardFallOut()
        {
            Vector3 p = transform.position;
            if (_controller.isGrounded && p.y > -1f && ZoneMap.ZoneOfPoint(p) >= 0)
            {
                _lastSafe = p;
                _haveSafe = true;
            }
            if (p.y >= FallOutHeight) return;
            Teleport((_haveSafe ? _lastSafe : ZoneMap.StartPosition) + Vector3.up * 0.3f);
        }

        void Update()
        {
            GuardFallOut();
            float dt = Time.deltaTime;
            Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
            bool aiming = !InputLocked && GameInput.AimHeld;
            bool firing = !InputLocked && GameInput.FireHeld;

            // Walking into a ladder while facing it grabs it.
            if (_ladder == null && !Downed && !InputLocked && Time.time >= _noGrabUntil && input.y > 0.1f)
            {
                var near = Ladder.Near(transform.position);
                if (near != null && Vector3.Dot(transform.forward, -near.Out) > 0.35f) _ladder = near;
            }
            if (_ladder != null)
            {
                IsSprinting = false;
                UpdateStamina(dt);
                Climb(dt, input);
                return;
            }
            // In the air (a jump, not a stair's flicker), pushing forward into something whose top the head reaches: climb onto it.
            _airTime = _controller.isGrounded ? 0f : _airTime + dt;
            if (!_mantling && _airTime > 0.1f && !Downed && !InputLocked && input.y > 0.1f) TryStartMantle();
            if (_mantling)
            {
                IsSprinting = false;
                UpdateStamina(dt);
                Mantle(dt);
                return;
            }

            IsSprinting = !InputLocked && !Downed && !Exhausted && Stamina > 0f && GameInput.SprintHeld && input.y > 0.1f && !aiming && !firing;
            UpdateStamina(dt);
            float targetSpeed = Downed ? downedSpeed : (IsSprinting ? sprintSpeed : aiming ? aimSpeed : walkSpeed) * SpeedMultiplier;
            if (IsSlowed) targetSpeed *= _slowScale;

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

        /// <summary>Sprinting drains the bar; it refills a moment after you stop. Running it dry locks sprint until it recovers a bit.</summary>
        void UpdateStamina(float dt)
        {
            if (IsSprinting)
            {
                Stamina = Mathf.Max(0f, Stamina - dt / Mathf.Max(0.1f, sprintDuration * StaminaScale));
                _lastSprintTime = Time.time;
                if (Stamina <= 0f)
                {
                    Exhausted = true;
                    IsSprinting = false;
                }
                return;
            }
            if (Time.time - _lastSprintTime < staminaRefillDelay) return;
            Stamina = Mathf.Min(1f, Stamina + dt / Mathf.Max(0.1f, staminaRefillTime));
            if (Exhausted && Stamina >= staminaRecoverAt) Exhausted = false;
        }

        /// <summary>
        /// Looks for a ledge just in front: a flat top no higher than the hands can reach (the head's height plus a
        /// little) and well above the feet, a face under it, and room to stand on it. Starts the climb if there is one.
        /// </summary>
        void TryStartMantle()
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) return;
            fwd.Normalize();
            Vector3 pos = transform.position;
            float radius = _controller.radius, height = _controller.height;
            float reach = pos.y + height + MantleReach;
            float lowest = pos.y + MantleMinRise;

            // The top: straight down just past the face, from as high as the hands reach.
            Vector3 probe = pos + fwd * (radius + MantleInset);
            if (!Physics.Raycast(new Vector3(probe.x, reach, probe.z), Vector3.down, out var top, reach - lowest, ~0, QueryTriggerInteraction.Ignore)) return;
            if (top.normal.y < 0.7f || top.collider.GetComponentInParent<NetworkObject>() != null) return; // Not a zombie's head.
            float ledge = top.point.y;

            // A face to climb: something solid in front just under the lip (not the underside of a deck).
            Vector3 lip = new Vector3(pos.x, ledge - 0.1f, pos.z);
            if (!Physics.Raycast(lip, fwd, radius + MantleInset + 0.1f, ~0, QueryTriggerInteraction.Ignore)) return;

            // Room to haul up beside the face, and room to stand on top.
            if (Blocked(new Vector3(pos.x, ledge, pos.z), radius, height)) return;
            if (Blocked(new Vector3(probe.x, ledge, probe.z), radius, height)) return;

            _mantling = true;
            _mantleTime = 0f;
            _mantleTop = ledge + 0.03f;
            _mantleTo = new Vector3(probe.x, _mantleTop, probe.z);
            _mantleDir = fwd;
            _velocity = Vector3.zero;
        }

        /// <summary>Would a standing player with their feet at this point overlap anything but themselves?</summary>
        bool Blocked(Vector3 feet, float radius, float height)
        {
            float r = radius * 0.9f;
            Vector3 bottom = feet + Vector3.up * (r + 0.06f);
            Vector3 top = feet + Vector3.up * (height - r);
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, r, s_Overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (s_Overlap[i] != _controller) return true;
            return false;
        }

        /// <summary>Straight up the face until the feet clear the lip, then forward onto the top.</summary>
        void Mantle(float dt)
        {
            _mantleTime += dt;
            if (Downed || _mantleTime > MantleTimeout)
            {
                _mantling = false;
                return;
            }
            Vector3 pos = transform.position;
            if (pos.y < _mantleTop)
            {
                // A touch of forward keeps the body against the face so it rolls over the lip.
                float rise = Mathf.Min(mantleRiseSpeed * dt, _mantleTop - pos.y + 0.005f);
                _velocity = Vector3.up * mantleRiseSpeed;
                var flags = _controller.Move(Vector3.up * rise + _mantleDir * (0.2f * dt));
                if ((flags & CollisionFlags.Above) != 0) _mantling = false;
                return;
            }
            Vector3 to = _mantleTo - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.05f || Vector3.Dot(to, _mantleDir) <= 0f)
            {
                _mantling = false;
                _velocity = _mantleDir * walkSpeed * 0.5f;
                return;
            }
            Vector3 step = to / dist * Mathf.Min(mantleForwardSpeed * dt, dist);
            _velocity = to / dist * mantleForwardSpeed;
            _controller.Move(step + Vector3.down * 0.01f);
        }

        /// <summary>Forward climbs (backward while facing away from the ladder), back climbs down, jump lets go.</summary>
        void Climb(float dt, Vector2 input)
        {
            var ladder = _ladder;
            if (Downed || !ladder.InReach(transform.position))
            {
                LetGo(Vector3.zero, 0.3f);
                return;
            }
            if (!InputLocked && GameInput.JumpPressed)
            {
                LetGo(ladder.Out * 3.5f + Vector3.up * 3f, 0.6f);
                return;
            }

            bool facing = Vector3.Dot(transform.forward, -ladder.Out) > -0.2f;
            float climb = input.y * climbSpeed * (facing ? 1f : -1f);
            float height = transform.position.y - ladder.Base.y;
            if (climb > 0f && height >= ladder.Height - 0.05f)
            {
                // Over the top: a hop forward onto the ledge.
                LetGo(-ladder.Out * 3.2f + Vector3.up * 2.5f, 0.6f);
                _controller.Move(_velocity * dt);
                return;
            }
            if (climb < 0f && _controller.isGrounded && height < 0.1f)
            {
                LetGo(Vector3.zero, 0.5f);
                return;
            }

            Vector3 side = Vector3.Cross(Vector3.up, ladder.Out);
            Vector3 move = Vector3.up * climb
                + side * (Vector3.Dot(transform.right * input.x, side) * 1.2f)
                - ladder.Out * 0.6f; // Pressed against the rungs.
            _velocity = move;
            _controller.Move(move * dt);
        }

        void LetGo(Vector3 velocity, float noGrab)
        {
            _ladder = null;
            _velocity = velocity;
            _noGrabUntil = Time.time + noGrab;
        }
    }
}
