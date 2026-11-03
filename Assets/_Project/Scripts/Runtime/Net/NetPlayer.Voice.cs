using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.UI;

namespace ZombiesFPS.Net
{
    /// <summary>
    /// Player voice lines: each character reacts to rounds, downs, buys, bosses and milestones, and two players
    /// standing together between rounds trade banter. The owner (or the host, for purchases) picks a line and
    /// tells everyone; each machine plays the character's offline recording and shows a subtitle.
    /// </summary>
    public partial class NetPlayer
    {
        const float VoiceCooldown = 3f;
        const float BanterRange = 6f;
        const float BanterCooldown = 80f;

        // Every machine.
        AudioSource _voice;
        float _voiceBusyUntil;
        float _lastVoiceTime = -100f;
        string _pendingLine;
        float _pendingAt = -1f;

        // Owner.
        LifeState _voiceLastState = LifeState.Alive;
        GamePhase _voiceLastPhase = GamePhase.Intermission;
        int _voiceLastRound;
        bool _saidLowAmmo;
        float _voiceLastPick = -100f;
        bool _saidBoss;
        readonly HashSet<ulong> _voiceDowned = new HashSet<ulong>();
        static float s_NextBanter = 40f;

        void SubscribeVoice()
        {
            RoundManager.BossDefeated += OnVoiceBossDefeated;
            MissionBoard.MissionCompleted += OnVoiceMissionDone;
        }

        void UnsubscribeVoice()
        {
            RoundManager.BossDefeated -= OnVoiceBossDefeated;
            MissionBoard.MissionCompleted -= OnVoiceMissionDone;
        }

        void OnVoiceBossDefeated(long bonus)
        {
            if (IsOwner && IsChosenSpeaker(7)) Say(VoiceEvent.BossDown);
        }

        void OnVoiceMissionDone(int id)
        {
            if (IsOwner && IsChosenSpeaker(id + 3)) Say(VoiceEvent.MissionDone);
        }

        /// <summary>One standing player speaks for the squad on shared events, so four people don't talk at once.</summary>
        bool IsChosenSpeaker(int salt)
        {
            NetPlayer best = null;
            int count = 0;
            foreach (var p in s_All) if (p.IsAlive) count++;
            if (count == 0) return false;
            int pick = Mathf.Abs(salt) % count, i = 0;
            var sorted = new List<NetPlayer>(s_All);
            sorted.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            foreach (var p in sorted)
            {
                if (!p.IsAlive) continue;
                if (i++ == pick)
                {
                    best = p;
                    break;
                }
            }
            return best == this;
        }

        /// <summary>Owner: say a line about an event. Variant -1 picks one at random.</summary>
        void Say(VoiceEvent e, int variant = -1)
        {
            var lines = StoryData.VoiceLines(CharacterId.Value, e);
            if (lines.Length == 0) return;
            if (variant < 0) variant = Random.Range(0, lines.Length);
            SayRpc(e, (byte)Mathf.Clamp(variant, 0, lines.Length - 1));
        }

