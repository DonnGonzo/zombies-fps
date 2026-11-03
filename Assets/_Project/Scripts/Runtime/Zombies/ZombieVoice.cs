using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;

namespace ZombiesFPS.Zombies
{
    /// <summary>
    /// The zombies' voices: recorded CC0 groans, snarls and death cries from Resources/Audio/Zombie (see
    /// tools/zombievox), with the old synthesised groans as the fallback. Every zombie gets a pitch of its own, never
    /// repeats the clip the horde used last, and shares a budget: only a few voices sound at once, so a horde reads
    /// as a few nearby moans over a murmur rather than a wall of the same groan.
    /// </summary>
    public static class ZombieVoice
    {
        public enum Call { Groan, Attack, Death }

        /// <summary>At most this many groans at once, and this many voices of any kind.</summary>
        public const int MaxGroans = 3, MaxVoices = 6;
        /// <summary>Groans start at least this far apart, so two never begin on the same beat.</summary>
        public const float GroanSpacing = 0.6f;
        public const float MinDistance = 2.5f, MaxDistance = 32f;

        static AudioClip[][] s_Pools;
        static readonly int[] s_Last = { -1, -1, -1 };
        static readonly VoiceBudget s_Budget = new VoiceBudget();
        static AnimationCurve s_Rolloff, s_Cutoff;
        static Transform s_Listener;
        static float s_NextListenerSearch;

        static AudioClip[] Pool(Call call)
        {
            if (s_Pools == null || s_Pools[0] == null || s_Pools[0].Length == 0 || s_Pools[0][0] == null)
            {
                var all = Resources.LoadAll<AudioClip>("Audio/Zombie");
                s_Pools = new AudioClip[3][];
                string[] prefixes = { "groan_", "attack_", "death_" };
                for (int p = 0; p < 3; p++)
                {
                    var list = new List<AudioClip>();
                    foreach (var c in all) if (c.name.StartsWith(prefixes[p])) list.Add(c);
                    list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    s_Pools[p] = list.ToArray();
                }
                if (s_Pools[0].Length == 0) s_Pools[0] = new[] { ProceduralAudio.RandomGroan(), ProceduralAudio.RandomGroan() };
            }
            return s_Pools[(int)call];
        }

        /// <summary>How many recorded clips a call has (0 when the recordings are missing).</summary>
        public static int Count(Call call) => Pool(call).Length;

        /// <summary>Sets up a zombie's source: a soft, muffled fall-off with distance and no doppler warble.</summary>
        public static void Configure(AudioSource source, GameObject owner)
        {
            if (s_Rolloff == null)
            {
                // Curves run over distance / MaxDistance. Full volume close up, mostly gone by half way.
                s_Rolloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(MinDistance / MaxDistance, 1f),
                    new Keyframe(0.25f, 0.45f), new Keyframe(0.5f, 0.16f), new Keyframe(1f, 0f));
                // Cutoff falls from open to about 2.5 kHz: far zombies are a muffled murmur through the walls.
                s_Cutoff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.15f, 0.9f), new Keyframe(1f, 0.11f));
            }
            source.spatialBlend = 1f;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, s_Rolloff);
            source.spread = 40f;
            source.dopplerLevel = 0f;
            source.playOnAwake = false;
            var lowPass = owner.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = owner.AddComponent<AudioLowPassFilter>();
            lowPass.customCutoffCurve = s_Cutoff;
        }

        /// <summary>A zombie's own pitch, fixed for its life: big ones lower, quick ones higher.</summary>
        public static float BasePitch(ZombieKind kind, uint seed)
        {
            float kindPitch = kind switch
            {
                ZombieKind.Boss => 0.72f,
                ZombieKind.Brute => 0.82f,
                ZombieKind.Runner => 1.08f,
                ZombieKind.Stalker => 1.04f,
                ZombieKind.Crawler => 0.95f,
                _ => 1f,
            };
            // Spread evenly from the seed rather than Random, so every machine hears the same voice.
            float spread = ((seed * 2654435761u) >> 8) / (float)(1u << 24);
            return kindPitch * Mathf.Lerp(0.9f, 1.08f, spread);
        }

        /// <summary>Seconds until a zombie's next groan: close ones more often, far ones rarely.</summary>
        public static float NextGroanDelay(float listenerDistance, bool fast)
        {
            float near = Mathf.Clamp01(listenerDistance / MaxDistance);
            float baseDelay = Mathf.Lerp(fast ? 3.5f : 5f, 14f, near);
            return baseDelay * Random.Range(0.75f, 1.35f);
        }

        /// <summary>Distance from the listening camera, or infinity when nobody is listening.</summary>
        public static float ListenerDistance(Vector3 position)
        {
            if (s_Listener == null || !s_Listener.gameObject.activeInHierarchy)
            {
                if (Time.unscaledTime < s_NextListenerSearch) return float.PositiveInfinity;
                s_NextListenerSearch = Time.unscaledTime + 1f;
                var listener = Object.FindAnyObjectByType<AudioListener>();
                s_Listener = listener != null ? listener.transform : null;
                if (s_Listener == null) return float.PositiveInfinity;
            }
            return Vector3.Distance(s_Listener.position, position);
        }

        /// <summary>
        /// Voices one call from a zombie if the shared budget allows it; returns whether it sounded. Deaths always get
        /// a slot unless the voice cap is full; groans give way first.
        /// </summary>
        public static bool TryPlay(AudioSource source, Call call, float pitch, float volume, float listenerDistance)
        {
            if (source == null || listenerDistance > MaxDistance) return false;
            var pool = Pool(call);
            if (pool.Length == 0) return false;
            int i = PickIndex(pool.Length, s_Last[(int)call], Random.Range(0, int.MaxValue));
            var clip = pool[i];
            if (clip == null) return false;
            float jittered = pitch * Random.Range(0.97f, 1.03f);
            if (!s_Budget.TryStart(Time.time, clip.length / Mathf.Max(0.1f, jittered), call == Call.Groan)) return false;
            s_Last[(int)call] = i;
            source.pitch = jittered;
            source.PlayOneShot(clip, volume);
            return true;
        }

        /// <summary>A clip index other than the last one played (when there is a choice).</summary>
        public static int PickIndex(int count, int last, int roll)
        {
            if (count <= 1) return 0;
            if (last < 0 || last >= count) return roll % count;
            int i = roll % (count - 1);
            return i >= last ? i + 1 : i;
        }

        /// <summary>Counts the voices sounding now, by when each one ends.</summary>
        public class VoiceBudget
        {
            readonly List<float> _groanEnds = new List<float>();
            readonly List<float> _otherEnds = new List<float>();
            float _lastGroanStart = float.NegativeInfinity;

            public bool TryStart(float now, float duration, bool groan)
            {
                // Time.time restarts at 0 in a new play session: drop anything that ends after a rewind.
                if (now < _lastGroanStart) { _groanEnds.Clear(); _otherEnds.Clear(); _lastGroanStart = float.NegativeInfinity; }
                _groanEnds.RemoveAll(end => end <= now);
                _otherEnds.RemoveAll(end => end <= now);
                if (_groanEnds.Count + _otherEnds.Count >= MaxVoices) return false;
                if (groan)
                {
                    if (_groanEnds.Count >= MaxGroans || now - _lastGroanStart < GroanSpacing) return false;
                    _groanEnds.Add(now + duration);
                    _lastGroanStart = now;
                }
                else _otherEnds.Add(now + duration);
                return true;
            }
        }
    }
}
