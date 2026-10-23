using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ZombiesFPS.World
{
    /// <summary>A contained infected using the supplied idle and alert clips; no root motion or gameplay authority.</summary>
    public sealed class QuarantinedHazmat : MonoBehaviour
    {
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        AnimationClipPlayable _idle, _alert;
        float _idleLength, _alertLength, _clock;

        void OnEnable()
        {
            var idle = Resources.Load<AnimationClip>("ImportedModels/HazmatAnimations/Zombie_Idle");
            var alert = Resources.Load<AnimationClip>("ImportedModels/HazmatAnimations/Zombie_EnemySpotted");
            if (idle == null || alert == null) return;
            var animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            _graph = PlayableGraph.Create("Contained hazmat");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _idle = AnimationClipPlayable.Create(_graph, idle);
            _alert = AnimationClipPlayable.Create(_graph, alert);
            _idleLength = Mathf.Max(0.01f, idle.length); _alertLength = Mathf.Max(0.01f, alert.length);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            _graph.Connect(_idle, 0, _mixer, 0); _graph.Connect(_alert, 0, _mixer, 1);
            AnimationPlayableOutput.Create(_graph, "Pose", animator).SetSourcePlayable(_mixer);
            _graph.Play();
        }
        void Update()
        {
            if (!_graph.IsValid()) return;
            _clock += Time.deltaTime;
            float phase = _clock % 18f;
            float alertTime = phase - 11f;
            float blend = alertTime < 0f ? 0f : Mathf.Clamp01(alertTime * 2f) * Mathf.Clamp01((_alertLength - alertTime) * 2f);
            _idle.SetTime(_clock % _idleLength);
            _alert.SetTime(Mathf.Clamp(alertTime, 0f, _alertLength));
            _mixer.SetInputWeight(0, 1f - blend); _mixer.SetInputWeight(1, blend);
            _graph.Evaluate(0f);
        }
        void OnDisable() { if (_graph.IsValid()) _graph.Destroy(); }
    }
}
