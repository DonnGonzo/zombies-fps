using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;

namespace ZombiesFPS.Player
{
    /// <summary>
    /// First-person movement on a CharacterController: walk, sprint (on a stamina bar), slide out of a sprint, aim-walk,
    /// jump, climb ladders, and climb up onto ledges the head reaches mid-jump.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        public float walkSpeed = 4.6f;
        public float sprintSpeed = 7.2f;
        public float aimSpeed = 2.8f;
        public float groundAcceleration = 14f;
        /// <summary>Braking when the keys are let go or reversed: a little sharper than speeding up, so stops feel planted.</summary>
        public float groundDeceleration = 20f;
        public float airAcceleration = 2.5f;
        /// <summary>Speed bled per second in the air with no keys held (almost none: a jump keeps its run-up).</summary>
        public float airDrag = 0.35f;
        public float jumpHeight = 1.1f;
        /// <summary>A jump still works this long after running off an edge.</summary>
        public float coyoteTime = 0.12f;
        /// <summary>A jump pressed this long before landing goes off on touchdown.</summary>
        public float jumpBuffer = 0.15f;
        /// <summary>Walking off a step or down a slope keeps the feet on the ground if the floor is within this.</summary>
        public float groundSnap = 0.45f;
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
        [Header("Sliding")]
        /// <summary>Speed at the start of a slide, as a multiple of sprint speed.</summary>
        public float slideBoost = 1.2f;
        /// <summary>Speed lost per second on flat ground (downhill slides keep going, uphill ones stop sooner).</summary>
        public float slideFriction = 6.5f;
        /// <summary>The slide ends once it slows to this (m/s).</summary>
        public float slideEndSpeed = 3.4f;
        public float slideMaxTime = 1.1f;
        /// <summary>Pause after a slide before the next one.</summary>
        public float slideCooldown = 0.45f;
        /// <summary>The body's height while sliding, low enough to slip under things.</summary>
        public float slideHeight = 1f;
        /// <summary>Stamina a slide takes from the bar.</summary>
        public float slideStaminaCost = 0.08f;
        /// <summary>Crawling speed when a slide ends somewhere too low to stand up.</summary>
        public float slideCrawlSpeed = 1.8f;

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
        /// <summary>Sliding along the ground out of a sprint (or crawling out from under something after one).</summary>
        public bool IsSliding => _sliding;
        /// <summary>0 standing to 1 fully down in a slide, eased, for the camera dip and the gun's tilt.</summary>
        public float SlideAmount { get; private set; }
        /// <summary>
        /// Metres to add to the eyes' height this frame: eases out the jolt of a stair step (up or down) and dips
        /// on a hard landing. Visual only.
        /// </summary>
        public float CameraOffset => _stepOffset + _landDip;
        /// <summary>Fires on touching down after a real fall, with the downward speed (m/s) at impact.</summary>
        public event System.Action<float> Landed;

        float _lastGroundedTime = -10f, _jumpQueuedAt = -10f;
        bool _wasGrounded, _jumped;
        float _stepOffset, _landDip, _landDipVel;

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

