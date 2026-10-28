using UnityEngine;

namespace ZombiesFPS.CharacterModels
{
    /// <summary>
    /// Walks, runs and breathes a <see cref="CharacterSkin"/> procedurally, from how fast its object is really moving,
    /// so whatever moves the NPC (a NavMeshAgent, a script, a cutscene) gets matching legs without animation clips.
    /// The angles are turns from the T-pose in the character's own space (see <see cref="CharacterSkin.Pose"/>).
    /// </summary>
    [RequireComponent(typeof(CharacterSkin))]
    public sealed class NpcGait : MonoBehaviour
    {
        /// <summary>Speed (m/s) at which the walk turns into a run, and the top of the run.</summary>
        public float WalkSpeed = 1.6f, RunSpeed = 4.2f;

        CharacterSkin _skin;
        Quaternion[] _own;
        Vector3 _last;
        bool _started;
        float _speed, _phase, _time;

        static readonly Vector3 X = Vector3.right, Y = Vector3.up, Z = Vector3.forward;

        void Start()
        {
            _skin = GetComponent<CharacterSkin>();
            _own = new Quaternion[_skin.Parts.Length];
            _last = transform.position;
            _time = Random.value * 10f;
            var r = _skin.Renderer;
            Debug.Log($"[Characters] {name} is in the game at {transform.position}, scale {transform.lossyScale.y:0.00}, " +
                $"mesh {(r != null && r.sharedMesh != null ? r.sharedMesh.vertexCount : 0)} vertices, drawn {(r != null && r.enabled && r.gameObject.activeInHierarchy)}.");
        }

        void LateUpdate()
        {
            if (_skin == null || _own == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var pos = transform.position;
            var step = pos - _last;
            step.y = 0f;
            _last = pos;
            float measured = _started ? step.magnitude / dt : 0f;
            _started = true;
            // A jump of several metres in one frame is a teleport, not a sprint.
            if (measured > 12f) measured = 0f;
            _speed = Mathf.Lerp(_speed, measured, 1f - Mathf.Exp(-8f * dt));
            _time += dt;

            float run = Mathf.Clamp01(_speed / WalkSpeed) + Mathf.Clamp01((_speed - WalkSpeed) / (RunSpeed - WalkSpeed));
            // Metres per full stride (two steps): longer as he speeds up.
            float stride = Mathf.Lerp(1.3f, 2.6f, Mathf.Clamp01(run - 1f));
            _phase = Mathf.Repeat(_phase + _speed * dt / stride, 1f);
            Pose(_phase, run, _time);
        }

        void Pose(float phase, float run, float t)
        {
            float w = Mathf.Min(run, 1f), r = Mathf.Max(run - 1f, 0f);
            float ph = phase * Mathf.PI * 2f;
            for (int i = 0; i < _own.Length; i++) _own[i] = Quaternion.identity;

            float thigh = 28f * w + 18f * r, knee = 30f * w + 45f * r;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                float s = left ? Mathf.Sin(ph) : -Mathf.Sin(ph);
                float sign = left ? 1f : -1f;
                // Negative about X swings a foot forward; the knee bends most as the leg swings through.
                Set(left ? Part.LUpperLeg : Part.RUpperLeg, Quaternion.AngleAxis(-thigh * s - 8f * r, X));
                float bend = knee * Mathf.Max(0f, Mathf.Sin(ph + (left ? 0f : Mathf.PI) - 1.2f)) + 4f;
                Set(left ? Part.LLowerLeg : Part.RLowerLeg, Quaternion.AngleAxis(bend, X));
                Set(left ? Part.LFoot : Part.RFoot, Quaternion.AngleAxis(6f * s * w, X));
                // Arms hang at the sides and swing against the legs; elbows bend more as he runs.
                Set(left ? Part.LUpperArm : Part.RUpperArm,
                    Quaternion.AngleAxis((22f * w + 20f * r) * s, X) * Quaternion.AngleAxis(78f * sign, Z));
                Set(left ? Part.LLowerArm : Part.RLowerArm, Quaternion.AngleAxis((14f + 8f * w + 55f * r) * sign, Y));
                // Fingers curl a little instead of the T-pose's flat hand.
                var curl = Quaternion.AngleAxis(20f * sign, Z);
                int first = left ? (int)Part.LIndex1 : (int)Part.RIndex1;
                for (int f = 0; f < 12; f++) _own[first + f] = curl;
            }
            float breathe = Mathf.Sin(t * 1.6f);
            float look = Mathf.Sin(t * 0.37f) * 18f * (1f - w);
            Set(Part.Spine, Quaternion.AngleAxis(3f + 4f * w + 9f * r + breathe, X) * Quaternion.AngleAxis(5f * Mathf.Sin(ph) * w, Y));
            Set(Part.UpperChest, Quaternion.AngleAxis(breathe * 1.5f, X));
            Set(Part.Neck, Quaternion.AngleAxis(look * 0.4f, Y) * Quaternion.AngleAxis(-2f * w - 6f * r, X));
            Set(Part.Head, Quaternion.AngleAxis(look * 0.6f, Y));
            float bob = -(0.02f * w + 0.04f * r) * Mathf.Abs(Mathf.Cos(ph)) - 0.03f * r;
            _skin.Pose(_own, new Vector3(0f, bob, 0f));
        }

        void Set(Part p, Quaternion q) => _own[(int)p] = q;
    }
}