        /// <summary>Host: say a line for this player (purchases are confirmed on the host).</summary>
        void ServerSay(VoiceEvent e)
        {
            var lines = StoryData.VoiceLines(CharacterId.Value, e);
            if (lines.Length > 0) SayRpc(e, (byte)Random.Range(0, lines.Length));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void SayRpc(VoiceEvent e, byte variant)
        {
            bool urgent = e == VoiceEvent.Downed || e == VoiceEvent.Milestone || e == VoiceEvent.TeammateDown;
            if (!urgent && Time.time - _lastVoiceTime < VoiceCooldown) return;
            var lines = StoryData.VoiceLines(CharacterId.Value, e);
            if (variant >= lines.Length) return;
            Speak(lines[variant]);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void BanterRpc(ulong otherObjectId, byte pair, bool mirror)
        {
            NetPlayer other = null;
            foreach (var p in s_All)
                if (p.NetworkObjectId == otherObjectId) other = p;
            if (other == null) return;
            if (mirror)
            {
                Speak(StoryData.MirrorBanter[0]);
                other.SpeakLater(StoryData.MirrorBanter[1], VoiceLibrary.Duration(Character.name, StoryData.MirrorBanter[0]) + 0.3f);
                return;
            }
            if (pair >= StoryData.BanterPairs.Length) return;
            var b = StoryData.BanterPairs[pair];
            Speak(b.lineA);
            other.SpeakLater(b.lineB, VoiceLibrary.Duration(Character.name, b.lineA) + 0.3f);
        }

        /// <summary>Every machine: plays a line from this player and shows it as a subtitle.</summary>
        void Speak(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _lastVoiceTime = Time.time;
            var c = Character;
            string speaker = IsOwner ? c.name : $"{c.name} ({DisplayName})";
            var recording = VoiceLibrary.Get(c.name, line);
            Subtitles.Show(speaker, line, c.color, duration: recording != null ? recording.length + 0.6f : -1f);
            if (_voice == null)
            {
                _voice = GameSettings.Voice(gameObject.AddComponent<AudioSource>());
                _voice.playOnAwake = false;
                _voice.spatialBlend = IsOwner ? 0f : 1f;
                _voice.minDistance = 3f;
                _voice.maxDistance = 40f;
            }
            _voice.Stop();
            _voice.pitch = 1f;
            if (recording != null)
            {
                _voice.clip = recording;
                _voice.Play();
                _voiceBusyUntil = Time.time + recording.length;
                _nextBlip = float.PositiveInfinity;
                return;
            }
            _voiceBusyUntil = Time.time + Mathf.Clamp(line.Length * 0.045f, 0.5f, 2.4f);
            _nextBlip = 0f;
        }

        void SpeakLater(string line, float delay)
        {
            _pendingLine = line;
            _pendingAt = Time.time + delay;
        }

        float _nextBlip;

        /// <summary>Every machine, every frame: speech blips while talking, and queued banter replies.</summary>
        void VoiceTick()
        {
            if (_pendingAt > 0f && Time.time >= _pendingAt)
            {
                _pendingAt = -1f;
                Speak(_pendingLine);
            }
            if (_voice == null || Time.time >= _voiceBusyUntil || Time.time < _nextBlip) return;
            var c = Character;
            _voice.pitch = c.voicePitch * Random.Range(0.92f, 1.1f);
            _voice.PlayOneShot(ProceduralAudio.RandomBlip(), 0.55f);
            _nextBlip = Time.time + Random.Range(0.09f, 0.15f);
        }

        /// <summary>Owner, every frame: notices the things worth a line.</summary>
        void OwnerVoiceTick()
        {
            var rounds = RoundManager.Instance;
            if (rounds == null || rounds.IsGameOver) return;

            var state = State.Value;
            if (state != _voiceLastState)
            {
                if (state == LifeState.Downed) Say(VoiceEvent.Downed);
                else if (state == LifeState.Alive && _voiceLastState == LifeState.Downed) Say(VoiceEvent.Revived);
                _voiceLastState = state;
            }
            if (!IsAlive) return;

            // A teammate going down: the nearest standing player calls it out.
            foreach (var p in s_All)
            {
                if (p == this) continue;
                bool down = p.State.Value == LifeState.Downed;
                if (down && _voiceDowned.Add(p.OwnerClientId))
                {
                    if (NearestStandingTo(p) == this) Say(VoiceEvent.TeammateDown);
                }
                else if (!down) _voiceDowned.Remove(p.OwnerClientId);
            }

            var phase = rounds.Phase.Value;
            int round = rounds.Round.Value;
            if (phase == GamePhase.RoundActive && (_voiceLastPhase != GamePhase.RoundActive || round != _voiceLastRound))
            {
                _saidBoss = false;
                int milestone = StoryData.MilestoneIndex(round);
                if (milestone >= 0 && IsChosenSpeaker(round)) Say(VoiceEvent.Milestone, milestone);
                else if (rounds.IsBlackout && IsChosenSpeaker(round)) Say(VoiceEvent.Blackout);
                else if (IsChosenSpeaker(round) && Random.value < 0.6f) Say(VoiceEvent.RoundStart);
            }
            else if (phase == GamePhase.Intermission && _voiceLastPhase == GamePhase.RoundActive)
            {
                if (IsChosenSpeaker(round + 1) && Random.value < 0.6f) Say(VoiceEvent.RoundEnd);
            }
            _voiceLastPhase = phase;
            _voiceLastRound = round;

            if (!_saidBoss && phase == GamePhase.RoundActive && Zombies.NetZombie.Bosses.Count > 0)
            {
                _saidBoss = true;
                if (IsChosenSpeaker(round + 2)) Say(VoiceEvent.Boss);
            }

            if (LastPickTime > _voiceLastPick)
            {
                _voiceLastPick = LastPickTime;
                if (Random.value < 0.7f) Say(VoiceEvent.PowerUp);
            }

            if (_gun != null)
            {
                bool low = _gun.Magazine + _gun.Reserve <= _gun.MagazineCapacity && _gun.MagazineCapacity > 0;
                if (low && !_saidLowAmmo)
                {
                    _saidLowAmmo = true;
                    Say(VoiceEvent.LowAmmo);
                }
                else if (!low) _saidLowAmmo = false;
            }

            if (phase == GamePhase.Intermission) TryBanter();
        }

        NetPlayer NearestStandingTo(NetPlayer target)
        {
            NetPlayer best = null;
            float bestDist = float.MaxValue;
            foreach (var p in s_All)
            {
                if (p == target || !p.IsAlive) continue;
                float d = (p.transform.position - target.transform.position).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Owner: between rounds, two players standing together sometimes trade a couple of lines.</summary>
        void TryBanter()
        {
            if (Time.time < s_NextBanter) return;
            foreach (var p in s_All)
            {
                if (p == this || !p.IsAlive || p.OwnerClientId < OwnerClientId) continue;
                if ((p.transform.position - transform.position).sqrMagnitude > BanterRange * BanterRange) continue;
                s_NextBanter = Time.time + BanterCooldown * Random.Range(0.8f, 1.4f);
                if (Random.value > 0.5f) return;
                int a = CharacterId.Value, b = p.CharacterId.Value;
                if (a == b)
                {
                    BanterRpc(p.NetworkObjectId, 0, true);
                    return;
                }
                var options = new List<int>();
                for (int i = 0; i < StoryData.BanterPairs.Length; i++)
                    if (StoryData.BanterPairs[i].a == a && StoryData.BanterPairs[i].b == b) options.Add(i);
                if (options.Count > 0) BanterRpc(p.NetworkObjectId, (byte)options[Random.Range(0, options.Count)], false);
                return;
            }
        }
    }
}
