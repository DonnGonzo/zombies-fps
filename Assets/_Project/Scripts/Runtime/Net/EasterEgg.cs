using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ZombiesFPS.Combat;
using ZombiesFPS.Core;
using ZombiesFPS.Data;
using ZombiesFPS.Weapons;
using ZombiesFPS.World;
using ZombiesFPS.Zombies;

namespace ZombiesFPS.Net
{
    /// <summary>Where the final step is. Public so Netcode can generate its serializer.</summary>
    public enum EggPhase : byte
    {
        None,
        /// <summary>Azhrakel has risen and is talking.</summary>
        GodSpeaks,
        /// <summary>Fight or serve: the two stones are lit.</summary>
        Choice,
        FightGod,
        /// <summary>In the Hollow Keep: Ashgrave stands up and talks.</summary>
        GeneralSpeaks,
        FightGeneral,
    }

    /// <summary>
    /// Host-owned easter egg (the steps, the story and the tuning are in EasterEggData).
    ///
    /// 1. Survival: with the whole squad in the Lantern Core, the reactor seals the doors and fifty runners rise.
    /// 2. Ritual: the relics are picked up (squad-wide) and set round the Archives altar; the spark on the altar
    ///    starts three minutes that only count while someone is in the room, with zombies streaming in.
    /// 3. Rescue: Sergeant Brannock appears in the Crane Yard; talking to him starts an ambush of a hundred
    ///    zombies, twelve monsters and a Warden. Zombies are drawn to him; if he goes down the squad tries again.
    /// 4. Boss: the spark in the binding stones wakes the Rotbloom, with zombies on top, until it dies.
    /// 5. Summon: the squad goes down through the stones to the Blood Sanctum (off the map). The spark and heart on
    ///    the blood altar raise Azhrakel; once he has spoken, a player picks Fight (he becomes a boss) or Serve
    ///    (everyone is sent to the Hollow Keep, Ashgrave stands, talks and fights). Winning hands out the
    ///    ending's boon and gun or sword, and the ways back up take everyone home.
    ///
    /// Extra zombies come straight from the pool and never count toward the round. While anyone is off the map
    /// the round stops spawning (HoldsRound), since its zombies couldn't reach them.
    ///
    /// Every machine follows the network variables: the lines, the fields, the props and the music all come from
    /// them changing, so nothing else needs sending except the lightning and fire strikes.
    /// </summary>
    public partial class EasterEgg : NetworkBehaviour
    {
        public static EasterEgg Instance { get; private set; }

        public const long StepReward = 2500;
        const float SpawnMinDistance = 7f;
        const float SpawnFallbackDistance = 4f;
        const float AddsHealthScale = 0.75f;

        /// <summary>Every machine: a step began (step).</summary>
        public static event Action<int> StepStarted;
        /// <summary>Every machine: a step was finished (step).</summary>
        public static event Action<int> StepCompleted;
        /// <summary>Every machine: someone in the easter egg says a line.</summary>
        public static event Action<EggLine> LineSaid;
        /// <summary>Every machine: something for a banner (title, line under it, colour).</summary>
        public static event Action<string, string, Color> Moment;
        /// <summary>Host: where zombies are drawn to besides the players (the Watchman while he's being saved).</summary>
        public static Vector3? Lure { get; private set; }

        /// <summary>Steps finished. The step being played, or next up, has this index.</summary>
        public NetworkVariable<int> StepsDone = new NetworkVariable<int>(0);
        /// <summary>A step is running.</summary>
        public NetworkVariable<bool> Active = new NetworkVariable<bool>(false);
        /// <summary>Kills toward the step (seconds held, for the ritual), out of Total.</summary>
        public NetworkVariable<int> Kills = new NetworkVariable<int>(0);
        public NetworkVariable<int> Total = new NetworkVariable<int>(0);
        /// <summary>The relics: bits 0-3 picked up and carried by the squad, bits 4-7 set on their pedestals.</summary>
        public NetworkVariable<byte> Relics = new NetworkVariable<byte>(0);
        /// <summary>The ritual's clock is stopped: nobody is in the Archives.</summary>
        public NetworkVariable<bool> Paused = new NetworkVariable<bool>(false);
        public NetworkVariable<float> WatchmanHealth = new NetworkVariable<float>(1f);
        public NetworkVariable<EggPhase> Phase = new NetworkVariable<EggPhase>(EggPhase.None);
        /// <summary>How the egg ended: the boon everyone got (None until it's over).</summary>
        public NetworkVariable<EggBoon> Ending = new NetworkVariable<EggBoon>(EggBoon.None);

        Lockdown _lockdown;
        ReactorFx _fx;
        EggProps _props;

        // Server.
        readonly List<NetZombie> _zombies = new List<NetZombie>();
        readonly List<Vector3> _spawnSpots = new List<Vector3>();
        readonly List<ZombieKind> _queue = new List<ZombieKind>();
        readonly List<(Vector3 at, float time)> _strikes = new List<(Vector3, float)>();
        readonly HashSet<NetZombie> _nearWatchman = new HashSet<NetZombie>();
        readonly Collider[] _near = new Collider[48];
        NetZombie _boss;
        EggBoss _bossKind;
        Vector3 _bossSpot;
        bool _bossDue;
        int _toSpawn;
        float _nextSpawn, _introEnds, _elapsed, _phaseEnds, _nextMove;

        // Every machine.
        readonly List<(GameObject mark, float until)> _marks = new List<(GameObject, float)>();
        float _lastWatchmanHealth = 1f, _hurtUntil;

        public EggStep CurrentStep => EasterEggData.At(StepsDone.Value);
        public bool Finished => StepsDone.Value >= EasterEggData.PlayableSteps;
        public int Remaining => Mathf.Max(0, Total.Value - Kills.Value);
        public int RelicsCarried => Count(Relics.Value & 0xF);
        public int RelicsPlaced => Count(Relics.Value >> 4);
        bool RelicTaken(int i) => (Relics.Value & (1 << i)) != 0 || (Relics.Value & (1 << (i + 4))) != 0;
        static int Count(int bits)
        {
            int n = 0;
            for (; bits != 0; bits &= bits - 1) n++;
            return n;
        }

