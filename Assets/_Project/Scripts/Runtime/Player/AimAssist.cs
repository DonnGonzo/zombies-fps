using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Player
{
    /// <summary>
    /// Controller aim assist, local to the player's own view (the host still decides every hit). Three parts,
    /// all scaled by the strength setting and only while the player is on a pad:
    /// slow-down while the crosshair is over a zombie, tracking that follows a zombie's drift across the view
    /// while you move or look, and a short nudge onto the nearest zombie when you start aiming down sights.
    /// It never moves the view on its own when the player isn't touching the sticks.
    /// </summary>
    public static class AimAssist
    {
        /// <summary>How far away zombies are considered, in metres.</summary>
        const float Range = 45f;
        /// <summary>The body's "magnet" radius in metres; bosses are bigger. Aiming down sights widens it.</summary>
        const float BodyRadius = 0.75f, BossRadius = 1.4f, AimWiden = 1.5f;
        const float MinCone = 1.5f, MaxCone = 10f;
        /// <summary>Most slow-down at full strength (0.45 = the stick turns at 55% speed over a target).</summary>
        const float MaxSlowdown = 0.45f;
        const float MaxTracking = 0.7f;
        const float SnapTime = 0.18f, SnapRate = 14f, SnapWiden = 2f;

        static NetZombie s_Target;
        static Vector2 s_LastBearing;
        static float s_SnapUntil = -1f;
        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        /// <summary>Call on the frame the player starts aiming down sights.</summary>
        public static void StartAim() => s_SnapUntil = Time.time + SnapTime;

        /// <summary>
        /// Adjusts this frame's look (degrees: x yaw right, y pitch up) from an eye transform. moving is the left
        /// stick's push (0-1), so tracking only helps while the player is already doing something.
        /// </summary>
        public static Vector2 Apply(Vector2 look, Transform eye, float aimAmount, float moving, float dt)
        {
            if (eye == null || !GameSettings.AimAssist || !GameInput.UsingGamepad || GameSettings.AimAssistStrength <= 0f)
            {
                s_Target = null;
                return look;
            }
            float strength = GameSettings.AimAssistStrength;
            bool snapping = Time.time < s_SnapUntil;
            float widen = Mathf.Lerp(1f, AimWiden, aimAmount) * (snapping ? SnapWiden : 1f);

            var target = FindTarget(eye, widen, out Vector2 offset, out float closeness);
            if (target == null)
            {
                s_Target = null;
                return look;
            }

            // Slow-down: the nearer the crosshair is to the middle of the target, the slower the stick turns.
            look *= 1f - MaxSlowdown * strength * closeness;

            // Tracking: turn with the target as it drifts across the view because it or the player moved.
            Vector2 bearing = Bearing(target.AimPoint - eye.position);
            float input = Mathf.Max(moving, GameInput.LookStickAmount);
            if (target == s_Target && input > 0.1f)
            {
                var drift = new Vector2(Mathf.DeltaAngle(s_LastBearing.x, bearing.x), bearing.y - s_LastBearing.y);
                drift = Vector2.ClampMagnitude(drift, 4f);
                look += drift * MaxTracking * strength * closeness;
            }
            s_Target = target;
            s_LastBearing = bearing;

            // Aiming down sights: a quick pull onto the target, then it's down to the player.
            if (snapping) look += offset * Mathf.Clamp01(SnapRate * dt) * strength;
            return look;
        }

        /// <summary>
        /// The visible zombie nearest the crosshair inside its assist cone: its offset from the crosshair in look
        /// degrees, and closeness, 1 dead centre falling to 0 at the cone's edge.
        /// </summary>
        static NetZombie FindTarget(Transform eye, float widen, out Vector2 offset, out float closeness)
        {
            offset = Vector2.zero;
            closeness = 0f;
            NetZombie best = null;
            float bestRatio = 1f;
            Vector3 origin = eye.position;
            var list = NetZombie.Spawned;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.IsAlive) continue;
                Vector3 to = z.AimPoint - origin;
                float dist = to.magnitude;
                if (dist < 0.5f || dist > Range) continue;
                float radius = (z.Data != null && z.Data.IsBoss ? BossRadius : BodyRadius) * widen;
                float cone = Mathf.Clamp(Mathf.Atan2(radius, dist) * Mathf.Rad2Deg, MinCone, MaxCone * widen);
                Vector2 off = Offset(eye, to);
                float ratio = off.magnitude / cone;
                if (ratio >= bestRatio || !Visible(origin, to, dist, z)) continue;
                bestRatio = ratio;
                best = z;
                offset = off;
            }
            if (best != null) closeness = Closeness(bestRatio);
            return best;
        }

        /// <summary>How strongly assist applies at a share of the cone (0 centre, 1 edge): full in the middle third, easing out to the edge.</summary>
        public static float Closeness(float ratio) => Mathf.Clamp01((1f - ratio) / 0.67f);

        /// <summary>Where a direction sits from the eye's forward, in look degrees (x right, y up).</summary>
        public static Vector2 Offset(Transform eye, Vector3 to)
        {
            Vector3 local = eye.InverseTransformDirection(to);
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            return new Vector2(yaw, pitch);
        }

        /// <summary>A world direction's compass heading and elevation in degrees.</summary>
        static Vector2 Bearing(Vector3 to) =>
            new Vector2(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg);

        /// <summary>Nothing but the zombie itself (or another zombie in front of it) between the eye and its chest.</summary>
        static bool Visible(Vector3 origin, Vector3 to, float dist, NetZombie z)
        {
            int n = Physics.RaycastNonAlloc(origin, to / dist, s_Hits, dist, ZoneMap.ShotMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = s_Hits[i].collider;
                if (c == null) continue;
                if (c.GetComponentInParent<NetZombie>() != null) continue;
                // The player's own body.
                if (c.GetComponentInParent<PlayerLook>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
