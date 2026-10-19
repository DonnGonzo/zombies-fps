using UnityEngine;
using ZombiesFPS.Net;

namespace ZombiesFPS.Core
{
    /// <summary>
    /// The game's music, on this machine: the menu theme on the main menu, a quiet ambience in a match, and the
    /// easter egg's track while one of its steps is running. Tracks crossfade. The clips (Resources/Audio/Music,
    /// made by tools/music/make_music.py) can be swapped for any other music under the same names.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const float FadeTime = 2.5f;
        const float MenuLevel = 0.6f, AmbienceLevel = 0.32f, EggLevel = 0.85f;

        enum Piece { None, Menu, Ambience, Egg }

        AudioSource _a, _b;
        Piece _playing = Piece.None;
        float _levelA, _levelB, _targetA, _targetB;
        AudioClip _menu, _ambience, _egg;

        void Awake()
        {
            _menu = Resources.Load<AudioClip>("Audio/Music/menu_theme");
            _ambience = Resources.Load<AudioClip>("Audio/Music/ambience");
            _egg = Resources.Load<AudioClip>("Audio/Music/easter_egg");
            _a = MakeSource();
            _b = MakeSource();
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            // Never ducked by anything else.
            s.priority = 0;
            s.ignoreListenerPause = true;
            return s;
        }

        Piece Wanted()
        {
            if (NetPlayer.Local == null) return Piece.Menu;
            var egg = EasterEgg.Instance;
            if (egg != null && egg.Active.Value) return Piece.Egg;
            return Piece.Ambience;
        }

        AudioClip ClipFor(Piece p) => p == Piece.Menu ? _menu : p == Piece.Ambience ? _ambience : p == Piece.Egg ? _egg : null;
        static float LevelFor(Piece p) => p == Piece.Menu ? MenuLevel : p == Piece.Ambience ? AmbienceLevel : p == Piece.Egg ? EggLevel : 0f;

        void Update()
        {
            var want = Wanted();
            if (want != _playing)
            {
                _playing = want;
                // The new piece starts on the quieter source; the other fades out.
                var clip = ClipFor(want);
                bool useA = _levelA <= _levelB;
                var source = useA ? _a : _b;
                if (clip != null)
                {
                    source.clip = clip;
                    source.time = 0f;
                    source.Play();
                }
                if (useA)
                {
                    _targetA = clip != null ? 1f : 0f;
                    _targetB = 0f;
                }
                else
                {
                    _targetB = clip != null ? 1f : 0f;
                    _targetA = 0f;
                }
            }
            float step = Time.unscaledDeltaTime / FadeTime;
            _levelA = Mathf.MoveTowards(_levelA, _targetA, step);
            _levelB = Mathf.MoveTowards(_levelB, _targetB, step);
            _a.volume = _levelA * LevelFor(PieceOf(_a.clip)) * GameSettings.MusicVolume;
            _b.volume = _levelB * LevelFor(PieceOf(_b.clip)) * GameSettings.MusicVolume;
            if (_levelA <= 0f && _targetA <= 0f && _a.isPlaying) _a.Stop();
            if (_levelB <= 0f && _targetB <= 0f && _b.isPlaying) _b.Stop();
        }

        Piece PieceOf(AudioClip clip) => clip == null ? Piece.None : clip == _menu ? Piece.Menu : clip == _ambience ? Piece.Ambience : clip == _egg ? Piece.Egg : Piece.None;
    }
}