        /// <summary>What the squad is carrying for the egg, for the HUD.</summary>
        public string Carrying
        {
            get
            {
                int done = StepsDone.Value;
                bool active = Active.Value;
                var items = new List<string>();
                if (RelicsCarried > 0) items.Add(RelicsCarried == 1 ? "a relic" : $"{RelicsCarried} relics");
                if (done == 1 && !active) items.Add("the mana spark (empty)");
                else if (done == 2 || (done == 3 && !active)) items.Add("the mana spark (charged)");
                if (done == 4 && Phase.Value == EggPhase.None) items.Add("the charged spark and the Elemental Heart");
                return items.Count == 0 ? null : string.Join(", ", items);
            }
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            var map = GameConfig.Map;
            if (map != null) BuildProps(map);
            Active.OnValueChanged += OnActiveChanged;
            Kills.OnValueChanged += OnKillsChanged;
            StepsDone.OnValueChanged += OnStepsDoneChanged;
            Relics.OnValueChanged += OnRelicsChanged;
            Phase.OnValueChanged += OnPhaseChanged;
            Ending.OnValueChanged += OnEndingChanged;
            Paused.OnValueChanged += OnPausedChanged;
            ShowState();
        }

        /// <summary>The egg's fields, reactor effects and props. Done at spawn, or as soon as the map exists if it didn't yet.</summary>
        void BuildProps(ZoneMap map)
        {
            if (_props != null) return;
            _lockdown = Lockdown.Build(map.Root, EasterEggData.LanternZone);
            _fx = ReactorFx.Build(map.Root, ZoneMap.ReactorBase, map.CoilTops);
            _props = EggProps.Build(map.Root);
            ShowState();
        }

        public override void OnNetworkDespawn()
        {
            Active.OnValueChanged -= OnActiveChanged;
            Kills.OnValueChanged -= OnKillsChanged;
            StepsDone.OnValueChanged -= OnStepsDoneChanged;
            Relics.OnValueChanged -= OnRelicsChanged;
            Phase.OnValueChanged -= OnPhaseChanged;
            Ending.OnValueChanged -= OnEndingChanged;
            Paused.OnValueChanged -= OnPausedChanged;
            if (_lockdown != null) Destroy(_lockdown.gameObject);
            if (_fx != null) Destroy(_fx.gameObject);
            if (_props != null) Destroy(_props.gameObject);
            StopWatchmanRun();
            foreach (var (mark, _) in _marks)
                if (mark != null) Destroy(mark);
            _marks.Clear();
            Lure = null;
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- queries (every machine)

        /// <summary>
        /// Players in a zone, out of everyone who still has to be there (players bled out and waiting for the round
        /// to end don't count). Downed players count where they lie.
        /// </summary>
        public static int PlayersInZone(int zone, out int needed)
        {
            needed = 0;
            int inside = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.State.Value == LifeState.Dead) continue;
                needed++;
                var at = p.transform.position;
                if (ZoneMap.ZoneOfPoint(at) == zone && at.y > -1f && at.y < ZoneMap.WallHeight) inside++;
            }
            return inside;
        }

        /// <summary>Host: the round holds its spawns while anyone is down in the Sanctum or the Keep.</summary>
        public bool HoldsRound
        {
            get
            {
                foreach (var p in NetPlayer.All)
                    if (p != null && p.State.Value != LifeState.Dead && ZoneMap.InArena(p.transform.position)) return true;
                return false;
            }
        }

