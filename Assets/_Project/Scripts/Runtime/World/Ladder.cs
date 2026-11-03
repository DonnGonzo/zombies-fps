using System.Collections.Generic;
using UnityEngine;

namespace ZombiesFPS.World
{
    /// <summary>
    /// A climbable ladder: a strip of wall from the floor up to a ledge. Players climb it in PlayerMotor by
    /// walking into it; zombies climb it along a NavMesh link the map lays next to it. Plain data with a
    /// registry, since the map is built in code on every machine.
    /// </summary>
    public class Ladder
    {
        const float Reach = 0.75f;
        const float HalfWidth = 0.6f;

        static readonly List<Ladder> s_All = new List<Ladder>();
        public static IReadOnlyList<Ladder> All => s_All;

        /// <summary>Foot of the ladder, on the face it leans against.</summary>
        public readonly Vector3 Base;
        /// <summary>Points away from the wall, towards where a climber stands.</summary>
        public readonly Vector3 Out;
        /// <summary>Height of the ledge at the top, above the base.</summary>
        public readonly float Height;

        public Ladder(Vector3 basePoint, Vector3 outward, float height)
        {
            Base = basePoint;
            Out = new Vector3(outward.x, 0f, outward.z).normalized;
            Height = height;
            s_All.Add(this);
        }

        public float TopY => Base.y + Height;

        /// <summary>Whether a player's feet at this position are in front of the ladder, close enough to hold on.</summary>
        public bool InReach(Vector3 feet)
        {
            Vector3 d = feet - Base;
            float outDist = Vector3.Dot(new Vector3(d.x, 0f, d.z), Out);
            float side = Vector3.Dot(new Vector3(d.x, 0f, d.z), Vector3.Cross(Vector3.up, Out));
            return outDist > -0.1f && outDist < Reach && Mathf.Abs(side) < HalfWidth && d.y > -0.3f && d.y < Height + 0.2f;
        }

        /// <summary>The ladder a player at this position could climb, if any.</summary>
        public static Ladder Near(Vector3 feet)
        {
            foreach (var l in s_All)
                if (l.InReach(feet)) return l;
            return null;
        }

        public static void ClearAll() => s_All.Clear();
    }
}