        bool _sliding;
        float _slideSpeed, _slideTime, _slideAir, _nextSlide;
        Vector3 _slideDir;
        float _standHeight;
        AudioSource _slideAudio;

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
            _standHeight = _controller.height;
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
            _stepOffset = _landDip = _landDipVel = 0f;
            _wasGrounded = false;
            _fallTime = _fallSpeed = 0f;
            if (_sliding) EndSlide();
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
            BeginFrame();
            Tick(Time.deltaTime);
            EndFrame(Time.deltaTime);
        }

        void Tick(float dt)
        {
            Vector2 input = InputLocked ? Vector2.zero : GameInput.Move;
            bool aiming = !InputLocked && GameInput.AimHeld;
            bool firing = !InputLocked && GameInput.FireHeld;
            SlideAmount = Mathf.MoveTowards(SlideAmount, _sliding ? 1f : 0f, dt * (_sliding ? 7f : 4f));
            if (_sliding)
            {
                IsSprinting = false;
                UpdateStamina(dt);
                Slide(dt, input);
                return;
            }

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
            if (IsSprinting && _controller.isGrounded && Time.time >= _nextSlide && GameInput.SlidePressed && PlanarSpeed > walkSpeed)
            {
                StartSlide();
                UpdateStamina(dt);
                Slide(dt, input);
                return;
            }
            UpdateStamina(dt);
            float targetSpeed = Downed ? downedSpeed : (IsSprinting ? sprintSpeed : aiming ? aimSpeed : walkSpeed) * SpeedMultiplier;
            if (IsSlowed) targetSpeed *= _slowScale;

            bool grounded = _controller.isGrounded;
            Vector3 wish = (transform.right * input.x + transform.forward * input.y) * targetSpeed;
            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            if (grounded)
            {
                // Speeding up toward where the keys point, braking harder when they're let go or turned against the run.
                bool braking = wish.sqrMagnitude < 0.01f || Vector3.Dot(wish, planar) < 0f || planar.sqrMagnitude > wish.sqrMagnitude + 0.01f;
                planar = Vector3.Lerp(planar, wish, 1f - Mathf.Exp(-(braking ? groundDeceleration : groundAcceleration) * dt));
            }
            else if (wish.sqrMagnitude > 0.01f)
            {
                // Air steering: turns the run-up toward the keys without throwing away speed a slide-jump built up.
                float keep = Mathf.Max(planar.magnitude, targetSpeed);
                if (Vector3.Dot(planar, wish) < 0f) keep = targetSpeed;
                planar = Vector3.Lerp(planar, wish.normalized * Mathf.Min(keep, sprintSpeed * SpeedMultiplier * 1.8f), 1f - Mathf.Exp(-airAcceleration * dt));
            }
            else planar *= Mathf.Exp(-airDrag * dt); // Let go mid-jump: carry on along the arc.

            float vertical = _velocity.y;
            if (grounded && vertical < 0f) vertical = -2f;
            bool canJump = grounded || Time.time - _lastGroundedTime <= coyoteTime;
            if (canJump && !_jumped && !InputLocked && !Downed && JumpQueued)
            {
                ConsumeJump();
                vertical = Mathf.Sqrt(2f * jumpHeight * -gravity);
                _jumped = true;
                _lastGroundedTime = -10f;
            }
            vertical += gravity * dt;

            _velocity = new Vector3(planar.x, vertical, planar.z);
            float yBefore = transform.position.y;
            var flags = _controller.Move(_velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;
            SnapToGround();
            SmoothSteps(transform.position.y - yBefore, planar.magnitude * dt);
            Footsteps(planar.magnitude * dt);
        }

        /// <summary>A jump was pressed within the last <see cref="jumpBuffer"/> seconds and hasn't been used.</summary>
        bool JumpQueued => Time.time - _jumpQueuedAt <= jumpBuffer;

        void ConsumeJump() => _jumpQueuedAt = -10f;

        /// <summary>
        /// Start of every frame: remembers a jump press (so one pressed just before landing still goes off) and when
        /// the feet were last on the ground (so one pressed just after running off an edge does too).
        /// </summary>
        void BeginFrame()
        {
            if (!InputLocked && GameInput.JumpPressed) _jumpQueuedAt = Time.time;
            if (_controller.isGrounded && _velocity.y <= 0f)
            {
                _lastGroundedTime = Time.time;
                _jumped = false;
            }
        }

        /// <summary>
        /// End of every frame: notices a landing (camera dip, a heavier footfall) and eases the camera's step and
        /// landing offsets back to nothing.
        /// </summary>
        void EndFrame(float dt)
        {
            bool grounded = _controller.isGrounded;
            if (grounded && !_wasGrounded && _fallTime > 0.2f && _fallSpeed > 3.5f)
            {
                float impact = _fallSpeed;
                float kick = Mathf.Min(impact * 0.18f, 2.2f) * (GameSettings.ReduceShake ? 0.4f : 1f);
                _landDipVel -= kick;
                LandSound(impact);
                Landed?.Invoke(impact);
            }
            _fallTime = grounded ? 0f : _fallTime + dt;
            _fallSpeed = grounded ? 0f : Mathf.Max(_fallSpeed, -_velocity.y);
            _wasGrounded = grounded;

            _stepOffset *= Mathf.Exp(-14f * dt);
            // A soft spring: drops on the landing, comes back up with a hint of overshoot.
            _landDipVel += (-170f * _landDip - 20f * _landDipVel) * dt;
            _landDip = Mathf.Clamp(_landDip + _landDipVel * dt, -0.25f, 0.08f);
        }

        float _fallTime, _fallSpeed;

        /// <summary>
        /// Walking off a stair tread or over the crest of a slope: if the floor is just below, put the feet back on
        /// it instead of drifting down through the air (which skipped footsteps, loosened control and widened the
        /// gun's spread on every flight of stairs).
        /// </summary>
        void SnapToGround()
        {
            if (_controller.isGrounded || !_wasGrounded || _jumped || _velocity.y > 0f) return;
            float r = _controller.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (r + 0.05f);
            if (!Physics.SphereCast(origin, r, Vector3.down, out var hit, groundSnap + 0.05f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.normal.y < Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad)) return;
            if (hit.collider.GetComponentInParent<NetworkObject>() != null) return; // Not onto a zombie's head.
            _controller.Move(Vector3.down * (hit.distance + 0.02f));
            if (_controller.isGrounded) _velocity.y = -2f;
        }

        /// <summary>
        /// A stair step lifts (or drops) the body in one frame; let the eyes catch up over a few frames instead, so
        /// stairs read as a smooth climb. Slopes rise gradually and are left alone.
        /// </summary>
        void SmoothSteps(float dy, float planarMoved)
        {
            if (!_wasGrounded || !_controller.isGrounded) return;
            if (Mathf.Abs(dy) < 0.05f + planarMoved * 0.6f) return;
            _stepOffset = Mathf.Clamp(_stepOffset - dy, -0.5f, 0.5f);
        }

        void LandSound(float impact)
        {
            var clip = ProceduralAudio.Footstep(Underfoot());
            if (clip == null) return;
            EnsureStepAudio();
            _steps.pitch = Random.Range(0.8f, 0.9f);
            _steps.PlayOneShot(clip, Mathf.Clamp(0.3f + impact * 0.04f, 0.35f, 0.75f));
            _stepDistance = 0f;
        }

        /// <summary>Drops low and shoots forward along the way the player was running, a little faster than the sprint.</summary>
        void StartSlide()
        {
            Vector3 planar = new Vector3(_velocity.x, 0f, _velocity.z);
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            _slideDir = planar.sqrMagnitude > 0.01f ? planar.normalized : fwd.normalized;
            float top = sprintSpeed * SpeedMultiplier * (IsSlowed ? _slowScale : 1f);
            _slideSpeed = Mathf.Max(planar.magnitude, top) * slideBoost;
            _sliding = true;
            _slideTime = _slideAir = 0f;
            IsSprinting = false;
            Stamina = Mathf.Max(0f, Stamina - slideStaminaCost);
            _lastSprintTime = Time.time;
            SetLow(true);
            PlaySlideSound();
        }

        /// <summary>
        /// Carries on along the slide's line, slowing on the flat, speeding down slopes and steering a little toward
        /// where the stick or keys point. Jump springs out of it keeping the speed; somewhere too low to stand, it
        /// becomes a crawl until there's head room.
        /// </summary>
        void Slide(float dt, Vector2 input)
        {
            _slideTime += dt;
            Vector3 pos = transform.position;
            bool grounded = _controller.isGrounded;
            _slideAir = grounded ? 0f : _slideAir + dt;
            bool canStand = RoomToStand(pos);

            // Gravity along the floor speeds the slide downhill and slows it uphill.
            float along = 0f;
            if (grounded && Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out var floor, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                along = Vector3.Dot(Vector3.ProjectOnPlane(Vector3.down, floor.normal), _slideDir) * -gravity;
            _slideSpeed = Mathf.Min(_slideSpeed + (along - slideFriction) * dt, sprintSpeed * SpeedMultiplier * 1.8f);

            Vector3 wish = transform.right * input.x + transform.forward * Mathf.Max(0f, input.y);
            wish.y = 0f;
            if (wish.sqrMagnitude > 0.01f) _slideDir = Vector3.RotateTowards(_slideDir, wish.normalized, 1.2f * dt, 0f);

            float vertical = _velocity.y;
            if (grounded && vertical < 0f) vertical = -2f;
            if (grounded && canStand && !InputLocked && !Downed && JumpQueued)
            {
                // Out of the slide into a jump, keeping its speed.
                ConsumeJump();
                _jumped = true;
                _lastGroundedTime = -10f;
                EndSlide();
                _velocity = _slideDir * Mathf.Max(_slideSpeed, walkSpeed) + Vector3.up * Mathf.Sqrt(2f * jumpHeight * -gravity);
                _controller.Move(_velocity * dt);
                return;
            }
            vertical += gravity * dt;

            bool spent = _slideSpeed <= slideEndSpeed || _slideTime >= slideMaxTime || _slideAir > 0.25f || Downed || InputLocked;
            if (spent && canStand)
            {
                EndSlide();
                _velocity = new Vector3(_slideDir.x * _slideSpeed, vertical, _slideDir.z * _slideSpeed);
                _controller.Move(_velocity * dt);
                return;
            }

            Vector3 planar = spent
                ? (transform.right * input.x + transform.forward * input.y) * slideCrawlSpeed // Stuck low: crawl out.
                : _slideDir * _slideSpeed;
            planar.y = 0f;
            if (spent) _slideSpeed = slideEndSpeed;
            _velocity = new Vector3(planar.x, vertical, planar.z);
            Vector3 before = transform.position;
            var flags = _controller.Move(_velocity * dt);
            if ((flags & CollisionFlags.Above) != 0 && _velocity.y > 0f) _velocity.y = 0f;
            if (!spent && (flags & CollisionFlags.Sides) != 0)
            {
                // Into a wall or a zombie: keep only the speed actually made.
                Vector3 moved = transform.position - before;
                moved.y = 0f;
                _slideSpeed = Mathf.Min(_slideSpeed, moved.magnitude / Mathf.Max(dt, 1e-4f) + 0.5f);
            }
        }

        void EndSlide()
        {
            _sliding = false;
            _nextSlide = Time.time + slideCooldown;
            SetLow(false);
            if (_slideAudio != null && _slideAudio.isPlaying) _slideAudio.Stop();
        }

        /// <summary>Shrinks the body to slide height, or back to standing, keeping the feet where they are.</summary>
        void SetLow(bool low)
        {
            float h = low ? Mathf.Min(slideHeight, _standHeight) : _standHeight;
            _controller.height = h;
            _controller.center = new Vector3(0f, h * 0.5f, 0f);
        }

        void PlaySlideSound()
        {
            var clip = ProceduralAudio.Slide(Underfoot());
            if (clip == null) return;
            if (_slideAudio == null)
            {
                _slideAudio = GameSettings.Effect(gameObject.AddComponent<AudioSource>());
                _slideAudio.playOnAwake = false;
                _slideAudio.spatialBlend = 0f;
            }
            _slideAudio.clip = clip;
            _slideAudio.volume = 0.55f;
            _slideAudio.pitch = Random.Range(0.95f, 1.05f);
            _slideAudio.Play();
        }

        AudioSource _steps;
        float _stepDistance;

        /// <summary>A footstep every stride on the ground, from Ivan's SFX library, sounding like what's underfoot.</summary>
        void Footsteps(float moved)
        {
            if (!_controller.isGrounded || moved <= 0f) return;
            _stepDistance += moved;
            float stride = Downed ? 0.9f : IsSprinting ? 2.3f : 1.7f;
            if (_stepDistance < stride) return;
            _stepDistance = 0f;
            var clip = ProceduralAudio.Footstep(Underfoot());
            if (clip == null) return;
            EnsureStepAudio();
            _steps.pitch = Random.Range(0.9f, 1.1f);
            _steps.PlayOneShot(clip, IsSprinting ? 0.5f : 0.35f);
        }

        void EnsureStepAudio()
        {
            if (_steps != null) return;
            _steps = GameSettings.Effect(gameObject.AddComponent<AudioSource>());
            _steps.playOnAwake = false;
            _steps.spatialBlend = 0f;
        }

        /// <summary>What the player stands on, guessed from the name of the floor's object.</summary>
        string Underfoot() => SurfaceAt(transform.position);

        /// <summary>What the floor under <paramref name="feet"/> is made of (concrete, wood, metal, gravel, dirt), for sounds.</summary>
        public static string SurfaceAt(Vector3 feet)
        {
            if (!Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                return "concrete";
            string n = hit.collider.name.ToLowerInvariant();
            if (n.Contains("metal") || n.Contains("catwalk") || n.Contains("grate") || n.Contains("steel") || n.Contains("iron")) return "metal";
            if (n.Contains("wood") || n.Contains("plank") || n.Contains("deck") || n.Contains("stair") || n.Contains("scaffold")) return "wood";
            if (n.Contains("gravel") || n.Contains("yard") || n.Contains("rubble")) return "gravel";
            if (n.Contains("dirt") || n.Contains("garden") || n.Contains("soil") || n.Contains("grass")) return "dirt";
            return "concrete";
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

        /// <summary>
        /// Is there head room to stand up at these feet? Only the map counts: a zombie or teammate alongside shouldn't
        /// hold the player down in a slide.
        /// </summary>
        bool RoomToStand(Vector3 feet)
        {
            float r = _controller.radius * 0.85f;
            Vector3 bottom = feet + Vector3.up * (r + 0.1f);
            Vector3 top = feet + Vector3.up * (_standHeight - r);
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, r, s_Overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (s_Overlap[i] != _controller && s_Overlap[i].GetComponentInParent<NetworkObject>() == null) return false;
            return true;
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
            if (!InputLocked && JumpQueued)
            {
                ConsumeJump();
                _jumped = true;
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
