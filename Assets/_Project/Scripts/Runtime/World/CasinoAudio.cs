using UnityEngine;
using ZombiesFPS.Core;

namespace ZombiesFPS.World
{
    /// <summary>
    /// The slot machines' sounds, on this machine: a lounge loop around the slot bank while anyone is playing (heard
    /// only in that corner of the Depot), the reels' ratchet on every spin, and once the reels stop a celebration
    /// when the spin paid more than the bet or a "wah wah" when it lost or broke even. Clips live in
    /// Resources/Audio/Casino (see tools/casino); without them the casino is simply quiet.
    /// </summary>
    public static class CasinoAudio
    {
        /// <summary>The music keeps going this long after the last spin anyone made.</summary>
        public const float Linger = 8f;
        /// <summary>Matches the HUD: the last reel stops this long after the spin comes back.</summary>
        public const float RevealDelay = 2f;
        /// <summary>A spin multiplier at or above this plays the jackpot sting (three diamonds or sevens).</summary>
        public const int JackpotMultiplier = 100;
        public const float MinDistance = 1.5f, MaxDistance = 11f;
        const float MusicLevel = 0.6f, StingLevel = 0.9f, SpinLevel = 0.55f, Ducked = 0.3f;
        const float FadeIn = 0.8f, FadeOut = 1.8f;

        static Runner s_Runner;
        static AudioClip s_Music, s_Spin, s_Win, s_Jackpot, s_Lose;
        static bool s_Loaded;
        static AnimationCurve s_Rolloff;

        /// <summary>How loud the slot music is where the listener stands, 0 to 1; the match music ducks under it.</summary>
        public static float Presence { get; private set; }

        static void Load()
        {
            if (s_Loaded) return;
            s_Loaded = true;
            s_Music = Resources.Load<AudioClip>("Audio/Casino/slot_music");
            s_Spin = Resources.Load<AudioClip>("Audio/Casino/slot_spin");
            s_Win = Resources.Load<AudioClip>("Audio/Casino/slot_win");
            s_Jackpot = Resources.Load<AudioClip>("Audio/Casino/slot_jackpot");
            s_Lose = Resources.Load<AudioClip>("Audio/Casino/slot_lose");
        }

        static Runner Get()
        {
            if (s_Runner != null) return s_Runner;
            Load();
            var go = new GameObject("Casino Audio");
            s_Runner = go.AddComponent<Runner>();
            return s_Runner;
        }

        static bool IsSlot(int station) => station >= 0 && station < ZoneMap.BlackjackStation;

        /// <summary>The front of a slot machine, at head height: the stand spot is 1.3 m out from the cabinet.</summary>
        static Vector3 MachineAt(int station) => ZoneMap.CasinoPositions[station] + new Vector3(0f, 1.3f, -1.1f);

        /// <summary>Keeps the slot music playing for at least <paramref name="seconds"/> more.</summary>
        public static void Keep(int station, float seconds)
        {
            if (!IsSlot(station)) return;
            var r = Get();
            r.KeepUntil = Mathf.Max(r.KeepUntil, Time.unscaledTime + seconds);
        }

        /// <summary>A spin at <paramref name="station"/>: the reels ratchet now and the outcome sounds when they stop.</summary>
        public static void Spun(int station, int multiplier)
        {
            if (!IsSlot(station)) return;
            Keep(station, Linger + RevealDelay);
            var r = Get();
            var fx = r.Fx(station);
            if (s_Spin != null) fx.PlayOneShot(s_Spin, SpinLevel * GameSettings.EffectsVolume);
            r.Pending[station] = Time.unscaledTime + RevealDelay;
            r.Outcome[station] = multiplier >= JackpotMultiplier ? s_Jackpot : multiplier > 1 ? s_Win : s_Lose;
        }

        static void Configure(AudioSource s)
        {
            s_Rolloff ??= new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(MinDistance / MaxDistance, 1f),
                new Keyframe(0.45f, 0.35f), new Keyframe(0.75f, 0.08f), new Keyframe(1f, 0f));
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.minDistance = MinDistance;
            s.maxDistance = MaxDistance;
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, s_Rolloff);
            s.dopplerLevel = 0f;
            s.spread = 60f;
        }

        sealed class Runner : MonoBehaviour
        {
            public float KeepUntil;
            public readonly float[] Pending = new float[ZoneMap.BlackjackStation];
            public readonly AudioClip[] Outcome = new AudioClip[ZoneMap.BlackjackStation];
            readonly AudioSource[] _fx = new AudioSource[ZoneMap.BlackjackStation];
            AudioSource _music;
            float _level, _stingUntil;
            Transform _listener;
            float _nextListenerSearch;

            void Awake()
            {
                // One music source for the whole bank, in front of the middle machine: three copies of the same loop a
                // metre apart would phase against each other.
                var m = new GameObject("Slot music");
                m.transform.SetParent(transform, false);
                m.transform.position = MachineAt(1);
                _music = m.AddComponent<AudioSource>();
                Configure(_music);
                _music.loop = true;
                _music.clip = s_Music;
                _music.volume = 0f;
            }

            public AudioSource Fx(int station)
            {
                if (_fx[station] != null) return _fx[station];
                var go = new GameObject("Slot " + station + " fx");
                go.transform.SetParent(transform, false);
                go.transform.position = MachineAt(station);
                var s = go.AddComponent<AudioSource>();
                Configure(s);
                return _fx[station] = s;
            }

            void Update()
            {
                float now = Time.unscaledTime;
                for (int i = 0; i < Pending.Length; i++)
                {
                    if (Pending[i] <= 0f || now < Pending[i]) continue;
                    Pending[i] = 0f;
                    var clip = Outcome[i];
                    if (clip == null) continue;
                    Fx(i).PlayOneShot(clip, StingLevel * GameSettings.EffectsVolume);
                    _stingUntil = Mathf.Max(_stingUntil, now + clip.length);
                }

                bool wanted = now < KeepUntil && _music.clip != null;
                _level = Mathf.MoveTowards(_level, wanted ? 1f : 0f, Time.unscaledDeltaTime / (wanted ? FadeIn : FadeOut));
                // The band drops back under a win or a loss so the sting reads.
                float duck = now < _stingUntil ? Ducked : 1f;
                _music.volume = Mathf.MoveTowards(_music.volume, _level * duck * MusicLevel * GameSettings.MusicVolume, Time.unscaledDeltaTime * 2f);
                if (_level > 0f && !_music.isPlaying) _music.Play();
                else if (_level <= 0f && _music.isPlaying) _music.Stop();

                Presence = _music.isPlaying ? _level * Heard(_music.transform.position) : 0f;
            }

            float Heard(Vector3 at)
            {
                if (_listener == null || !_listener.gameObject.activeInHierarchy)
                {
                    if (Time.unscaledTime < _nextListenerSearch) return 0f;
                    _nextListenerSearch = Time.unscaledTime + 1f;
                    var listener = FindAnyObjectByType<AudioListener>();
                    _listener = listener != null ? listener.transform : null;
                    if (_listener == null) return 0f;
                }
                return s_Rolloff.Evaluate(Vector3.Distance(_listener.position, at) / MaxDistance);
            }

            void OnDestroy()
            {
                if (s_Runner == this) { s_Runner = null; Presence = 0f; }
            }
        }
    }
}
