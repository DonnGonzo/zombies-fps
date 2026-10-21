using System.Collections.Generic;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Net;

namespace ZombiesFPS.UI
{
    /// <summary>
    /// Tells the story on this machine: the Dispatcher's radio calls at story rounds, comments on missions,
    /// bosses and blackouts, the Trader's lore at the spawn shop, and the facility's background hum. Everything
    /// here follows replicated state (the round, the mission board), so every player hears the same story
    /// without any extra network traffic.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        const float LineGap = 0.6f;
        const float TraderCooldown = 25f;
        const float AmbienceVolume = 0.35f;

        NetPlayer _player;
        AudioSource _radio, _ambience;
        readonly Queue<(string speaker, string text, Color color, float pitch, AudioClip voice)> _queue = new Queue<(string, string, Color, float, AudioClip)>();
        float _nextLineAt;
        float _talkUntil, _nextBlip;
        float _talkPitch = 0.7f;

        GamePhase _lastPhase = GamePhase.Intermission;
        int _lastRound;
        int _lastSetRound;
        bool _gameOverSaid;
        bool _shopWasOpen;
        float _nextTrader;
        int _traderIndex;

        /// <summary>The act the squad is in, for the pause menu.</summary>
        public static int CurrentAct { get; private set; }
        /// <summary>The last story call heard, for the pause menu.</summary>
        public static string LastBeat { get; private set; }
        /// <summary>The last few lines this player heard (speaker, line, colour), newest last, for the story screen.</summary>
        public static readonly List<(string speaker, string text, Color color)> Heard = new List<(string, string, Color)>();
        const int HeardKept = 6;
        /// <summary>Raised on this machine when round 50 is cleared and the story's finale plays.</summary>
        public static event System.Action StoryCompleted;

        public void Init(NetPlayer player)
        {
            _player = player;
            _radio = GameSettings.Voice(gameObject.AddComponent<AudioSource>());
            _radio.playOnAwake = false;
            // Not in the effects registry, which would reset it to full volume; Update follows the slider instead.
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = ProceduralAudio.Drone;
            _ambience.loop = true;
            _ambience.volume = GameSettings.EffectsVolume * AmbienceVolume;
            _ambience.Play();
            CurrentAct = 0;
            LastBeat = null;
            Heard.Clear();
            _traderIndex = Random.Range(0, 8);
            MissionBoard.MissionCompleted += OnMissionCompleted;
            MissionBoard.MissionFailed += OnMissionFailed;
            EasterEgg.LineSaid += OnScientistLine;
            MapState.PowerRestored += OnPowerRestored;
        }

        void OnDestroy()
        {
            MissionBoard.MissionCompleted -= OnMissionCompleted;
            MissionBoard.MissionFailed -= OnMissionFailed;
            EasterEgg.LineSaid -= OnScientistLine;
            MapState.PowerRestored -= OnPowerRestored;
        }

        void OnPowerRestored(ulong by)
        {
            foreach (var line in StoryData.PowerOn) Radio(line);
            LastBeat = StoryData.PowerOn[0];
        }

        int Round => RoundManager.Instance != null ? RoundManager.Instance.DisplayRound : 1;

        void Radio(string text)
        {
            int round = Round;
            _queue.Enqueue((StoryData.RadioSpeaker(round), text, StoryData.RadioSpeakerColor(round), 0.72f, null));
        }

