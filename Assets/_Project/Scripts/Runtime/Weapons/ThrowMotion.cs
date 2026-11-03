using UnityEngine;
using ZombiesFPS.Data;

namespace ZombiesFPS.Weapons
{
    /// <summary>
    /// The first-person throw, shared by the view and the throw itself: the off hand takes the throwable from the
    /// belt, pulls the pin (grenades), draws back over the shoulder, snaps forward and lets go at <see cref="WindUp"/>
    /// (which is when the throwable leaves the hand and starts flying), follows through across the body and goes
    /// back to the gun. Knives are a quicker flick. Positions are in the camera's space (x right, y up, z forward).
    /// </summary>
    public static class ThrowMotion
    {
        struct Key
        {
            public float Time;
            public Vector3 Position;
            public Vector3 Euler;
            public Key(float time, Vector3 position, Vector3 euler) { Time = time; Position = position; Euler = euler; }
        }

        // Take, cocked over the shoulder, release, follow-through.
        static readonly Key[] Lob =
        {
            new Key(0.10f, new Vector3(-0.09f, -0.13f, 0.32f), new Vector3(-45f, 20f, 0f)),
            new Key(0.78f, new Vector3(-0.24f, 0.09f, 0.13f), new Vector3(-15f, -10f, 20f)),
            new Key(1f, new Vector3(-0.07f, 0.03f, 0.44f), new Vector3(35f, 0f, -5f)),
            new Key(0.14f, new Vector3(0.04f, -0.24f, 0.42f), new Vector3(75f, -15f, -25f)),
        };
        static readonly Key[] Flick =
        {
            new Key(0.05f, new Vector3(-0.1f, -0.1f, 0.3f), new Vector3(-30f, 10f, 0f)),
            new Key(0.75f, new Vector3(-0.2f, 0.07f, 0.16f), new Vector3(-25f, -20f, 35f)),
            new Key(1f, new Vector3(-0.05f, 0.02f, 0.46f), new Vector3(25f, -5f, -10f)),
            new Key(0.1f, new Vector3(0.05f, -0.18f, 0.46f), new Vector3(65f, -20f, -30f)),
        };

        /// <summary>Seconds from the key press to the throwable leaving the hand.</summary>
        public static float WindUp(ThrowableKind kind) => kind == ThrowableKind.Knife ? 0.18f : kind == ThrowableKind.Concussion ? 0.3f : 0.34f;

        /// <summary>Seconds from the release until the hand is back on the gun.</summary>
        static float Recover(ThrowableKind kind) => kind == ThrowableKind.Knife ? 0.32f : 0.44f;

        /// <summary>The whole throw, press to hand back on the gun.</summary>
        public static float Total(ThrowableKind kind) => WindUp(kind) + Recover(kind);

        /// <summary>When the pin comes out (seconds after the press), or a negative number for things with no pin.</summary>
        public static float PinPull(ThrowableKind kind) => kind == ThrowableKind.Grenade ? 0.17f : -1f;

        /// <summary>
        /// Where the throwing hand is <paramref name="t"/> seconds into the throw, and how far it has taken over from
        /// holding the gun (0 = on the gun, 1 = all throw). False outside the throw.
        /// </summary>
        public static bool Sample(ThrowableKind kind, float t, out Vector3 position, out Quaternion rotation, out float weight)
        {
            float windUp = WindUp(kind), total = Total(kind);
            var keys = kind == ThrowableKind.Knife ? Flick : Lob;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            weight = 0f;
            if (t < 0f || t >= total) return false;

            // Absolute times: the take is in seconds, cocked and release are shares of the wind-up, follow-through
            // is seconds after the release.
            float take = keys[0].Time, cocked = keys[1].Time * windUp, release = windUp, follow = windUp + keys[3].Time;
            if (t < take) Pose(keys[0], keys[0], 0f, out position, out rotation);
            else if (t < cocked) Pose(keys[0], keys[1], Smooth((t - take) / (cocked - take)), out position, out rotation);
            else if (t < release)
            {
                // Speeding up into the snap.
                float k = (t - cocked) / (release - cocked);
                Pose(keys[1], keys[2], k * k, out position, out rotation);
            }
            else if (t < follow)
            {
                // Slowing down after it.
                float k = 1f - (t - release) / (follow - release);
                Pose(keys[2], keys[3], 1f - k * k, out position, out rotation);
            }
            else Pose(keys[3], keys[3], 0f, out position, out rotation);

            if (t < take) weight = Smooth(t / take);
            else if (t < follow) weight = 1f;
            else weight = 1f - Smooth((t - follow) / (total - follow));
            return true;
        }

        static void Pose(Key a, Key b, float k, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.LerpUnclamped(a.Position, b.Position, k);
            rotation = Quaternion.Slerp(Quaternion.Euler(a.Euler), Quaternion.Euler(b.Euler), k);
        }

        static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }
    }
}
