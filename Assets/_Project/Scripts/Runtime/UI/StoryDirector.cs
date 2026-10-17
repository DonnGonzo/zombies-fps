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
        readonly Queue<(string speaker, string text, Color color, float pitch)> _queue = new Queue<(string, string, Color, float)>();
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
            _traderIndex = Random.Range(0, 8);
            MissionBoard.MissionCompleted += OnMissionCompleted;
            MissionBoard.MissionFailed += OnMissionFailed;
        }

        void OnDestroy()
        {
            MissionBoard.MissionCompleted -= OnMissionCompleted;
            MissionBoard.MissionFailed -= OnMissionFailed;
        }

        int Round => RoundManager.Instance != null ? RoundManager.Instance.DisplayRound : 1;

        void Radio(string text)
        {
            int round = Round;
            _queue.Enqueue((StoryData.RadioSpeaker(round), text, StoryData.RadioSpeakerColor(round), 0.72f));
        }

        static string Pick(string[] lines) => lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];

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
                _queue.Enqueue((StoryData.TraderName, StoryData.TraderLine(Round, _traderIndex++), StoryData.TraderColor, 0.6f));
            }
            _shopWasOpen = open;
        }

        void PlayQueue()
        {
            if (_queue.Count == 0 || Time.time < _nextLineAt) return;
            var (speaker, text, color, pitch) = _queue.Dequeue();
            if (string.IsNullOrEmpty(text)) return;
            bool radio = speaker != StoryData.TraderName;
            float duration = Mathf.Clamp(1.6f + text.Length * 0.055f, 2.5f, 8f);
            Subtitles.Show(speaker, text, color, radio, duration);
            _radio.pitch = 1f;
            if (radio) _radio.PlayOneShot(ProceduralAudio.Radio, 0.6f);
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