        static string Pick(string[] lines) => lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];

        /// <summary>
        /// The easter egg's voices: Doctor Morrow over the intercoms, Brannock, Azhrakel and Ashgrave in the room.
        /// Their recording if there is one, else the placeholder voice.
        /// </summary>
        void OnScientistLine(EggLine line)
        {
            var voice = string.IsNullOrEmpty(line.id) ? null : Resources.Load<AudioClip>(EasterEggData.VoiceFolder(line.speaker) + line.id);
            float pitch = line.speaker == EggSpeaker.God ? 0.4f : line.speaker == EggSpeaker.General ? 0.5f : line.speaker == EggSpeaker.Watchman ? 0.62f : 0.55f;
            _queue.Enqueue((EasterEggData.SpeakerName(line.speaker), line.text, EasterEggData.SpeakerColor(line.speaker), pitch, voice));
        }

        void OnMissionCompleted(int id)
        {
            if (Random.value < 0.6f) Radio(Pick(StoryData.MissionComplete[StoryData.ActFor(Round)]));
        }

        void OnMissionFailed(int id)
        {
            var def = GameConfig.Missions != null ? GameConfig.Missions.At(id) : null;
            if (def != null && def.objective == MissionObjective.ProtectSurvivor) Radio(StoryData.SurvivorLost);
        }

        void Update()
        {
            if (_player == null) return;
            _ambience.volume = GameSettings.EffectsVolume * AmbienceVolume;
            var rounds = RoundManager.Instance;
            if (rounds != null) WatchRounds(rounds);
            WatchShop();
            PlayQueue();
            Blips();
        }

        void WatchRounds(RoundManager rounds)
        {
            if (rounds.IsGameOver)
            {
                if (!_gameOverSaid)
                {
                    _gameOverSaid = true;
                    _queue.Clear();
                    if (rounds.Won.Value)
                        foreach (var line in StoryData.Victory) Radio(line);
                    else Radio(StoryData.GameOver[StoryData.ActFor(rounds.DisplayRound)]);
                }
                return;
            }
            _gameOverSaid = false;

            var phase = rounds.Phase.Value;
            int round = rounds.Round.Value;
            // Round 50 cleared: the story is over. The game carries on, with the Trader on the radio.
            if (phase == GamePhase.Intermission && _lastPhase == GamePhase.RoundActive && round == StoryData.FinalRound)
            {
                foreach (var line in StoryData.Finale) Radio(line);
                LastBeat = StoryData.Finale[StoryData.Finale.Length - 1];
                CurrentAct = StoryData.ActFor(round + 1);
                StoryCompleted?.Invoke();
            }
            if (phase == GamePhase.RoundActive && (round != _lastRound || _lastPhase != GamePhase.RoundActive))
            {
                if (round < _lastRound) _lastSetRound = 0; // A restart.
                CurrentAct = StoryData.ActFor(round);
                var beat = StoryData.BeatFor(round);
                if (beat.HasValue)
                {
                    foreach (var line in beat.Value.lines) Radio(line);
                    LastBeat = beat.Value.lines[beat.Value.lines.Length - 1];
                    Tips.Offer("radio", "That's the Dispatcher on the radio. The story moves on at certain rounds; the pause menu keeps track of it.");
                }
                else if (GameConfig.Round.IsBossRound(round)) Radio(Pick(StoryData.BossRound));
                else if (rounds.IsBlackout) Radio(Pick(StoryData.BlackoutRound));

                var board = MissionBoard.Instance;
                if (board != null && board.SetRound.Value > 0 && board.SetRound.Value != _lastSetRound)
                {
                    // Round 1's call already mentions jobs.
                    if (_lastSetRound != 0 || round > 1) Radio(Pick(StoryData.MissionSet[CurrentAct]));
                    _lastSetRound = board.SetRound.Value;
                }
            }
            _lastPhase = phase;
            _lastRound = round;
        }

        void WatchShop()
        {
            bool open = _player.ShopOpen;
            if (open && !_shopWasOpen && Time.time >= _nextTrader)
            {
                _nextTrader = Time.time + TraderCooldown;
                _queue.Enqueue((StoryData.TraderName, StoryData.TraderLine(Round, _traderIndex++), StoryData.TraderColor, 0.6f, null));
            }
            _shopWasOpen = open;
        }

        void PlayQueue()
        {
            if (_queue.Count == 0 || Time.time < _nextLineAt) return;
            var (speaker, text, color, pitch, voice) = _queue.Dequeue();
            if (string.IsNullOrEmpty(text)) return;
            // The Trader and the easter egg's people in the room talk to you directly; everyone else is on the radio.
            bool radio = speaker != StoryData.TraderName && speaker != EasterEggData.SpeakerName(EggSpeaker.Watchman)
                && speaker != EasterEggData.SpeakerName(EggSpeaker.God) && speaker != EasterEggData.SpeakerName(EggSpeaker.General);
            float duration = Mathf.Clamp(1.6f + text.Length * 0.055f, 2.5f, 8f);
            if (voice != null) duration = Mathf.Max(duration, voice.length + 0.6f);
            Subtitles.Show(speaker, text, color, radio, duration);
            Heard.Add((speaker, text, color));
            if (Heard.Count > HeardKept) Heard.RemoveAt(0);
            _radio.pitch = 1f;
            if (radio) _radio.PlayOneShot(ProceduralAudio.Radio, 0.6f);
            if (voice != null)
            {
                // A real recording: no placeholder syllables over it.
                _radio.PlayOneShot(voice, 1f);
                _talkUntil = 0f;
                _nextLineAt = Time.time + duration + LineGap;
                return;
            }
            _talkPitch = pitch;
            _talkUntil = Time.time + (radio ? 0.3f : 0f) + Mathf.Clamp(text.Length * 0.05f, 0.6f, 4f);
            _nextBlip = Time.time + (radio ? 0.3f : 0f);
            _nextLineAt = Time.time + duration + LineGap;
        }

        /// <summary>Placeholder speech: low, buzzy syllables while a line is on screen.</summary>
        void Blips()
        {
            if (Time.time >= _talkUntil || Time.time < _nextBlip) return;
            _radio.pitch = _talkPitch * Random.Range(0.9f, 1.08f);
            _radio.PlayOneShot(ProceduralAudio.RandomBlip(), 0.5f);
            _nextBlip = Time.time + Random.Range(0.1f, 0.16f);
            if (Time.time + 0.2f >= _talkUntil) _radio.pitch = 1f;
        }
    }
}