        /// <summary>
        /// What touching the reactor would do for a player, for the prompt and the host's check: 0 when it starts the
        /// next step, -1 (with the reason in what) when it can't.
        /// </summary>
        public long ReactorPrice(NetPlayer player, out string what)
        {
            what = null;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.IsGameOver) return -1;
            if (StepsDone.Value > 0)
            {
                what = Finished ? "The Lantern burns steadily. Doctor Morrow's work is done" : "The Lantern hums. The next step is somewhere else";
                return -1;
            }
            if (Active.Value)
            {
                what = $"The Lantern is waking. {Remaining} left";
                return -1;
            }
            var step = CurrentStep;
            int inside = PlayersInZone(step.zone, out int needed);
            if (inside < needed)
            {
                what = $"Everyone must be in the {ZoneMap.ZoneNames[step.zone]} to wake the reactor ({inside}/{needed} here)";
                return -1;
            }
            what = "touch the reactor";
            return 0;
        }

        /// <summary>Whether an egg spot has anything to show or say right now (hidden ones don't even get a prompt).</summary>
        public bool SpotShown(int index)
        {
            var s = (EggSpot)index;
            int done = StepsDone.Value;
            bool active = Active.Value;
            var phase = Phase.Value;
            switch (s)
            {
                case EggSpot.Relic0:
                case EggSpot.Relic1:
                case EggSpot.Relic2:
                case EggSpot.Relic3:
                    return done == 1 && !active && !RelicTaken(index);
                case EggSpot.Altar: return done == 1;
                case EggSpot.Watchman: return done == 2 && !active;
                case EggSpot.Stones: return (done == 3 && !active) || done == 4 || done >= 5;
                case EggSpot.BloodAltar: return done == 4 && phase == EggPhase.None;
                case EggSpot.Fight:
                case EggSpot.Serve: return done == 4 && phase == EggPhase.Choice;
                case EggSpot.SanctumExit: return !(active && (phase == EggPhase.GodSpeaks || phase == EggPhase.FightGod));
                case EggSpot.KeepExit: return !(active && (phase == EggPhase.GeneralSpeaks || phase == EggPhase.FightGeneral));
            }
            return false;
        }

        /// <summary>What using an egg spot would do: 0 when it can be used now, -1 (reason in what) when not.</summary>
        public long SpotPrice(NetPlayer player, int index, out string what)
        {
            what = null;
            var rounds = RoundManager.Instance;
            if ((rounds != null && rounds.IsGameOver) || !SpotShown(index)) return -1;
            var s = (EggSpot)index;
            switch (s)
            {
                case EggSpot.Relic0:
                case EggSpot.Relic1:
                case EggSpot.Relic2:
                case EggSpot.Relic3:
                    what = "pick up the " + EasterEggData.RelicNames[index];
                    return 0;
                case EggSpot.Altar:
                    if (Active.Value)
                    {
                        int left = Remaining;
                        what = Paused.Value ? "The ritual has stalled: someone has to be in the Archives" : $"The spark is charging: {left / 60}:{left % 60:00} left";
                        return -1;
                    }
                    if (RelicsCarried > 0)
                    {
                        what = RelicsCarried == 1 ? "set the relic round the altar" : $"set the {RelicsCarried} relics round the altar";
                        return 0;
                    }
                    if (RelicsPlaced < EasterEggData.RelicCount)
                    {
                        what = $"The altar needs its four relics ({RelicsPlaced}/4 set): two in the Chapel, two in the Sunken Temple";
                        return -1;
                    }
                    what = "lay the spark on the altar and start the ritual";
                    return 0;
                case EggSpot.Watchman:
                    what = "talk to the old soldier";
                    return 0;
                case EggSpot.Stones:
                    if (StepsDone.Value == 3)
                    {
                        what = "set the charged spark in the binding stones";
                        return 0;
                    }
                    if (StepsDone.Value >= 5)
                    {
                        what = "go down to the Blood Sanctum";
                        return 0;
                    }
                    if (Active.Value || HoldsRound)
                    {
                        what = "follow the squad down";
                        return 0;
                    }
                    int inside = PlayersInZone(EasterEggData.GreenhouseZone, out int needed);
                    if (inside < needed)
                    {
                        what = $"The stones are bleeding. Everyone must be in the Greenhouse to go down ({inside}/{needed} here)";
                        return -1;
                    }
                    what = "go down through the bleeding stones";
                    return 0;
                case EggSpot.BloodAltar:
                    what = "lay the spark and the Elemental Heart on the blood altar";
                    return 0;
                case EggSpot.Fight:
                    what = "fight Azhrakel";
                    return 0;
                case EggSpot.Serve:
                    what = "serve Azhrakel: hunt down the Betrayer";
                    return 0;
                default:
                    what = "go back up to the Greenhouse";
                    return 0;
            }
        }

        // ---------------------------------------------------------------- server: using things

        /// <summary>Host: a player touched the reactor. Starts step one if everything is in place.</summary>
        public void ServerTouchReactor(NetPlayer player)
        {
            if (!IsServer || ReactorPrice(player, out _) < 0) return;
            StartSurvival(CurrentStep);
        }

        /// <summary>Host: a player used an egg spot.</summary>
        public void ServerUseSpot(NetPlayer player, int index)
        {
            if (!IsServer || SpotPrice(player, index, out _) < 0) return;
            var s = (EggSpot)index;
            switch (s)
            {
                case EggSpot.Relic0:
                case EggSpot.Relic1:
                case EggSpot.Relic2:
                case EggSpot.Relic3:
                    Relics.Value = (byte)(Relics.Value | (1 << index));
                    break;
                case EggSpot.Altar:
                    if (RelicsCarried > 0) Relics.Value = (byte)((Relics.Value & 0xF0) | ((Relics.Value & 0xF) << 4));
                    else StartRitual(CurrentStep);
                    break;
                case EggSpot.Watchman:
                    StartRescue(CurrentStep);
                    break;
                case EggSpot.Stones:
                    if (StepsDone.Value == 3) StartBoss(CurrentStep);
                    else if (StepsDone.Value >= 5) player.ServerTeleport(ZoneMap.SanctumArrival);
                    else if (Active.Value || HoldsRound) player.ServerTeleport(Phase.Value >= EggPhase.GeneralSpeaks ? ZoneMap.KeepArrival : ZoneMap.SanctumArrival);
                    else SendSquad(ZoneMap.SanctumArrival);
                    break;
                case EggSpot.BloodAltar:
                    Kills.Value = 0;
                    Total.Value = 0;
                    Phase.Value = EggPhase.GodSpeaks;
                    _phaseEnds = Time.time + TalkTime(CurrentStep.onStart) + 1f;
                    Active.Value = true;
                    break;
                case EggSpot.Fight:
                    Phase.Value = EggPhase.FightGod;
                    CollectSpawnSpots(ZoneMap.SanctumRect, -1, ZoneMap.BloodAltar);
                    _bossKind = EggBoss.God;
                    _bossSpot = ZoneMap.GodSpot;
                    _bossDue = true;
                    _nextSpawn = Time.time + TalkTime(EasterEggData.ChoseFight);
                    _nextMove = _nextSpawn + EasterEggData.BossMoveEvery(EggBoss.God);
                    break;
                case EggSpot.Serve:
                    Phase.Value = EggPhase.GeneralSpeaks;
                    SendSquad(ZoneMap.KeepArrival);
                    _phaseEnds = Time.time + TalkTime(EasterEggData.ChoseServe) + TalkTime(EasterEggData.GeneralIntro);
                    break;
                case EggSpot.SanctumExit:
                case EggSpot.KeepExit:
                    player.ServerTeleport(ZoneMap.ReturnSpot(SlotOf(player)));
                    break;
            }
        }

        static int SlotOf(NetPlayer player)
        {
            for (int i = 0; i < NetPlayer.All.Count; i++)
                if (NetPlayer.All[i] == player) return i;
            return 0;
        }

        /// <summary>Everyone still in the game, side by side at a place.</summary>
        void SendSquad(Vector3 at)
        {
            int i = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || p.State.Value == LifeState.Dead) continue;
                p.ServerTeleport(at + new Vector3((i % 4 - 1.5f) * 1.6f, 0f, 0f));
                i++;
            }
        }

        /// <summary>How long a set of lines takes to say, for the host's timing (the same sums the subtitles use).</summary>
        static float TalkTime(EggLine[] lines)
        {
            if (lines == null) return 0f;
            float t = 0f;
            foreach (var line in lines)
            {
                float duration = Mathf.Clamp(1.6f + line.text.Length * 0.055f, 2.5f, 8f);
                var voice = string.IsNullOrEmpty(line.id) ? null : Resources.Load<AudioClip>(EasterEggData.VoiceFolder(line.speaker) + line.id);
                if (voice != null) duration = Mathf.Max(duration, voice.length + 0.6f);
                t += duration + 0.6f;
            }
            return t;
        }

        // ---------------------------------------------------------------- server: starting steps

        void StartSurvival(EggStep step)
        {
            CollectSpawnSpots(step.zone);
            Kills.Value = 0;
            Total.Value = step.zombies;
            _toSpawn = step.zombies;
            // Doctor Morrow has his say before the first of them climbs out of the floor.
            _nextSpawn = Time.time + step.introDelay;
            Active.Value = true;
        }

        void StartRitual(EggStep step)
        {
            CollectSpawnSpots(step.zone);
            // Not on or right against the altar's dais: they climb up to the squad, not out of the steps.
            var altar = ZoneMap.ArchivesAltar;
            _spawnSpots.RemoveAll(p => Mathf.Abs(p.x - altar.x) < 7.5f && Mathf.Abs(p.z - altar.z) < 7.5f);
            Kills.Value = 0;
            Total.Value = step.seconds;
            _elapsed = 0f;
            Paused.Value = false;
            _nextSpawn = _introEnds = Time.time + step.introDelay;
            Active.Value = true;
        }

        void StartRescue(EggStep step)
        {
            CollectSpawnSpots(step.zone);
            _spawnSpots.RemoveAll(p => (p - ZoneMap.WatchmanSpot).sqrMagnitude < 12f * 12f);
            _queue.Clear();
            for (int i = 0; i < step.zombies; i++) _queue.Add(i % 5 < 3 ? ZombieKind.Walker : ZombieKind.Runner);
            // Monsters through the middle and back of the wave, then the Warden last.
            for (int i = 0; i < EasterEggData.AmbushMonsters.Length; i++)
                _queue.Insert(Mathf.Min(_queue.Count, 25 + i * 7), EasterEggData.AmbushMonsters[i]);
            _queue.Add(ZombieKind.Boss);
            Kills.Value = 0;
            Total.Value = _queue.Count;
            WatchmanHealth.Value = 1f;
            _nextSpawn = _introEnds = Time.time + step.introDelay;
            Active.Value = true;
        }

        void StartBoss(EggStep step)
        {
            CollectSpawnSpots(step.zone);
            _spawnSpots.RemoveAll(p => (p - ZoneMap.StonesCenter).sqrMagnitude < 5f * 5f);
            Kills.Value = 0;
            Total.Value = 0;
            _bossKind = EggBoss.Rotbloom;
            _bossSpot = ZoneMap.StonesCenter + new Vector3(0f, 0f, -4.6f);
            _bossDue = true;
            _nextSpawn = Time.time + step.introDelay;
            _nextMove = _nextSpawn + EasterEggData.BossMoveEvery(EggBoss.Rotbloom);
            Active.Value = true;
        }

        // ---------------------------------------------------------------- server: running steps

        void Update()
        {
            if (IsServer) ServerTick();
            ClientTick();
        }

        void ServerTick()
        {
            Lure = Active.Value && StepsDone.Value == 2 && Time.time >= _introEnds ? WatchmanPosition : (Vector3?)null;
            if (!Active.Value) return;
            var rounds = RoundManager.Instance;
            if (rounds != null && rounds.IsGameOver) return;
            _zombies.RemoveAll(z => z == null || !z.IsSpawned);
            if (_boss != null && !_boss.IsSpawned) _boss = null;
            int players = NetPlayer.All.Count;
            var step = CurrentStep;
            if (step == null) return;
            switch (step.kind)
            {
                case EggStepKind.Survival:
                    if (_toSpawn > 0 && Time.time >= _nextSpawn && _zombies.Count < EasterEggData.AliveCap(players))
                        _nextSpawn = Time.time + (TrySpawn(step.zombieKind, EasterEggData.HealthScale, ZombieSpeedTier.Jog) ? EasterEggData.SpawnInterval : 0.25f);
                    break;

                case EggStepKind.Ritual:
                    RitualTick(step, players);
                    break;

                case EggStepKind.Rescue:
                    if (_queue.Count > 0 && Time.time >= _nextSpawn && _zombies.Count < EasterEggData.AmbushAliveCap(players))
                    {
                        var kind = _queue[0];
                        if (TrySpawn(kind, kind == ZombieKind.Boss ? 1f : 0.8f, ZombieSpeedTier.Jog))
                        {
                            _queue.RemoveAt(0);
                            _nextSpawn = Time.time + EasterEggData.AmbushSpawnInterval;
                        }
                        else _nextSpawn = Time.time + 0.25f;
                    }
                    WatchmanRunTick();
                    WatchmanTick();
                    break;

                case EggStepKind.Boss:
                    BossFightTick(players);
                    break;

                case EggStepKind.Summon:
                    SummonTick(players);
                    break;
            }
            StrikesTick();
        }

        void RitualTick(EggStep step, int players)
        {
            bool inside = PlayersInZone(step.zone, out _) > 0;
            if (Paused.Value == inside) Paused.Value = !inside;
            if (Time.time < _introEnds) return;
            if (inside)
            {
                _elapsed += Time.deltaTime;
                int held = Mathf.Min(Total.Value, Mathf.FloorToInt(_elapsed));
                if (held != Kills.Value) Kills.Value = held;
                if (_elapsed >= Total.Value)
                {
                    CompleteStep();
                    return;
                }
            }
            if (Time.time >= _nextSpawn && _zombies.Count < EasterEggData.RitualAliveCap(players))
                _nextSpawn = Time.time + (TrySpawn(RitualKind(), EasterEggData.RitualHealthScale, ZombieSpeedTier.Walk) ? EasterEggData.RitualSpawnInterval : 0.25f);
        }

        /// <summary>Mostly walkers, some runners, the odd crawler once the round has them.</summary>
        static ZombieKind RitualKind()
        {
            float r = UnityEngine.Random.value;
            return r < 0.25f ? ZombieKind.Runner : r < 0.32f ? ZombieKind.Crawler : ZombieKind.Walker;
        }

        /// <summary>Host: zombies in reach of the Watchman wear him down. At zero he's down and the ambush is called off.</summary>
        void WatchmanTick()
        {
            if (Time.time < _introEnds) return;
            var at = WatchmanPosition;
            _nearWatchman.Clear();
            int n = Physics.OverlapSphereNonAlloc(at + Vector3.up, EasterEggData.WatchmanReach, _near, ~0, QueryTriggerInteraction.Ignore);
            float dps = 0f;
            for (int c = 0; c < n; c++)
            {
                var hb = _near[c].GetComponent<Hitbox>();
                if (hb == null || !(hb.Owner is NetZombie z) || !z.IsAlive || !_nearWatchman.Add(z)) continue;
                var zd = z.Data;
                dps += zd.damage / Mathf.Max(0.5f, zd.attackWindup + zd.attackRecovery);
            }
            if (dps <= 0f)
            {
                WatchmanRecover();
                return;
            }
            _watchHitAt = Time.time;
            WatchmanHealth.Value = Mathf.Max(0f, WatchmanHealth.Value - dps * Time.deltaTime / EasterEggData.WatchmanHealth);
            if (WatchmanHealth.Value <= 0f) FailStep();
        }

        void BossFightTick(int players)
        {
            if (Time.time < _nextSpawn) return;
            if (_bossDue)
            {
                if (TrySpawnBoss()) _bossDue = false;
                return;
            }
            Adds(players);
            BossMoves();
        }

        void SummonTick(int players)
        {
            switch (Phase.Value)
            {
                case EggPhase.GodSpeaks:
                    if (Time.time >= _phaseEnds) Phase.Value = EggPhase.Choice;
                    break;
                case EggPhase.FightGod:
                case EggPhase.FightGeneral:
                    BossFightTick(players);
                    break;
                case EggPhase.GeneralSpeaks:
                    if (Time.time >= _phaseEnds)
                    {
                        CollectSpawnSpots(ZoneMap.KeepRect, -1, ZoneMap.ThroneSpot);
                        _bossKind = EggBoss.General;
                        _bossSpot = ZoneMap.ThroneSpot + new Vector3(0f, -0.6f, -3f);
                        _bossDue = true;
                        _nextSpawn = Time.time;
                        _nextMove = Time.time + EasterEggData.BossMoveEvery(EggBoss.General);
                        Phase.Value = EggPhase.FightGeneral;
                    }
                    break;
            }
        }

        /// <summary>Zombies keep coming while a boss is up.</summary>
        void Adds(int players)
        {
            if (Time.time < _nextSpawn || _zombies.Count >= EasterEggData.BossAddsCap(players)) return;
            var kind = UnityEngine.Random.value < 0.35f ? ZombieKind.Runner : ZombieKind.Walker;
            _nextSpawn = Time.time + (TrySpawn(kind, AddsHealthScale, ZombieSpeedTier.Jog) ? EasterEggData.BossAddsInterval : 0.25f);
        }

        /// <summary>Each boss's special move, every so often: seed pods, fire rain, or the fallen Watch.</summary>
        void BossMoves()
        {
            if (_boss == null || !_boss.IsAlive || Time.time < _nextMove) return;
            _nextMove = Time.time + EasterEggData.BossMoveEvery(_bossKind);
            var at = _boss.transform.position;
            switch (_bossKind)
            {
                case EggBoss.Rotbloom:
                    // Seed pods: crawlers burst out of the soil round it.
                    for (int i = 0; i < 3; i++) SpawnNear(at, ZombieKind.Crawler, AddsHealthScale);
                    BurstRpc(at, (byte)EggBoss.Rotbloom);
                    break;
                case EggBoss.God:
                    // Fire rain: a mark under every player still standing; move off it before it lands.
                    foreach (var p in NetPlayer.All)
                    {
                        if (p == null || !p.IsAlive || !ZoneMap.InArena(p.transform.position)) continue;
                        var spot = p.transform.position;
                        _strikes.Add((spot, Time.time + EasterEggData.StrikeWarning));
                        MarkRpc(spot);
                    }
                    break;
                case EggBoss.General:
                    // The fallen Watch answer their general.
                    for (int i = 0; i < 4; i++) SpawnNear(at, ZombieKind.Runner, AddsHealthScale);
                    BurstRpc(at, (byte)EggBoss.General);
                    break;
            }
        }

        void StrikesTick()
        {
            for (int i = _strikes.Count - 1; i >= 0; i--)
            {
                var (at, time) = _strikes[i];
                if (Time.time < time) continue;
                _strikes.RemoveAt(i);
                FireRpc(at);
                foreach (var p in NetPlayer.All)
                {
                    if (p == null || !p.IsAlive) continue;
                    var d = p.transform.position - at;
                    d.y = 0f;
                    if (d.sqrMagnitude < EasterEggData.StrikeRadius * EasterEggData.StrikeRadius)
                        p.ServerApplyDamage(EasterEggData.StrikeDamage, at);
                }
            }
        }

        // ---------------------------------------------------------------- server: spawning

        /// <summary>Open floor in a room, on a grid, for zombies to rise from.</summary>
        void CollectSpawnSpots(int zone)
        {
            _spawnSpots.Clear();
            foreach (var r in ZoneMap.ZoneRects(zone)) AddSpots(r, zone);
        }

        /// <summary>The same, for one of the rooms off the map, keeping clear of a centrepiece.</summary>
        void CollectSpawnSpots(RectInt rect, int zone, Vector3 avoid)
        {
            _spawnSpots.Clear();
            AddSpots(rect, zone);
            _spawnSpots.RemoveAll(p => (p - avoid).sqrMagnitude < 5f * 5f);
        }

        void AddSpots(RectInt r, int zone)
        {
            for (float x = r.xMin + 1.5f; x < r.xMax - 1f; x += 2.5f)
                for (float z = r.yMin + 1.5f; z < r.yMax - 1f; z += 2.5f)
                {
                    var p = new Vector3(x, 0.2f, z);
                    if (!NavMesh.SamplePosition(p, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                    // Floor level only: not on top of the furniture.
                    if (hit.position.y > 0.4f || ZoneMap.ZoneOfPoint(hit.position) != zone) continue;
                    _spawnSpots.Add(hit.position);
                }
        }

        long RoundHealth(double scale)
        {
            var rounds = RoundManager.Instance;
            int round = rounds != null ? rounds.DisplayRound : 1;
            return Math.Max(1L, (long)Math.Round(GameConfig.Round.HealthForRound(round) * scale));
        }

        static ZombieSpeedTier TierAtLeast(ZombieSpeedTier least)
        {
            var rounds = RoundManager.Instance;
            var tier = GameConfig.Round.SpeedTierForRound(rounds != null ? rounds.DisplayRound : 1);
            return tier < least ? least : tier;
        }

        bool TrySpawn(ZombieKind kind, float healthScale, ZombieSpeedTier least)
        {
            if (_spawnSpots.Count == 0 || ZombiePool.Instance == null) return false;
            var at = PickSpot(SpawnMinDistance) ?? PickSpot(SpawnFallbackDistance);
            if (!at.HasValue) return false;
            Spawn(at.Value, kind, healthScale, least, EggBoss.None);
            StrikeRpc(at.Value);
            return true;
        }

        void SpawnNear(Vector3 at, ZombieKind kind, float healthScale)
        {
            float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            var p = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * UnityEngine.Random.Range(2f, 4f);
            if (!NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) return;
            Spawn(hit.position, kind, healthScale, ZombieSpeedTier.Jog, EggBoss.None);
        }

        NetZombie Spawn(Vector3 at, ZombieKind kind, float healthScale, ZombieSpeedTier least, EggBoss special)
        {
            var face = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            var zombie = ZombiePool.Instance.Get(at, face);
            var data = GameConfig.ZombieType(kind);
            double scale = healthScale;
            int extra = Mathf.Max(0, NetPlayer.All.Count - 1);
            if (data.healthPerExtraPlayer > 0f) scale *= 1.0 + data.healthPerExtraPlayer * extra;
            zombie.ServerSetup(kind, RoundHealth(scale), TierAtLeast(least), special);
            zombie.Died += OnZombieDied;
            zombie.Recycled += OnZombieRecycled;
            zombie.NetworkObject.Spawn(true);
            if (special == EggBoss.None) _zombies.Add(zombie);
            return zombie;
        }

        bool TrySpawnBoss()
        {
            if (ZombiePool.Instance == null) return false;
            if (!NavMesh.SamplePosition(_bossSpot, out var hit, 3f, NavMesh.AllAreas)) return false;
            _boss = Spawn(hit.position, ZombieKind.Boss, EasterEggData.BossHealth(_bossKind), ZombieSpeedTier.Jog, _bossKind);
            BurstRpc(hit.position, (byte)_bossKind);
            return true;
        }

        Vector3? PickSpot(float minDistance)
        {
            int start = UnityEngine.Random.Range(0, _spawnSpots.Count);
            for (int i = 0; i < _spawnSpots.Count; i++)
            {
                var p = _spawnSpots[(start + i) % _spawnSpots.Count];
                bool clear = true;
                foreach (var player in NetPlayer.All)
                    if ((player.transform.position - p).sqrMagnitude < minDistance * minDistance)
                    {
                        clear = false;
                        break;
                    }
                if (clear) return p;
            }
            return null;
        }

        void OnZombieDied(NetZombie zombie, DamageInfo info)
        {
            _zombies.Remove(zombie);
            if (!Active.Value) return;
            if (zombie == _boss)
            {
                _boss = null;
                OnBossDied();
                return;
            }
            var step = CurrentStep;
            // Only the counted steps count kills (the ritual's Kills is its clock).
            if (step == null || (step.kind != EggStepKind.Survival && step.kind != EggStepKind.Rescue)) return;
            Kills.Value++;
            if (Kills.Value >= Total.Value) CompleteStep();
        }

        /// <summary>A stuck one comes back somewhere else in the room (a stuck boss where it first rose).</summary>
        void OnZombieRecycled(NetZombie zombie)
        {
            _zombies.Remove(zombie);
            if (!Active.Value) return;
            if (zombie == _boss)
            {
                _boss = null;
                _bossDue = true;
                return;
            }
            var step = CurrentStep;
            if (step == null) return;
            if (step.kind == EggStepKind.Survival) _toSpawn++;
            else if (step.kind == EggStepKind.Rescue) _queue.Insert(0, zombie.Kind);
        }

        void OnBossDied()
        {
            var step = CurrentStep;
            if (step == null) return;
            if (step.kind == EggStepKind.Boss)
            {
                CompleteStep();
                return;
            }
            if (step.kind != EggStepKind.Summon) return;
            // The ending: everyone in the game gets its boon, and the god's brand or Ashgrave's sword.
            bool serve = Phase.Value == EggPhase.FightGeneral;
            int godGun = GameConfig.Guns != null ? GameConfig.Guns.Length - 1 : -1;
            foreach (var p in NetPlayer.All)
                if (p != null) p.ServerGrantEggReward(serve ? EggBoon.EmberOath : EggBoon.Godblood, serve, serve ? -1 : godGun);
            Ending.Value = serve ? EggBoon.EmberOath : EggBoon.Godblood;
            CompleteStep();
        }

        void ClearZombies()
        {
            foreach (var z in _zombies.ToArray())
                if (z != null && z.IsSpawned) z.ServerRemove();
            _zombies.Clear();
            if (_boss != null && _boss.IsSpawned) _boss.ServerRemove();
            _boss = null;
            _bossDue = false;
            _queue.Clear();
            _strikes.Clear();
            _toSpawn = 0;
        }

        void CompleteStep()
        {
            foreach (var p in NetPlayer.All) p.Points.Value += StepReward;
            // Whatever is left of the step's zombies drops where it stands.
            foreach (var z in _zombies)
                if (z != null && z.IsSpawned) BurstRpc(z.transform.position, 0);
            ClearZombies();
            StopWatchmanRun();
            Paused.Value = false;
            Phase.Value = EggPhase.None;
            Active.Value = false;
            StepsDone.Value++;
        }

        /// <summary>The Watchman went down: the ambush is called off, and he'll talk to the squad again when they're ready.</summary>
        void FailStep()
        {
            ClearZombies();
            StopWatchmanRun();
            Active.Value = false;
            WatchmanHealth.Value = 1f;
            FailedRpc();
        }

        /// <summary>Host: a restart puts the egg back to the start.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            ClearZombies();
            StopWatchmanRun();
            Active.Value = false;
            Kills.Value = 0;
            Total.Value = 0;
            Relics.Value = 0;
            Paused.Value = false;
            WatchmanHealth.Value = 1f;
            Phase.Value = EggPhase.None;
            Ending.Value = EggBoon.None;
            StepsDone.Value = 0;
            Lure = null;
        }

        /// <summary>Host only (test console): finishes the step being played, or skips ahead one step. The final step ends on serve or fight.</summary>
        public void DebugCompleteStep(bool serve = false)
        {
            if (!IsServer || Finished) return;
            var step = CurrentStep;
            if (Active.Value && (step.kind == EggStepKind.Survival || step.kind == EggStepKind.Rescue)) Kills.Value = Total.Value;
            if (step.kind == EggStepKind.Ritual) Relics.Value = 0xF0;
            if (step.kind == EggStepKind.Summon)
            {
                ClearZombies();
                Phase.Value = serve ? EggPhase.FightGeneral : EggPhase.FightGod;
                Active.Value = true;
                OnBossDied();
                return;
            }
            CompleteStep();
        }

        /// <summary>Host only (test console): every relic picked up.</summary>
        public void DebugAllRelics()
        {
            if (!IsServer || StepsDone.Value != 1) return;
            Relics.Value = (byte)(Relics.Value | (~(Relics.Value >> 4) & 0xF));
        }

        // ---------------------------------------------------------------- effects (every machine)

        /// <summary>Lightning from the reactor (step one) or a flash of the step's colour where a zombie climbs out.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void StrikeRpc(Vector3 at)
        {
            if (StepsDone.Value == 0)
            {
                if (_fx != null) _fx.Strike(at);
                return;
            }
            var fx = FxSystem.Instance;
            if (fx == null) return;
            var color = StepsDone.Value == 1 ? ZoneMap.ManaColor : StepsDone.Value >= 4 ? ZoneMap.BloodGlow : new Color(0.6f, 0.5f, 0.35f);
            fx.Flash(at + Vector3.up * 0.5f, color, 6f, 0.2f);
            fx.Impact(at + Vector3.up * 0.1f, Vector3.up, color * 0.7f, 10, 3f, 0.05f);
        }

        /// <summary>A boss rising, a burst of seed pods or the fallen Watch, or a leftover zombie dropping at the end.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void BurstRpc(Vector3 at, byte boss)
        {
            var fx = FxSystem.Instance;
            if (fx == null) return;
            var color = boss == 0 ? ZoneMap.ManaColor : EasterEggData.BossGlow((EggBoss)boss);
            fx.Flash(at + Vector3.up, color, boss == 0 ? 6f : 18f, boss == 0 ? 0.2f : 0.5f);
            fx.Impact(at + Vector3.up * 0.2f, Vector3.up, color, boss == 0 ? 10 : 30, boss == 0 ? 3f : 6f, 0.07f);
            fx.PlayAt(at, boss == 0 ? ProceduralAudio.Zap : ProceduralAudio.Thunder, boss == 0 ? 0.5f : 1f);
        }

        /// <summary>A fire-rain mark: a glowing ring where the strike will land.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void MarkRpc(Vector3 at)
        {
            var mark = Greybox.Prim(PrimitiveType.Cylinder, "Fire Mark", null, new Vector3(at.x, at.y + 0.04f, at.z),
                new Vector3(EasterEggData.StrikeRadius * 2f, 0.01f, EasterEggData.StrikeRadius * 2f), new Color(1f, 0.3f, 0.1f, 0.45f), collider: false, unlit: true);
            Greybox.DisableShadows(mark);
            _marks.Add((mark, Time.time + EasterEggData.StrikeWarning + 0.1f));
            if (FxSystem.Instance != null) FxSystem.Instance.PlayAt(at, ProceduralAudio.Whoosh, 0.6f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void FireRpc(Vector3 at)
        {
            var fx = FxSystem.Instance;
            if (fx == null) return;
            fx.Flash(at + Vector3.up * 1.2f, ZoneMap.BloodGlow, 12f, 0.35f);
            fx.Impact(at + Vector3.up * 0.1f, Vector3.up, ZoneMap.BloodGlow, 24, 7f, 0.08f);
            fx.PlayAt(at, ProceduralAudio.Explosion, 0.8f);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void FailedRpc()
        {
            var step = EasterEggData.At(StepsDone.Value);
            if (step != null) Say(step.onFail);
            Moment?.Invoke("THE WATCHMAN IS DOWN", "He'll talk to you again when you're ready to try.", EasterEggData.SpeakerColor(EggSpeaker.Watchman));
        }

        // ---------------------------------------------------------------- every machine

        void ClientTick()
        {
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var (mark, until) = _marks[i];
                if (mark == null)
                {
                    _marks.RemoveAt(i);
                    continue;
                }
                if (Time.time < until)
                {
                    // Pulses faster as it's about to land.
                    float left = until - Time.time;
                    mark.transform.localScale = new Vector3(1f, 0.005f, 1f) * EasterEggData.StrikeRadius * 2f * (0.85f + 0.15f * Mathf.Sin(Time.time * (30f / Mathf.Max(0.2f, left))));
                    continue;
                }
                Destroy(mark);
                _marks.RemoveAt(i);
            }
            if (_props == null && GameConfig.Map != null && Time.frameCount % 30 == 0) BuildProps(GameConfig.Map);
            if (_props != null) _props.SetWatchmanAt(WatchmanPosition);
            float health = WatchmanHealth.Value;
            if (health < _lastWatchmanHealth - 0.001f) _hurtUntil = Time.time + 0.15f;
            _lastWatchmanHealth = health;
            if (_props != null) _props.SetWatchmanHurt(Time.time < _hurtUntil ? 1f : 0f);
            if (_props != null && Active.Value && StepsDone.Value == 1) _props.SetCharge(Total.Value > 0 ? Kills.Value / (float)Total.Value : 0f, Paused.Value);
        }

        void OnActiveChanged(bool was, bool now)
        {
            ShowState();
            var step = CurrentStep;
            if (now && !was && step != null)
            {
                Say(step.onStart);
                StepStarted?.Invoke(StepsDone.Value);
            }
        }

        void OnKillsChanged(int was, int now)
        {
            var step = CurrentStep;
            if (!Active.Value || step == null || step.along == null) return;
            foreach (var (at, line) in step.along)
                if (was < at && now >= at) LineSaid?.Invoke(line);
        }

        void OnStepsDoneChanged(int was, int now)
        {
            ShowState();
            // A restart sets it back to 0: nothing to say.
            if (now != was + 1) return;
            var step = EasterEggData.At(was);
            if (step != null) Say(step.onComplete);
            StepCompleted?.Invoke(was);
        }

        void OnRelicsChanged(byte was, byte now)
        {
            ShowState();
            int carriedBefore = Count(was & 0xF) + Count(was >> 4), carriedNow = Count(now & 0xF) + Count(now >> 4);
            if (carriedNow > carriedBefore)
            {
                if (carriedBefore == 0) LineSaid?.Invoke(EasterEggData.FirstRelic);
                Moment?.Invoke("RELIC FOUND", $"{carriedNow} of {EasterEggData.RelicCount}. They go round the altar in the Archives.", new Color(1f, 0.82f, 0.45f));
            }
            if (Count(now >> 4) == EasterEggData.RelicCount && Count(was >> 4) < EasterEggData.RelicCount) LineSaid?.Invoke(EasterEggData.AllRelics);
        }

        void OnPhaseChanged(EggPhase was, EggPhase now)
        {
            ShowState();
            if (now == EggPhase.FightGod) Say(EasterEggData.ChoseFight);
            else if (now == EggPhase.GeneralSpeaks)
            {
                Say(EasterEggData.ChoseServe);
                Say(EasterEggData.GeneralIntro);
            }
            else if (now == EggPhase.Choice)
                Moment?.Invoke("CHOOSE", "Fight Azhrakel, or serve him and hunt the Betrayer. The stones either side of the altar.", ZoneMap.BloodGlow);
        }

        void OnEndingChanged(EggBoon was, EggBoon now)
        {
            ShowState();
            if (now == EggBoon.None) return;
            Say(now == EggBoon.Godblood ? EasterEggData.FightWon : EasterEggData.ServeWon);
            string gift = now == EggBoon.Godblood ? $"the {EasterEggData.GodGunName}" : EasterEggData.SwordName;
            Moment?.Invoke(EasterEggData.BoonName(now).ToUpperInvariant(), $"{EasterEggData.BoonText(now)} for the rest of the game, and {gift}.", EasterEggData.SpeakerColor(EggSpeaker.God));
        }

        void OnPausedChanged(bool was, bool now) => ShowState();

        static void Say(EggLine[] lines)
        {
            if (lines == null) return;
            foreach (var line in lines) LineSaid?.Invoke(line);
        }

        void ShowState()
        {
            int done = StepsDone.Value;
            bool active = Active.Value;
            var phase = Phase.Value;
            if (_lockdown != null) _lockdown.Sealed = active && done == 0;
            if (_fx != null) _fx.SetState(active && done == 0 ? ReactorFx.State.Waking : done > 0 ? ReactorFx.State.Awake : ReactorFx.State.Dormant);
            if (_props == null) return;
            var v = new EggProps.View
            {
                relicOnFloor = new bool[EasterEggData.RelicCount],
                relicPlaced = new bool[EasterEggData.RelicCount],
            };
            for (int i = 0; i < EasterEggData.RelicCount; i++)
            {
                v.relicOnFloor[i] = done == 1 && !RelicTaken(i);
                v.relicPlaced[i] = done > 1 || (done == 1 && (Relics.Value & (1 << (i + 4))) != 0);
            }
            v.altarSpark = done == 1 && active;
            v.ritual = done == 1 && active;
            v.ritualPaused = Paused.Value;
            v.charge = Total.Value > 0 ? Kills.Value / (float)Total.Value : 0f;
            v.watchman = done >= 2;
            v.stonesSpark = done == 3 && active;
            v.stonesAwake = done == 3 && active;
            v.portal = done >= 4;
            v.altarItems = (done == 4 && phase != EggPhase.None) || done >= 5;
            v.god = done == 4 && (phase == EggPhase.GodSpeaks || phase == EggPhase.Choice);
            v.godRising = v.god;
            v.general = done == 4 ? phase != EggPhase.FightGeneral : Ending.Value != EggBoon.EmberOath;
            v.generalStanding = done == 4 && phase == EggPhase.GeneralSpeaks;
            v.choices = done == 4 && phase == EggPhase.Choice;
            _props.Show(v);
        }
    }
}
